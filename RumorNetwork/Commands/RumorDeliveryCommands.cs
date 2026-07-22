using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class RumorDeliveryCommands
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorRegistry rumorRegistry;
        private readonly RumorTargetResolver rumorTargetResolver;

        public RumorDeliveryCommands(
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

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("draw")
                .WithDescription(
                    "Sorteia um rumor ainda não vendido."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    api.ChatCommands.Parsers.Word("knowledge")
                )
                .HandleWith(DrawRumor)
                .EndSubCommand();
        }

        private TextCommandResult DrawRumor(
            TextCommandCallingArgs args
        )
        {
            string requestedKnowledge =
                ((string)args[0]).Trim();

            if (!TryParseKnowledgeLevel(
                    requestedKnowledge,
                    out RumorKnowledgeLevel knowledge
                ))
            {
                return TextCommandResult.Error(
                    "Tipo inválido. " +
                    "Use approximate ou exact."
                );
            }

            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return TextCommandResult.Error(
                    "O comando precisa ser " +
                    "executado por um jogador."
                );
            }

            bool selected =
                rumorRegistry.TryPickRandomNotSold(
                    api.World.Rand,
                    out RumorRecord? record
                );

            if (!selected || record == null)
            {
                return TextCommandResult.Error(
                    "Não existem rumores " +
                    "ainda não vendidos."
                );
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
                return TextCommandResult.Error(
                    targetError
                );
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
                return TextCommandResult.Error(
                    waypointError
                );
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

                return TextCommandResult.Error(
                    "A localização foi adicionada ao mapa, " +
                    "mas o rumor não pôde ser registrado " +
                    "como vendido."
                );
            }

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

            string precisionText =
                knowledge
                == RumorKnowledgeLevel.Approximate
                    ? "aproximada"
                    : "exata";

            return TextCommandResult.Success(
                $"Rumor sorteado: {record.Kind}. " +
                $"Localização {precisionText} " +
                "adicionada ao mapa."
            );
        }

        private static bool TryParseKnowledgeLevel(
            string value,
            out RumorKnowledgeLevel knowledge
        )
        {
            switch (value.ToLowerInvariant())
            {
                case "approximate":
                case "approx":
                    knowledge =
                        RumorKnowledgeLevel.Approximate;

                    return true;

                case "exact":
                    knowledge =
                        RumorKnowledgeLevel.Exact;

                    return true;

                default:
                    knowledge =
                        RumorKnowledgeLevel.NotSold;

                    return false;
            }
        }
    }
}
