using RumorNetwork.Purchases;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class RumorDeliveryCommands
    {
        private readonly ICoreServerAPI api;

        private readonly RumorDeliveryService
            rumorDeliveryService;

        private readonly RumorPurchaseService
            rumorPurchaseService;

        public RumorDeliveryCommands(
            ICoreServerAPI api,
            RumorDeliveryService rumorDeliveryService,
            RumorPurchaseService rumorPurchaseService
        )
        {
            this.api = api;
            this.rumorDeliveryService =
                rumorDeliveryService;

            this.rumorPurchaseService =
                rumorPurchaseService;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("draw")
                .WithDescription(
                    "Sorteia gratuitamente um rumor " +
                    "ainda não vendido para debug."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    api.ChatCommands.Parsers.Word(
                        "knowledge"
                    )
                )
                .HandleWith(DrawRumor)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("buy")
                .WithDescription(
                    "Compra um rumor usando o preço " +
                    "configurado."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    api.ChatCommands.Parsers.Word(
                        "knowledge"
                    )
                )
                .HandleWith(BuyRumor)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("clearwaypoints")
                .WithDescription(
                    "Remove os waypoints criados pelo " +
                    "Rumor Network."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ClearRumorWaypoints)
                .EndSubCommand();
        }

        private TextCommandResult DrawRumor(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return PlayerRequiredResult();
            }

            if (!TryReadKnowledge(
                    args,
                    out RumorKnowledgeLevel knowledge,
                    out TextCommandResult parseError
                ))
            {
                return parseError;
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

            return TextCommandResult.Success(
                $"Rumor de debug sorteado: " +
                $"{record.Kind}. " +
                $"{CreatePrecisionText(knowledge)} " +
                "adicionada ao mapa gratuitamente."
            );
        }

        private TextCommandResult BuyRumor(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return PlayerRequiredResult();
            }

            if (!TryReadKnowledge(
                    args,
                    out RumorKnowledgeLevel knowledge,
                    out TextCommandResult parseError
                ))
            {
                return parseError;
            }

            bool purchased =
                rumorPurchaseService.TryPurchase(
                    player,
                    knowledge,
                    out RumorPurchaseResult? result,
                    out string purchaseError
                );

            if (!purchased || result == null)
            {
                return TextCommandResult.Error(
                    purchaseError
                );
            }

            return TextCommandResult.Success(
                $"Rumor comprado: " +
                $"{result.Record.Kind}. " +
                $"{CreatePrecisionText(knowledge)} " +
                "adicionada ao mapa. " +
                $"Pago: {result.Price.Description}."
            );
        }

        private TextCommandResult ClearRumorWaypoints(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return PlayerRequiredResult();
            }

            bool cleared =
                RumorWaypointService.TryClearWaypoints(
                    api,
                    player,
                    out int removedCount,
                    out string clearError
                );

            if (!cleared)
            {
                return TextCommandResult.Error(
                    clearError
                );
            }

            return TextCommandResult.Success(
                removedCount == 1
                    ? "1 waypoint do Rumor Network removido."
                    : $"{removedCount} waypoints do " +
                        "Rumor Network removidos."
            );
        }

        private static bool TryReadKnowledge(
            TextCommandCallingArgs args,
            out RumorKnowledgeLevel knowledge,
            out TextCommandResult error
        )
        {
            string requestedKnowledge =
                ((string)args[0]).Trim();

            if (TryParseKnowledgeLevel(
                    requestedKnowledge,
                    out knowledge
                ))
            {
                error = TextCommandResult.Success();
                return true;
            }

            error = TextCommandResult.Error(
                "Tipo inválido. " +
                "Use approximate ou exact."
            );

            return false;
        }

        private static string CreatePrecisionText(
            RumorKnowledgeLevel knowledge
        )
        {
            return knowledge
                == RumorKnowledgeLevel.Approximate
                    ? "Localização aproximada"
                    : "Localização exata";
        }

        private static TextCommandResult
            PlayerRequiredResult()
        {
            return TextCommandResult.Error(
                "O comando precisa ser " +
                "executado por um jogador."
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
