using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class RumorDeliveryCommands
    {
        private readonly ICoreServerAPI api;
        private readonly RumorDeliveryService rumorDeliveryService;

        public RumorDeliveryCommands(
            ICoreServerAPI api,
            RumorDeliveryService rumorDeliveryService
        )
        {
            this.api = api;
            this.rumorDeliveryService = rumorDeliveryService;
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

            bool delivered =
                rumorDeliveryService.TryDeliver(
                    player,
                    knowledge,
                    out RumorRecord? record,
                    out string deliveryError
                );

            if (!delivered || record == null)
            {
                return TextCommandResult.Error(
                    deliveryError
                );
            }

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
