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

        private readonly VerifiedStructureDiscoveryService
            discoveryService;

        public RumorPurchaseService(
            RumorDeliveryService deliveryService,
            RumorPriceResolver priceResolver,
            RumorInventoryPaymentService paymentService,
            VerifiedStructureDiscoveryService discoveryService
        )
        {
            this.deliveryService = deliveryService;
            this.priceResolver = priceResolver;
            this.paymentService = paymentService;
            this.discoveryService = discoveryService;
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

            // General rumors remain immediately purchasable. This starts
            // verified translocator discovery in the background so remote
            // translocators enter later draws without blocking ordinary
            // ruins and sites.
            discoveryService.RequestAdditional(
                StructureKind.Translocator,
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
                error = discoveryService.IsWorking
                    ? preparationError +
                        " A descoberta remota ainda está " +
                        "simulando worldgen sem salvar chunks. " +
                        $"Peeks ativos={discoveryService.ActivePeekCount} | " +
                        $"Buscas={discoveryService.ActiveSearchCount}."
                    : preparationError;

                return false;
            }

            // The general-rumor menu advertises a price by knowledge level,
            // before the target is drawn. Keep that price stable even when
            // the random target happens to be a translocator. The dedicated
            // translocator purchase remains responsible for its guaranteed,
            // structure-specific temporal-gear price.
            bool priceResolved =
                priceResolver.TryResolveGeneralPreview(
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
