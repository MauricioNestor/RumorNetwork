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
                rumorTargetResolver.TryResolve(
                    record,
                    out RumorTarget? target,
                    out string targetError
                );

            if (
                !targetResolved ||
                target == null
            )
            {
                error = targetError;
                return false;
            }

            bool waypointAdded =
                RumorWaypointService.TryAddWaypoint(
                    api,
                    player,
                    record,
                    knowledge,
                    target,
                    api.World.Rand,
                    out Vec3d waypointPosition,
                    out string waypointError
                );

            if (!waypointAdded)
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
                    $"O waypoint do rumor {record.Id} " +
                    "foi criado, mas o registro não pôde " +
                    "ser marcado como vendido."
                );

                error =
                    "A localização foi adicionada ao mapa, " +
                    "mas o rumor não pôde ser registrado " +
                    "como vendido.";

                return false;
            }

            LogDelivery(
                record,
                knowledge,
                target,
                waypointPosition
            );

            deliveredRecord = record;
            return true;
        }

        private void LogDelivery(
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTarget target,
            Vec3d waypointPosition
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
                $"Parts={record.PartCount}"
            );

            logger.Notification(
                $"TrueCenter=(" +
                $"{center.X}; " +
                $"{center.Y}; " +
                $"{center.Z}) | " +
                $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            logger.Notification(
                $"ResolvedTarget=(" +
                $"{target.Position.X:0.0}; " +
                $"{target.Position.Y:0.0}; " +
                $"{target.Position.Z:0.0}) | " +
                $"TargetKind={target.Kind}"
            );

            logger.Notification(
                $"Waypoint=(" +
                $"{waypointPosition.X:0.0}; " +
                $"{waypointPosition.Y:0.0}; " +
                $"{waypointPosition.Z:0.0})"
            );
        }
    }
}
