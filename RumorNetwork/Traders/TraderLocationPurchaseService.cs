using System.Collections.Generic;
using RumorNetwork.Catalog;
using RumorNetwork.Configuration;
using RumorNetwork.Purchases;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Traders
{
    public sealed class TraderLocationPurchaseService
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorNetworkConfig config;
        private readonly RumorTargetResolver targetResolver;
        private readonly RumorPriceResolver priceResolver;
        private readonly RumorInventoryPaymentService paymentService;
        private readonly TraderKnowledgeRegistry knowledgeRegistry;
        private readonly TraderLocationSelector selector;
        private readonly VerifiedStructureDiscoveryService
            discoveryService;

        public TraderLocationPurchaseService(
            ICoreServerAPI api,
            ILogger logger,
            RumorNetworkConfig config,
            RumorTargetResolver targetResolver,
            RumorPriceResolver priceResolver,
            RumorInventoryPaymentService paymentService,
            TraderKnowledgeRegistry knowledgeRegistry,
            TraderLocationSelector selector,
            VerifiedStructureDiscoveryService discoveryService
        )
        {
            this.api = api;
            this.logger = logger;
            this.config = config;
            this.targetResolver = targetResolver;
            this.priceResolver = priceResolver;
            this.paymentService = paymentService;
            this.knowledgeRegistry = knowledgeRegistry;
            this.selector = selector;
            this.discoveryService = discoveryService;
        }

        public string CheckAvailability(
            IServerPlayer player
        )
        {
            if (!config.TraderLocations.Enabled)
            {
                return "none";
            }

            Vec3d playerPosition = new(
                player.Entity.Pos.X,
                player.Entity.Pos.Y,
                player.Entity.Pos.Z
            );

            bool sellerFound =
                selector.TryFindSeller(
                    playerPosition,
                    config.TraderLocations
                        .SellerMatchRadius,
                    out RumorRecord? seller,
                    out _
                );

            if (!sellerFound || seller == null)
            {
                discoveryService.RequestAdditional(
                    StructureKind.Trader,
                    (int)player.Entity.Pos.X,
                    (int)player.Entity.Pos.Z
                );

                return "searching";
            }

            Vec3i sellerCenter =
                seller.CreateLocation().Center;

            PlayerTraderKnowledge knowledge =
                knowledgeRegistry.GetOrCreate(
                    player.PlayerUID
                );

            knowledge.MarkVisited(seller.Id);

            int sellerPurchaseLimit =
                config.TraderLocations
                    .MaxLocationsSoldPerTrader;

            if (!knowledge.CanPurchaseFromSeller(
                    seller.Id,
                    sellerPurchaseLimit
                ))
            {
                return "quota";
            }

            bool targetFound =
                selector.TryFindNearestUnknown(
                    seller,
                    knowledge,
                    out _,
                    out _
                );

            if (targetFound)
            {
                return "available";
            }

            discoveryService.RequestAdditional(
                StructureKind.Trader,
                sellerCenter.X,
                sellerCenter.Z
            );

            return "none";
        }

        public bool TryPurchase(
            IServerPlayer player,
            out TraderLocationPurchaseResult? result,
            out string error
        )
        {
            result = null;
            error = string.Empty;

            if (!config.TraderLocations.Enabled)
            {
                error =
                    "Rumores de comerciantes estão " +
                    "desativados na configuração.";

                return false;
            }

            Vec3d playerPosition = new(
                player.Entity.Pos.X,
                player.Entity.Pos.Y,
                player.Entity.Pos.Z
            );

            bool sellerFound =
                selector.TryFindSeller(
                    playerPosition,
                    config.TraderLocations
                        .SellerMatchRadius,
                    out RumorRecord? seller,
                    out double sellerDistance
                );

            if (!sellerFound || seller == null)
            {
                discoveryService.RequestAdditional(
                    StructureKind.Trader,
                    (int)player.Entity.Pos.X,
                    (int)player.Entity.Pos.Z
                );

                error =
                    "Nenhum comerciante verificado foi encontrado " +
                    $"a até {config.TraderLocations.SellerMatchRadius:0} " +
                    "blocos. A descoberta por worldgen temporário " +
                    "foi acionada; aguarde e tente novamente.";

                return false;
            }

            Vec3i sellerCenter =
                seller.CreateLocation().Center;

            PlayerTraderKnowledge knowledge =
                knowledgeRegistry.GetOrCreate(
                    player.PlayerUID
                );

            knowledge.MarkVisited(seller.Id);

            int sellerPurchaseLimit =
                config.TraderLocations
                    .MaxLocationsSoldPerTrader;

            int sellerPurchasesUsed =
                knowledge.GetPurchasesFromSeller(
                    seller.Id
                );

            if (!knowledge.CanPurchaseFromSeller(
                    seller.Id,
                    sellerPurchaseLimit
                ))
            {
                error =
                    "Este comerciante já vendeu todas as " +
                    $"{sellerPurchaseLimit} localizações que " +
                    "conhecia para você.";

                return false;
            }

            bool targetFound =
                selector.TryFindNearestUnknown(
                    seller,
                    knowledge,
                    out RumorRecord? target,
                    out double targetDistance
                );

            if (!targetFound || target == null)
            {
                discoveryService.RequestAdditional(
                    StructureKind.Trader,
                    sellerCenter.X,
                    sellerCenter.Z
                );

                int indexedTraderCount =
                    selector.CountIndexedTraders();

                int knownTraderCount =
                    selector.CountKnownIndexedTraders(
                        knowledge
                    );

                bool searchActive =
                    discoveryService.IsSearching(
                        StructureKind.Trader,
                        sellerCenter.X,
                        sellerCenter.Z
                    );

                error = searchActive
                    ? "O catálogo está simulando worldgen sem " +
                        "salvar chunks até encontrar outro " +
                        "comerciante confirmado. " +
                        $"Verificados={indexedTraderCount} | " +
                        $"Conhecidos={knownTraderCount} | " +
                        $"Peeks ativos={discoveryService.ActivePeekCount} | " +
                        $"Orçamento pendente={discoveryService.PendingPeekBudget}. " +
                        "Tente novamente quando a busca terminar."
                    : "Não há outro comerciante verificado que " +
                        "você ainda não conheça dentro do raio e " +
                        "orçamento configurados. " +
                        $"Verificados={indexedTraderCount} | " +
                        $"Conhecidos={knownTraderCount}.";

                return false;
            }

            bool priceResolved =
                priceResolver.TryResolveTraderLocation(
                    out RumorPrice? price,
                    out string priceError
                );

            if (!priceResolved || price == null)
            {
                error = priceError;
                return false;
            }

            bool targetResolved =
                targetResolver.TryResolveAll(
                    target,
                    out RumorTargetResolution? resolution,
                    out _,
                    out string targetError
                );

            if (!targetResolved || resolution == null)
            {
                error = targetError;
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

            bool waypointAdded =
                RumorWaypointService.TryAddWaypointsTracked(
                    api,
                    player,
                    target,
                    RumorKnowledgeLevel.Exact,
                    resolution,
                    api.World.Rand,
                    out IReadOnlyList<RumorWaypointHandle>
                        waypointHandles,
                    out string waypointError
                );

            if (!waypointAdded)
            {
                return FailAndRefund(
                    player,
                    receipt,
                    waypointError,
                    out error
                );
            }

            if (waypointHandles.Count != 1)
            {
                RumorWaypointService.TryRemoveWaypoints(
                    api,
                    player,
                    GetWaypointGuids(waypointHandles),
                    out _,
                    out _
                );

                return FailAndRefund(
                    player,
                    receipt,
                    "A localização do comerciante gerou uma " +
                    "quantidade inesperada de waypoints.",
                    out error
                );
            }

            if (!knowledge.MarkRevealed(target.Id))
            {
                RumorWaypointService.TryRemoveWaypoints(
                    api,
                    player,
                    GetWaypointGuids(waypointHandles),
                    out _,
                    out _
                );

                return FailAndRefund(
                    player,
                    receipt,
                    "A localização selecionada já era conhecida.",
                    out error
                );
            }

            sellerPurchasesUsed =
                knowledge.RecordPurchaseFromSeller(
                    seller.Id
                );

            RumorWaypointHandle waypoint =
                waypointHandles[0];

            result = new TraderLocationPurchaseResult(
                seller,
                target,
                price,
                waypoint,
                sellerDistance,
                targetDistance,
                sellerPurchasesUsed,
                sellerPurchaseLimit
            );

            logger.Notification(
                "=== Rumor Network: localização de comerciante comprada ==="
            );

            logger.Notification(
                $"Player={player.PlayerUID} | " +
                $"Seller={seller.Id} | " +
                $"Target={target.Id} | " +
                $"Distance={targetDistance:0.0} | " +
                $"Price={price.Description} | " +
                $"SellerQuota={sellerPurchasesUsed}/" +
                $"{sellerPurchaseLimit} | " +
                $"Waypoint={waypoint.Guid}"
            );

            return true;
        }

        private bool FailAndRefund(
            IServerPlayer player,
            RumorPaymentReceipt receipt,
            string failure,
            out string error
        )
        {
            bool refunded =
                paymentService.Refund(
                    player,
                    receipt,
                    out string refundError
                );

            error = refunded
                ? failure +
                    " O pagamento foi devolvido."
                : failure +
                    " " +
                    refundError;

            return false;
        }

        private static IEnumerable<string>
            GetWaypointGuids(
                IReadOnlyList<RumorWaypointHandle>
                    waypointHandles
            )
        {
            foreach (
                RumorWaypointHandle handle
                in waypointHandles
            )
            {
                yield return handle.Guid;
            }
        }
    }
}
