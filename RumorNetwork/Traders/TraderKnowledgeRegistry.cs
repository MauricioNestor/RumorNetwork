using System;
using System.Collections.Generic;

namespace RumorNetwork.Traders
{
    public sealed class TraderKnowledgeRegistry
    {
        private readonly Dictionary<
            string,
            PlayerTraderKnowledge
        > knowledgeByPlayer =
            new(StringComparer.Ordinal);

        public PlayerTraderKnowledge GetOrCreate(
            string playerUid
        )
        {
            if (!knowledgeByPlayer.TryGetValue(
                    playerUid,
                    out PlayerTraderKnowledge? knowledge
                ))
            {
                knowledge = new PlayerTraderKnowledge();

                knowledgeByPlayer.Add(
                    playerUid,
                    knowledge
                );
            }

            return knowledge;
        }

        public bool Clear(
            string playerUid,
            out int revealedCount,
            out int visitedCount
        )
        {
            revealedCount = 0;
            visitedCount = 0;

            if (!knowledgeByPlayer.TryGetValue(
                    playerUid,
                    out PlayerTraderKnowledge? knowledge
                ))
            {
                return false;
            }

            revealedCount = knowledge.RevealedCount;
            visitedCount = knowledge.VisitedCount;

            knowledgeByPlayer.Remove(playerUid);
            return true;
        }

        public TraderKnowledgeSaveData Export()
        {
            TraderKnowledgeSaveData saveData = new();

            foreach (
                KeyValuePair<
                    string,
                    PlayerTraderKnowledge
                > pair
                in knowledgeByPlayer
            )
            {
                saveData.Players.Add(
                    pair.Value.Export(pair.Key)
                );
            }

            return saveData;
        }

        public void Import(
            TraderKnowledgeSaveData saveData
        )
        {
            knowledgeByPlayer.Clear();

            if (saveData?.Players == null)
            {
                return;
            }

            foreach (
                PlayerTraderKnowledgeRecord record
                in saveData.Players
            )
            {
                if (string.IsNullOrWhiteSpace(
                        record.PlayerUid
                    ))
                {
                    continue;
                }

                knowledgeByPlayer[record.PlayerUid] =
                    PlayerTraderKnowledge.Import(record);
            }
        }
    }
}
