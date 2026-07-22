using System.Collections.Generic;
using System.Text;
using RumorNetwork.Offers;
using RumorNetwork.Traders;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class RumorOfferCommands
    {
        private readonly RumorOfferService offerService;
        private readonly TraderLocationPurchaseService
            traderPurchaseService;
        private readonly TraderKnowledgeRegistry
            traderKnowledgeRegistry;

        public RumorOfferCommands(
            RumorOfferService offerService,
            TraderLocationPurchaseService
                traderPurchaseService,
            TraderKnowledgeRegistry
                traderKnowledgeRegistry
        )
        {
            this.offerService = offerService;
            this.traderPurchaseService =
                traderPurchaseService;
            this.traderKnowledgeRegistry =
                traderKnowledgeRegistry;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("offers")
                .WithDescription(
                    "Lista as ofertas atualmente configuradas."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ListOffers)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("buytrader")
                .WithDescription(
                    "Compra de um comerciante próximo a " +
                    "localização do comerciante desconhecido " +
                    "mais próximo."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(BuyTraderLocation)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("cleartraderknowledge")
                .WithDescription(
                    "Limpa o conhecimento de comerciantes " +
                    "do jogador para debug."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ClearTraderKnowledge)
                .EndSubCommand();
        }

        private TextCommandResult ListOffers(
            TextCommandCallingArgs args
        )
        {
            bool resolved =
                offerService.TryGetOffers(
                    out IReadOnlyList<RumorOffer> offers,
                    out string error
                );

            if (!resolved)
            {
                return TextCommandResult.Error(error);
            }

            StringBuilder text = new(
                "Ofertas do Rumor Network:"
            );

            foreach (RumorOffer offer in offers)
            {
                text.AppendLine();
                text.Append("- ");
                text.Append(offer.Title);
                text.Append(": ");
                text.Append(
                    offer.PreviewPrice.Description
                );

                if (offer.PriceMayVary)
                {
                    text.Append(
                        " (preço base; pode variar pelo destino)"
                    );
                }
            }

            return TextCommandResult.Success(
                text.ToString()
            );
        }

        private TextCommandResult BuyTraderLocation(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return PlayerRequiredResult();
            }

            bool purchased =
                traderPurchaseService.TryPurchase(
                    player,
                    out TraderLocationPurchaseResult? result,
                    out string error
                );

            if (!purchased || result == null)
            {
                return TextCommandResult.Error(error);
            }

            return TextCommandResult.Success(
                "Localização de comerciante comprada. " +
                $"Distância a partir do vendedor: " +
                $"{result.TargetDistance:0} blocos. " +
                $"Pago: {result.Price.Description}. " +
                $"Este vendedor ainda possui " +
                $"{result.SellerPurchasesRemaining} " +
                "localizações para você."
            );
        }

        private TextCommandResult ClearTraderKnowledge(
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
                traderKnowledgeRegistry.Clear(
                    player.PlayerUID,
                    out int revealedCount,
                    out int visitedCount,
                    out int purchaseCount
                );

            if (!cleared)
            {
                return TextCommandResult.Success(
                    "O jogador ainda não possuía " +
                    "conhecimento de comerciantes."
                );
            }

            return TextCommandResult.Success(
                "Conhecimento de comerciantes limpo. " +
                $"Revelados={revealedCount} | " +
                $"Visitados={visitedCount} | " +
                $"Compras={purchaseCount}."
            );
        }

        private static TextCommandResult
            PlayerRequiredResult()
        {
            return TextCommandResult.Error(
                "O comando precisa ser executado " +
                "por um jogador."
            );
        }
    }
}
