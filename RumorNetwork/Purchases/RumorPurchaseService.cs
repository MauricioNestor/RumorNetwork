using System;
using System.Collections.Generic;
using RumorNetwork.Catalog;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Purchases
{
    public sealed class RumorPurchaseService
    {
        private readonly ICoreServerAPI? api;

        private readonly RumorRegistry? rumorRegistry;

        private readonly int regionSearchRadius;

        private readonly RumorDeliveryService
            deliveryService;

        private readonly RumorPriceResolver
            priceResolver;

        private readonly RumorInventoryPaymentService
            paymentService;

        private readonly VerifiedStructureDiscoveryService?
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

        public RumorPurchaseService(
            ICoreServerAPI api,
            RumorRegistry rumorRegistry,
            int regionSearchRadius,
            RumorDeliveryService deliveryService,
            RumorPriceResolver priceResolver,
            RumorInventoryPaymentService paymentService
        )
        {
            this.api = api;
            this.rumorRegistry = rumorRegistry;
            this.regionSearchRadius = Math.Max(
                0,
                regionSearchRadius
            );
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

            if (api != null && rumorRegistry != null)
            {
                IndexLoadedGeneralRumors(player);
            }
            else
            {
                // Compatibility path for callers that still use the previous
                // constructor. The runtime mod system now uses local ruin
                // indexing instead.
                discoveryService?.RequestAdditional(
                    StructureKind.Translocator,
                    (int)player.Entity.Pos.X,
                    (int)player.Entity.Pos.Z
                );
            }

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
                error = discoveryService?.IsWorking == true
                    ? preparationError +
                        " A descoberta remota ainda está " +
                        "simulando worldgen sem salvar chunks. " +
                        $"Peeks ativos={discoveryService.ActivePeekCount} | " +
                        $"Buscas={discoveryService.ActiveSearchCount}."
                    : preparationError;

                return false;
            }

            // The general-rumor menu advertises a stable price by knowledge
            // level before a ruin is drawn.
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

        private void IndexLoadedGeneralRumors(
            IServerPlayer player
        )
        {
            if (api == null || rumorRegistry == null)
            {
                return;
            }

            List<GeneratedStructure> structures =
                MapRegionStructureCollector
                    .CollectLoadedNeighborhood(
                        api,
                        player.Entity.Pos.AsBlockPos,
                        regionSearchRadius,
                        out _
                    );

            List<RumorSite> builtSites =
                RumorSiteBuilder.Build(structures);

            List<RumorSite> generalSites = new();

            foreach (RumorSite site in builtSites)
            {
                if (
                    RumorEligibilityPolicy
                        .IsGeneralRumorEligible(site.Kind)
                )
                {
                    generalSites.Add(site);
                }
            }

            rumorRegistry.Merge(generalSites);
        }
    }
}
