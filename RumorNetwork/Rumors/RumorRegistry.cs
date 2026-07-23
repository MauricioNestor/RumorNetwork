using System;
using System.Collections.Generic;
using RumorNetwork.Configuration;
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

    public int RemoveNear(
        StructureKind kind,
        int x,
        int y,
        int z,
        int radius
    )
    {
        long radiusSquared = (long)radius * radius;
        List<string> idsToRemove = new();

        foreach (
            KeyValuePair<string, RumorRecord> pair
            in records
        )
        {
            RumorRecord record = pair.Value;

            if (record.Kind != kind)
            {
                continue;
            }

            var center = record.CreateLocation().Center;
            long deltaX = center.X - x;
            long deltaY = center.Y - y;
            long deltaZ = center.Z - z;
            long distanceSquared =
                deltaX * deltaX +
                deltaY * deltaY +
                deltaZ * deltaZ;

            if (distanceSquared <= radiusSquared)
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

    public List<RumorRecord> CreateShuffledCandidates(
        Random random,
        RumorKnowledgeLevel requestedKnowledge
    )
    {
        List<WeightedCandidate> candidates = new();

        foreach (RumorRecord candidate in records.Values)
        {
            if (
                !CanSellAtKnowledge(
                    candidate,
                    requestedKnowledge
                ) ||
                !RumorEligibilityPolicy
                    .IsGeneralRumorEligible(candidate.Kind)
            )
            {
                continue;
            }

            int weight = RumorEligibilityPolicy
                .GetGeneralRumorWeight(candidate.Kind);

            if (weight <= 0)
            {
                continue;
            }

            candidates.Add(
                new WeightedCandidate(
                    candidate,
                    weight
                )
            );
        }

        return CreateWeightedOrder(
            candidates,
            random
        );
    }

    public List<RumorRecord>
        CreateShuffledNotSoldCandidates(
            Random random
        )
    {
        return CreateShuffledNotSoldCandidates(
            random,
            null
        );
    }

    public List<RumorRecord>
        CreateShuffledNotSoldCandidates(
            Random random,
            StructureKind? requiredKind
        )
    {
        List<WeightedCandidate> candidates = new();

        foreach (RumorRecord candidate in records.Values)
        {
            if (
                candidate.Knowledge !=
                    RumorKnowledgeLevel.NotSold ||
                !RumorEligibilityPolicy
                    .IsGeneralRumorEligible(candidate.Kind) ||
                (
                    requiredKind.HasValue &&
                    candidate.Kind != requiredKind.Value
                )
            )
            {
                continue;
            }

            int weight = RumorEligibilityPolicy
                .GetGeneralRumorWeight(candidate.Kind);

            if (weight <= 0)
            {
                continue;
            }

            candidates.Add(
                new WeightedCandidate(
                    candidate,
                    weight
                )
            );
        }

        return CreateWeightedOrder(
            candidates,
            random
        );
    }

    public bool TryPickRandomNotSold(
        Random random,
        out RumorRecord? record
    )
    {
        List<RumorRecord> candidates =
            CreateShuffledNotSoldCandidates(random);

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

        if (record.Knowledge == RumorKnowledgeLevel.NotSold)
        {
            record.Knowledge = knowledge;
            return true;
        }

        if (
            RumorRuntimeSettings
                .GeneralRumors
                .AllowApproximateToExactUpgrade &&
            record.Knowledge ==
                RumorKnowledgeLevel.Approximate &&
            knowledge == RumorKnowledgeLevel.Exact
        )
        {
            record.Knowledge = RumorKnowledgeLevel.Exact;
            return true;
        }

        return false;
    }

    private static bool CanSellAtKnowledge(
        RumorRecord record,
        RumorKnowledgeLevel requestedKnowledge
    )
    {
        if (record.Knowledge == RumorKnowledgeLevel.NotSold)
        {
            return true;
        }

        return
            RumorRuntimeSettings
                .GeneralRumors
                .AllowApproximateToExactUpgrade &&
            requestedKnowledge == RumorKnowledgeLevel.Exact &&
            record.Knowledge == RumorKnowledgeLevel.Approximate;
    }

    private static List<RumorRecord> CreateWeightedOrder(
        List<WeightedCandidate> candidates,
        Random random
    )
    {
        List<RumorRecord> ordered = new();
        List<WeightedCandidate> remaining = new(candidates);

        while (remaining.Count > 0)
        {
            long totalWeight = 0;

            foreach (WeightedCandidate candidate in remaining)
            {
                totalWeight += candidate.Weight;
            }

            double roll = random.NextDouble() * totalWeight;
            long cumulative = 0;
            int selectedIndex = remaining.Count - 1;

            for (int index = 0; index < remaining.Count; index++)
            {
                cumulative += remaining[index].Weight;

                if (roll < cumulative)
                {
                    selectedIndex = index;
                    break;
                }
            }

            ordered.Add(
                remaining[selectedIndex].Record
            );

            remaining.RemoveAt(selectedIndex);
        }

        return ordered;
    }

    private sealed class WeightedCandidate
    {
        public RumorRecord Record { get; }

        public int Weight { get; }

        public WeightedCandidate(
            RumorRecord record,
            int weight
        )
        {
            Record = record;
            Weight = weight;
        }
    }
}
