using System.Collections.Generic;
using RumorNetwork.Catalog;
using RumorNetwork.Configuration;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Purchases
{
    public sealed class BetterRuinsPurchaseService
    {
        private readonly ICoreServerAPI api;
        private readonly RumorRegistry rumorRegistry;
        private readonly int regionSearchRadius;
        private readonly RumorTargetResolver targetResolver;
        private readonly RumorDeliveryService deliveryService;
        private readonly RumorPriceResolver priceResolver;
        private readonly RumorInventoryPaymentService paymentService;
        private readonly HashSet<string> physicallyInvalidRecords = new();

        public BetterRuinsPurchaseService(
            ICoreServerAPI api,
            RumorRegistry rumorRegistry,
            int regionSearchRadius,
            RumorTargetResolver targetResolver,
            RumorDeliveryService deliveryService,
            RumorPriceResolver priceResolver,
            RumorInventoryPaymentService paymentService
        )
        {
            this.api = api;
            this.rumorRegistry = rumorRegistry;
            this.regionSearchRadius = regionSearchRadius;
            this.targetResolver = targetResolver;
            this.deliveryService = deliveryService;
            this.priceResolver = priceResolver;
            this.paymentService = paymentService;
        }

        public bool TryPurchase(
            IServerPlayer player,
            out RumorPurchaseResult? result,
            out string error
        )
        {
            result = null;
            error = string.Empty;

            if (!BetterRuinsRumorPolicy.DedicatedAvailable)
            {
                error =
                    "Rumores dedicados do BetterRuins estão " +
                    "desativados ou o mod não está instalado.";
                return false;
            }

            IndexLoadedRumors(player);

            List<RumorRecord> candidates =
                rumorRegistry
                    .CreateShuffledBetterRuinsCandidates(
                        api.World.Rand,
                        RumorKnowledgeLevel.Exact
                    );

            RumorPreparedDelivery? prepared = null;

            foreach (RumorRecord record in candidates)
            {
                if (physicallyInvalidRecords.Contains(record.Id))
                {
                    continue;
                }

                StructurePhysicalAuditResult audit =
                    StructurePhysicalAuditor.Audit(api, record);

                if (!audit.ChunksLoaded)
                {
                    api.Logger.VerboseDebug(
                        $"BetterRuins {record.Id} ignorada por enquanto: " +
                        "os chunks da bounding box não estão carregados."
                    );
                    continue;
                }

                if (!audit.HasStructuralEvidence)
                {
                    physicallyInvalidRecords.Add(record.Id);

                    api.Logger.Warning(
                        "Rumor Network descartou uma ruína registrada do " +
                        "BetterRuins sem evidência física de construção. " +
                        $"Id={record.Id} | SourceCode={record.SourceCode} | " +
                        $"BoxVolume={audit.DeclaredVolume} | " +
                        $"Air={audit.AirBlocks} | Natural={audit.NaturalBlocks} | " +
                        $"Artificial={audit.ArtificialBlocks} | " +
                        $"BlockEntities={audit.BlockEntities} | " +
                        $"TopBlocks={audit.FormatTopBlocks()}"
                    );
                    continue;
                }

                bool resolved = targetResolver.TryResolveAll(
                    record,
                    out RumorTargetResolution? resolution,
                    out _,
                    out _
                );

                if (!resolved || resolution == null)
                {
                    continue;
                }

                prepared = new RumorPreparedDelivery(
                    record,
                    RumorKnowledgeLevel.Exact,
                    resolution
                );
                break;
            }

            if (prepared == null)
            {
                error =
                    "Não conheço nenhuma ruína do BetterRuins " +
                    "que ainda possa indicar e que exista fisicamente.";
                return false;
            }

            bool priceResolved =
                priceResolver.TryResolveBetterRuins(
                    out RumorPrice? price,
                    out string priceError
                );

            if (!priceResolved || price == null)
            {
                error = priceError;
                return false;
            }

            bool paymentTaken = paymentService.TryTake(
                player,
                price,
                out RumorPaymentReceipt? receipt,
                out string paymentError
            );

            if (!paymentTaken || receipt == null)
            {
                error =
                    paymentError +
                    " Preço: " +
                    price.Description +
                    ".";
                return false;
            }

            bool delivered = deliveryService.TryDeliverPrepared(
                player,
                prepared,
                out IReadOnlyList<RumorWaypointHandle> handles,
                out string deliveryError
            );

            if (!delivered)
            {
                bool refunded = paymentService.Refund(
                    player,
                    receipt,
                    out string refundError
                );

                error = refunded
                    ? deliveryError +
                        " O pagamento foi devolvido."
                    : deliveryError + " " + refundError;
                return false;
            }

            result = new RumorPurchaseResult(
                prepared.Record,
                price,
                handles
            );
            return true;
        }

        private void IndexLoadedRumors(
            IServerPlayer player
        )
        {
            List<GeneratedStructure> structures =
                MapRegionStructureCollector
                    .CollectLoadedNeighborhood(
                        api,
                        player.Entity.Pos.AsBlockPos,
                        regionSearchRadius,
                        out _
                    );

            rumorRegistry.Merge(
                RumorSiteBuilder.Build(structures)
            );
        }
    }
}
