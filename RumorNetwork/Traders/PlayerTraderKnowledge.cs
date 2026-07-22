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

        public int RevealedCount =>
            revealedTraderIds.Count;

        public int VisitedCount =>
            visitedTraderIds.Count;

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

        public PlayerTraderKnowledgeRecord Export(
            string playerUid
        )
        {
            return new PlayerTraderKnowledgeRecord
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

            return knowledge;
        }
    }
}
