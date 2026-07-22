using System;
using System.Collections.Generic;

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
            if (string.IsNullOrWhiteSpace(record.Id))
            {
                continue;
            }

            records[record.Id] = record;
        }
    }

    public bool TryPickRandomNotSold(
         Random random,
         out RumorRecord? record
    )
    {
        List<RumorRecord> candidates = new();

        foreach (RumorRecord candidate in records.Values)
        {
            if (
                candidate.Knowledge
                == RumorKnowledgeLevel.NotSold
            )
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            record = null;
            return false;
        }

        record = candidates[
            random.Next(candidates.Count)
        ];

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