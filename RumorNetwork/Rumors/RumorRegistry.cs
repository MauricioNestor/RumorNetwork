using System;
using System.Collections.Generic;
using RumorNetwork.Structures;

namespace RumorNetwork.Rumors;

public sealed class RumorRegistry
{
    private readonly Dictionary<string, RumorRecord> records =
        new(StringComparer.Ordinal);

    public int Count => records.Count;

    public IEnumerable<RumorRecord> Records =>
        records.Values;

    public int Merge(
        IEnumerable<RumorSite> sites
    )
    {
        int addedCount = 0;

        foreach (RumorSite site in sites)
        {
            // Vanilla "gates" are translocator structures. They are
            // admitted only after a real BlockStaticTranslocator is found
            // and are then stored as StructureKind.Translocator.
            if (site.Kind == StructureKind.Gate)
            {
                continue;
            }

            if (records.ContainsKey(site.Id))
            {
                continue;
            }

            RumorRecord record =
                RumorRecord.FromSite(site);

            records.Add(
                record.Id,
                record
            );

            addedCount++;
        }

        return addedCount;
    }

    public bool TryGet(
        string id,
        out RumorRecord record
    )
    {
        return records.TryGetValue(
            id,
            out record
        );
    }

    public int CountByKnowledge(
        RumorKnowledgeLevel knowledge
    )
    {
        int count = 0;

        foreach (RumorRecord record in records.Values)
        {
            if (record.Knowledge == knowledge)
            {
                count++;
            }
        }

        return count;
    }

    public int CountByKind(
        StructureKind kind
    )
    {
        int count = 0;

        foreach (RumorRecord record in records.Values)
        {
            if (record.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    public int RemoveByKind(
        params StructureKind[] kinds
    )
    {
        if (kinds == null || kinds.Length == 0)
        {
            return 0;
        }

        HashSet<StructureKind> selectedKinds =
            new(kinds);

        List<string> idsToRemove = new();

        foreach (
            KeyValuePair<string, RumorRecord> pair
            in records
        )
        {
            if (selectedKinds.Contains(pair.Value.Kind))
            {
                idsToRemove.Add(pair.Key);
            }
        }

        foreach (string id in idsToRemove)
        {
            records.Remove(id);
        }

        return idsToRemove.Count;
    }

    public RumorRegistrySaveData Export()
    {
        return new RumorRegistrySaveData
        {
            Version = 1,
            Records = new List<RumorRecord>(
                records.Values
            )
        };
    }

    public void Import(
        RumorRegistrySaveData saveData
    )
    {
        records.Clear();

        if (saveData?.Records == null)
        {
            return;
        }

        foreach (RumorRecord record in saveData.Records)
        {
            if (
                string.IsNullOrWhiteSpace(record.Id) ||
                record.Kind == StructureKind.Gate
            )
            {
                continue;
            }

            records[record.Id] = record;
        }
    }

    public List<RumorRecord>
        CreateShuffledNotSoldCandidates(
            Random random
        )
    {
        List<RumorRecord> candidates = new();

        foreach (RumorRecord candidate in records.Values)
        {
            if (
                candidate.Knowledge
                == RumorKnowledgeLevel.NotSold &&
                RumorEligibilityPolicy
                    .IsGeneralRumorEligible(
                        candidate.Kind
                    )
            )
            {
                candidates.Add(candidate);
            }
        }

        for (
            int index = candidates.Count - 1;
            index > 0;
            index--
        )
        {
            int swapIndex =
                random.Next(index + 1);

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

    public bool TryPickRandomNotSold(
         Random random,
         out RumorRecord? record
    )
    {
        List<RumorRecord> candidates =
            CreateShuffledNotSoldCandidates(
                random
            );

        if (candidates.Count == 0)
        {
            record = null;
            return false;
        }

        record = candidates[0];
        return true;
    }

    public bool TryMarkSold(
        string id,
        RumorKnowledgeLevel knowledge
    )
    {
        if (knowledge == RumorKnowledgeLevel.NotSold)
        {
            return false;
        }

        if (!records.TryGetValue(
                id,
                out RumorRecord? record
            ))
        {
            return false;
        }

        if (
            record.Knowledge
            != RumorKnowledgeLevel.NotSold
        )
        {
            return false;
        }

        record.Knowledge = knowledge;
        return true;
    }
}
