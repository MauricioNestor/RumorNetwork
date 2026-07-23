using System.Collections.Generic;
using RumorNetwork.Catalog;
using RumorNetwork.Rumors;
using Vintagestory.API.Server;

namespace RumorNetwork.Purchases
{
    public sealed class TranslocatorPurchaseService
    {
        private readonly RumorRegistry rumorRegistry;
        private readonly RumorTargetResolver targetResolver;
        private readonly RumorPriceResolver priceResolver;
        private readonly RumorInventoryPaymentService paymentService;
        private readonly VerifiedStructureDiscoveryService discoveryService;
        private readonly ICoreServerAPI api;

        public TranslocatorPurchaseService(
            ICoreServerAPI api,
            RumorRegistry rumorRegistry,
            RumorTargetResolver targetResolver,
            RumorPriceResolver priceResolver,
            RumorInventoryPaymentService paymentService,
            VerifiedStructureDiscoveryService discoveryService
        )
        {
            this.api = api;
            this.rumorRegistry = rumorRegistry;
            this.targetResolver = targetResolver;
            this.priceResolver = priceResolver;
            this.paymentService = paymentService;
            this.discoveryService = discoveryService;
        }

        public bool TryPurchase(
            IServerPlayer player,
            out RumorPurchaseResult? result,
            out string error
        )
        {
            result = null;
            error = string.Empty;

            discoveryService.RequestAdditional(
                StructureKind.Translocator,
                (int)player.Entity.Pos.X,
                (int)player.Entity.Pos.Z
            );

            List<RumorRecord> candidates =
                CreateShuffledTranslocatorCandidates();

            RumorPreparedDelivery? prepared = null;

            foreach (RumorRecord record in candidates)
            {
                bool resolved = targetResolver.TryResolveAll(
                    record,
                    out RumorTargetResolution? resolution,
                    out _,
                    out _
                );

                if (resolved && resolution != null)
                {
                    prepared = new RumorPreparedDelivery(
                        record,
                        RumorKnowledgeLevel.Exact,
                        resolution
                    );

                    break;
                }
            }

            if (prepared == null)
            {
                error = discoveryService.IsWorking
                    ? "Ainda estou procurando um translocador intacto. " +
                        $"Peeks ativos={discoveryService.ActivePeekCount} | " +
                        $"Buscas={discoveryService.ActiveSearchCount}."
                    : "Não conheço nenhum translocador intacto que você ainda não tenha recebido.";

                return false;
            }

            bool priceResolved = priceResolver.TryResolve(
                StructureKind.Translocator,
                RumorKnowledgeLevel.Exact,
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

            bool waypointsAdded =
                RumorWaypointService.TryAddWaypointsTracked(
                    api,
                    player,
                    prepared.Record,
                    RumorKnowledgeLevel.Exact,
                    prepared.Resolution,
                    api.World.Rand,
                    out IReadOnlyList<RumorWaypointHandle> handles,
                    out string waypointError
                );

            if (!waypointsAdded)
            {
                paymentService.Refund(player, receipt, out _);
                error =
                    waypointError +
                    " O pagamento foi devolvido.";

                return false;
            }

            if (!rumorRegistry.TryMarkSold(
                    prepared.Record.Id,
                    RumorKnowledgeLevel.Exact
                ))
            {
                RumorWaypointService.TryRemoveWaypoints(
                    api,
                    player,
                    System.Linq.Enumerable.Select(
                        handles,
                        handle => handle.Guid
                    ),
                    out _,
                    out _
                );

                paymentService.Refund(player, receipt, out _);
                error =
                    "O translocador não pôde ser reservado. " +
                    "O pagamento foi devolvido.";

                return false;
            }

            result = new RumorPurchaseResult(
                prepared.Record,
                price,
                handles
            );

            return true;
        }

        private List<RumorRecord>
            CreateShuffledTranslocatorCandidates()
        {
            List<RumorRecord> candidates = new();

            foreach (RumorRecord record in rumorRegistry.Records)
            {
                if (
                    record.Knowledge ==
                        RumorKnowledgeLevel.NotSold &&
                    record.Kind == StructureKind.Translocator
                )
                {
                    candidates.Add(record);
                }
            }

            for (
                int index = candidates.Count - 1;
                index > 0;
                index--
            )
            {
                int swapIndex = api.World.Rand.Next(index + 1);

                (
                    candidates[index],
                    candidates[swapIndex]
                ) =
                (
                    candidates[swapIndex],
                    candidates[index]
                );
            }

            return candidates;
        }
    }
}
