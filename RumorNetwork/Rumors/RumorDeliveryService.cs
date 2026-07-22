using System.Collections.Generic;
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
            error = string.Empty;

            if (knowledge == RumorKnowledgeLevel.NotSold)
            {
                error =
                    "O rumor precisa ser entregue como " +
                    "approximate ou exact.";

                return false;
            }

            bool selected =
                rumorRegistry.TryPickRandomNotSold(
                    api.World.Rand,
                    out RumorRecord? record
                );

            if (!selected || record == null)
            {
                error =
                    "Não existem rumores " +
                    "ainda não vendidos.";

                return false;
            }

            bool targetResolved =
                rumorTargetResolver.TryResolveAll(
                    record,
                    out RumorTargetResolution? resolution,
                    out string targetError
                );

            if (
                !targetResolved ||
                resolution == null
            )
            {
                error = targetError;
                return false;
            }

            bool waypointsAdded =
                RumorWaypointService.TryAddWaypoints(
                    api,
                    player,
                    record,
                    knowledge,
                    resolution,
                    api.World.Rand,
                    out IReadOnlyList<Vec3d> waypointPositions,
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
                    knowledge
                );

            if (!committed)
            {
                logger.Error(
                    $"Os waypoints do rumor {record.Id} " +
                    "foram criados, mas o registro não pôde " +
                    "ser marcado como vendido."
                );

                error =
                    "As localizações foram adicionadas ao mapa, " +
                    "mas o rumor não pôde ser registrado " +
                    "como vendido.";

                return false;
            }

            LogDelivery(
                record,
                knowledge,
                resolution,
                waypointPositions
            );

            deliveredRecord = record;
            return true;
        }

        private void LogDelivery(
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTargetResolution resolution,
            IReadOnlyList<Vec3d> waypointPositions
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
                $"Waypoints={waypointPositions.Count}"
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
                index < waypointPositions.Count;
                index++
            )
            {
                Vec3d waypointPosition =
                    waypointPositions[index];

                logger.Notification(
                    $"Waypoint[{index}]=(" +
                    $"{waypointPosition.X:0.0}; " +
                    $"{waypointPosition.Y:0.0}; " +
                    $"{waypointPosition.Z:0.0})"
                );
            }
        }
    }
}
