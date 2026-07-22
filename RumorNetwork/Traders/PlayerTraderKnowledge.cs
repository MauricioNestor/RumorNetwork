using System;
using System.Collections.Generic;

namespace RumorNetwork.Traders
{
    public sealed class PlayerTraderKnowledge
    {
        private readonly HashSet<string>
            revealedTraderIds =
                new(StringComparer.Ordinal);

        private readonly HashSet<string>
            visitedTraderIds =
                new(StringComparer.Ordinal);

        private readonly Dictionary<string, int>
            purchasesBySeller =
                new(StringComparer.Ordinal);

        public int RevealedCount =>
            revealedTraderIds.Count;

        public int VisitedCount =>
            visitedTraderIds.Count;

        public int TotalTraderLocationPurchases
        {
            get
            {
                int total = 0;

                foreach (
                    int purchaseCount
                    in purchasesBySeller.Values
                )
                {
                    total += purchaseCount;
                }

                return total;
            }
        }

        public bool IsKnown(
            string traderId
        )
        {
            return
                revealedTraderIds.Contains(traderId) ||
                visitedTraderIds.Contains(traderId);
        }

        public bool MarkRevealed(
            string traderId
        )
        {
            if (visitedTraderIds.Contains(traderId))
            {
                return false;
            }

            return revealedTraderIds.Add(traderId);
        }

        public bool MarkVisited(
            string traderId
        )
        {
            bool removedRevealed =
                revealedTraderIds.Remove(traderId);

            bool addedVisited =
                visitedTraderIds.Add(traderId);

            return removedRevealed || addedVisited;
        }

        public int GetPurchasesFromSeller(
            string sellerTraderId
        )
        {
            return purchasesBySeller.TryGetValue(
                sellerTraderId,
                out int purchaseCount
            )
                ? purchaseCount
                : 0;
        }

        public bool CanPurchaseFromSeller(
            string sellerTraderId,
            int maximumPurchases
        )
        {
            return
                GetPurchasesFromSeller(sellerTraderId) <
                maximumPurchases;
        }

        public int RecordPurchaseFromSeller(
            string sellerTraderId
        )
        {
            int purchaseCount =
                GetPurchasesFromSeller(sellerTraderId) + 1;

            purchasesBySeller[sellerTraderId] =
                purchaseCount;

            return purchaseCount;
        }

        public PlayerTraderKnowledgeRecord Export(
            string playerUid
        )
        {
            PlayerTraderKnowledgeRecord record = new()
            {
                PlayerUid = playerUid,
                RevealedTraderIds =
                    new List<string>(
                        revealedTraderIds
                    ),
                VisitedTraderIds =
                    new List<string>(
                        visitedTraderIds
                    )
            };

            foreach (
                KeyValuePair<string, int> pair
                in purchasesBySeller
            )
            {
                record.SellerPurchases.Add(
                    new TraderSellerPurchaseRecord
                    {
                        SellerTraderId = pair.Key,
                        PurchaseCount = pair.Value
                    }
                );
            }

            return record;
        }

        public static PlayerTraderKnowledge Import(
            PlayerTraderKnowledgeRecord record
        )
        {
            PlayerTraderKnowledge knowledge = new();

            if (record.RevealedTraderIds != null)
            {
                foreach (
                    string traderId
                    in record.RevealedTraderIds
                )
                {
                    if (!string.IsNullOrWhiteSpace(traderId))
                    {
                        knowledge.revealedTraderIds.Add(
                            traderId
                        );
                    }
                }
            }

            if (record.VisitedTraderIds != null)
            {
                foreach (
                    string traderId
                    in record.VisitedTraderIds
                )
                {
                    if (string.IsNullOrWhiteSpace(traderId))
                    {
                        continue;
                    }

                    knowledge.revealedTraderIds.Remove(
                        traderId
                    );

                    knowledge.visitedTraderIds.Add(
                        traderId
                    );
                }
            }

            if (record.SellerPurchases != null)
            {
                foreach (
                    TraderSellerPurchaseRecord purchase
                    in record.SellerPurchases
                )
                {
                    if (
                        string.IsNullOrWhiteSpace(
                            purchase.SellerTraderId
                        ) ||
                        purchase.PurchaseCount <= 0
                    )
                    {
                        continue;
                    }

                    knowledge.purchasesBySeller[
                        purchase.SellerTraderId
                    ] = purchase.PurchaseCount;
                }
            }

            return knowledge;
        }
    }
}
