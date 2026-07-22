using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Rumors
{
    public sealed class RumorDeliveryService
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorRegistry rumorRegistry;
        private readonly RumorTargetResolver rumorTargetResolver;

        public RumorDeliveryService(
            ICoreServerAPI api,
            ILogger logger,
            RumorRegistry rumorRegistry,
            RumorTargetResolver rumorTargetResolver
        )
        {
            this.api = api;
            this.logger = logger;
            this.rumorRegistry = rumorRegistry;
            this.rumorTargetResolver = rumorTargetResolver;
        }

        public bool TryDeliver(
            IServerPlayer player,
            RumorKnowledgeLevel knowledge,
            out RumorRecord? deliveredRecord,
            out string error
        )
        {
            deliveredRecord = null;

            bool prepared =
                TryPrepare(
                    knowledge,
                    out RumorPreparedDelivery?
                        preparedDelivery,
                    out error
                );

            if (
                !prepared ||
                preparedDelivery == null
            )
            {
                return false;
            }

            bool delivered =
                TryDeliverPrepared(
                    player,
                    preparedDelivery,
                    out _,
                    out error
                );

            if (!delivered)
            {
                return false;
            }

            deliveredRecord =
                preparedDelivery.Record;

            return true;
        }

        public bool TryPrepare(
            RumorKnowledgeLevel knowledge,
            out RumorPreparedDelivery?
                preparedDelivery,
            out string error
        )
        {
            preparedDelivery = null;
            error = string.Empty;

            if (knowledge == RumorKnowledgeLevel.NotSold)
            {
                error =
                    "O rumor precisa ser entregue como " +
                    "approximate ou exact.";

                return false;
            }

            List<RumorRecord> candidates =
                rumorRegistry
                    .CreateShuffledNotSoldCandidates(
                        api.World.Rand
                    );

            if (candidates.Count == 0)
            {
                error =
                    "Não existem rumores " +
                    "ainda não vendidos.";

                return false;
            }

            Dictionary<
                RumorResolutionFailureKind,
                int
            > failures = new();

            foreach (RumorRecord record in candidates)
            {
                bool targetResolved =
                    rumorTargetResolver.TryResolveAll(
                        record,
                        out RumorTargetResolution? resolution,
                        out RumorResolutionFailureKind failure,
                        out string targetError
                    );

                if (
                    !targetResolved ||
                    resolution == null
                )
                {
                    IncrementFailure(
                        failures,
                        failure
                    );

                    logger.VerboseDebug(
                        $"Rumor {record.Id} ignorado " +
                        $"durante o sorteio: {targetError}"
                    );

                    continue;
                }

                preparedDelivery =
                    new RumorPreparedDelivery(
                        record,
                        knowledge,
                        resolution
                    );

                return true;
            }

            error = CreateNoResolvableRumorError(
                candidates.Count,
                failures
            );

            logger.Notification(
                "=== Rumor Network: nenhum candidato resolvível ==="
            );

            logger.Notification(error);
            return false;
        }

        public bool TryDeliverPrepared(
            IServerPlayer player,
            RumorPreparedDelivery preparedDelivery,
            out IReadOnlyList<RumorWaypointHandle>
                waypointHandles,
            out string error
        )
        {
            waypointHandles =
                new List<RumorWaypointHandle>()
                    .AsReadOnly();

            error = string.Empty;

            RumorRecord record =
                preparedDelivery.Record;

            if (
                record.Knowledge
                != RumorKnowledgeLevel.NotSold
            )
            {
                error =
                    "O rumor preparado já foi vendido.";

                return false;
            }

            bool waypointsAdded =
                RumorWaypointService
                    .TryAddWaypointsTracked(
                        api,
                        player,
                        record,
                        preparedDelivery.Knowledge,
                        preparedDelivery.Resolution,
                        api.World.Rand,
                        out waypointHandles,
                        out string waypointError
                    );

            if (!waypointsAdded)
            {
                error = waypointError;
                return false;
            }

            bool committed =
                rumorRegistry.TryMarkSold(
                    record.Id,
                    preparedDelivery.Knowledge
                );

            if (!committed)
            {
                bool cleaned =
                    RumorWaypointService
                        .TryRemoveWaypoints(
                            api,
                            player,
                            waypointHandles.Select(
                                handle =>
                                    handle.Guid
                            ),
                            out _,
                            out string cleanupError
                        );

                logger.Error(
                    $"Os waypoints do rumor {record.Id} " +
                    "foram criados, mas o registro não pôde " +
                    "ser marcado como vendido."
                );

                error = cleaned
                    ? "O rumor não pôde ser registrado. " +
                        "Os waypoints foram removidos."
                    : "O rumor não pôde ser registrado e " +
                        "os waypoints não puderam ser " +
                        $"removidos: {cleanupError}";

                return false;
            }

            LogDelivery(
                record,
                preparedDelivery.Knowledge,
                preparedDelivery.Resolution,
                waypointHandles
            );

            return true;
        }

        private static void IncrementFailure(
            Dictionary<
                RumorResolutionFailureKind,
                int
            > failures,
            RumorResolutionFailureKind failure
        )
        {
            if (!failures.TryGetValue(
                    failure,
                    out int count
                ))
            {
                count = 0;
            }

            failures[failure] = count + 1;
        }

        private static int GetFailureCount(
            Dictionary<
                RumorResolutionFailureKind,
                int
            > failures,
            RumorResolutionFailureKind failure
        )
        {
            return failures.TryGetValue(
                failure,
                out int count
            )
                ? count
                : 0;
        }

        private static string CreateNoResolvableRumorError(
            int testedCount,
            Dictionary<
                RumorResolutionFailureKind,
                int
            > failures
        )
        {
            int noOpenings = GetFailureCount(
                failures,
                RumorResolutionFailureKind.NoOpenings
            );

            int enclosed = GetFailureCount(
                failures,
                RumorResolutionFailureKind.Enclosed
            );

            int chunksUnavailable = GetFailureCount(
                failures,
                RumorResolutionFailureKind.ChunksUnavailable
            );

            int searchLimit = GetFailureCount(
                failures,
                RumorResolutionFailureKind.SearchLimitReached
            );

            int unknown = GetFailureCount(
                failures,
                RumorResolutionFailureKind.Unknown
            );

            return
                "Nenhum rumor pôde ser resolvido agora. " +
                $"Testados={testedCount} | " +
                $"Sem abertura={noOpenings} | " +
                $"Fechados={enclosed} | " +
                $"Chunks indisponíveis={chunksUnavailable} | " +
                $"Limite={searchLimit} | " +
                $"Outros={unknown}.";
        }

        private void LogDelivery(
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTargetResolution resolution,
            IReadOnlyList<RumorWaypointHandle>
                waypointHandles
        )
        {
            Cuboidi box =
                record.CreateLocation();

            Vec3i center =
                box.Center;

            logger.Notification(
                "=== Rumor Network: rumor sorteado ==="
            );

            logger.Notification(
                $"Id={record.Id}"
            );

            logger.Notification(
                $"Knowledge={knowledge} | " +
                $"Kind={record.Kind} | " +
                $"Family={record.Family} | " +
                $"Parts={record.PartCount} | " +
                $"Waypoints={waypointHandles.Count}"
            );

            logger.Notification(
                $"TrueCenter=(" +
                $"{center.X}; " +
                $"{center.Y}; " +
                $"{center.Z}) | " +
                $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            for (
                int index = 0;
                index < resolution.Targets.Count;
                index++
            )
            {
                RumorTarget target =
                    resolution.Targets[index];

                logger.Notification(
                    $"ResolvedTarget[{index}]=(" +
                    $"{target.Position.X:0.0}; " +
                    $"{target.Position.Y:0.0}; " +
                    $"{target.Position.Z:0.0}) | " +
                    $"TargetKind={target.Kind}"
                );
            }

            for (
                int index = 0;
                index < waypointHandles.Count;
                index++
            )
            {
                RumorWaypointHandle handle =
                    waypointHandles[index];

                logger.Notification(
                    $"Waypoint[{index}]=(" +
                    $"{handle.Position.X:0.0}; " +
                    $"{handle.Position.Y:0.0}; " +
                    $"{handle.Position.Z:0.0}) | " +
                    $"Guid={handle.Guid}"
                );
            }
        }
    }
}
