using System.Collections.Generic;
using RumorNetwork.Catalog;
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

        private readonly SelectiveStructureCatalogService
            catalogService;

        public RumorPurchaseService(
            RumorDeliveryService deliveryService,
            RumorPriceResolver priceResolver,
            RumorInventoryPaymentService paymentService,
            SelectiveStructureCatalogService catalogService
        )
        {
            this.deliveryService = deliveryService;
            this.priceResolver = priceResolver;
            this.paymentService = paymentService;
            this.catalogService = catalogService;
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

            catalogService.RequestBackfillAround(
                (int)player.Entity.Pos.X,
                (int)player.Entity.Pos.Z
            );

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
                error = catalogService.IsWorking
                    ? preparationError +
                        " O catálogo remoto ainda está " +
                        "verificando traders e translocadores " +
                        $"em {catalogService.PendingRegionCount} " +
                        "regiões já geradas."
                    : preparationError;

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
