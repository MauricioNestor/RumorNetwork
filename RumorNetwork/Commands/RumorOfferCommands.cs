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

        public RumorOfferCommands(
            RumorOfferService offerService,
            TraderLocationPurchaseService
                traderPurchaseService
        )
        {
            this.offerService = offerService;
            this.traderPurchaseService =
                traderPurchaseService;
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
        }

        private TextCommandResult ListOffers(
            TextCommandCallingArgs args
        )
        {
            bool resolved =
                offerService.TryGetOffers(
                    out var offers,
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
                return TextCommandResult.Error(
                    "O comando precisa ser executado " +
                    "por um jogador."
                );
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
                $"Pago: {result.Price.Description}."
            );
        }
    }
}
