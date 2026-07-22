using System.Collections.Generic;
using RumorNetwork.Rumors;
using Vintagestory.API.Server;

namespace RumorNetwork.Purchases
{
    public sealed class RumorPurchaseService
    {
        private readonly RumorDeliveryService
            deliveryService;

        private readonly RumorPriceResolver
            priceResolver;

        private readonly RumorInventoryPaymentService
            paymentService;

        public RumorPurchaseService(
            RumorDeliveryService deliveryService,
            RumorPriceResolver priceResolver,
            RumorInventoryPaymentService paymentService
        )
        {
            this.deliveryService = deliveryService;
            this.priceResolver = priceResolver;
            this.paymentService = paymentService;
        }

        public bool TryPurchase(
            IServerPlayer player,
            RumorKnowledgeLevel knowledge,
            out RumorPurchaseResult? result,
            out string error
        )
        {
            result = null;
            error = string.Empty;

            bool prepared =
                deliveryService.TryPrepare(
                    knowledge,
                    out RumorPreparedDelivery?
                        preparedDelivery,
                    out string preparationError
                );

            if (
                !prepared ||
                preparedDelivery == null
            )
            {
                error = preparationError;
                return false;
            }

            bool priceResolved =
                priceResolver.TryResolve(
                    preparedDelivery.Record.Kind,
                    knowledge,
                    out RumorPrice? price,
                    out string priceError
                );

            if (!priceResolved || price == null)
            {
                error = priceError;
                return false;
            }

            bool paymentTaken =
                paymentService.TryTake(
                    player,
                    price,
                    out RumorPaymentReceipt? receipt,
                    out string paymentError
                );

            if (!paymentTaken || receipt == null)
            {
                error =
                    $"{paymentError} " +
                    $"Preço: {price.Description}.";

                return false;
            }

            bool delivered =
                deliveryService.TryDeliverPrepared(
                    player,
                    preparedDelivery,
                    out IReadOnlyList<RumorWaypointHandle>
                        waypointHandles,
                    out string deliveryError
                );

            if (!delivered)
            {
                bool refunded =
                    paymentService.Refund(
                        player,
                        receipt,
                        out string refundError
                    );

                error = refunded
                    ? deliveryError +
                        " O pagamento foi devolvido."
                    : deliveryError +
                        " " +
                        refundError;

                return false;
            }

            result = new RumorPurchaseResult(
                preparedDelivery.Record,
                price,
                waypointHandles
            );

            return true;
        }
    }
}
