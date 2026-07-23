using RumorNetwork.Configuration;
using RumorNetwork.Structures;

namespace RumorNetwork.Rumors;

public static class RumorEligibilityPolicy
{
    public static bool IsIndexable(
        StructureKind kind
    )
    {
        return
            kind == StructureKind.Trader ||
            kind == StructureKind.Translocator ||
            IsGeneralRumorEligible(kind);
    }

    public static bool IsGeneralRumorEligible(
        StructureKind kind
    )
    {
        return RumorRuntimeSettings
            .GeneralRumors
            .IsStructureEnabled(kind);
    }

    public static bool IsGeneralRumorEligible(
        RumorRecord record
    )
    {
        if (BetterRuinsRumorPolicy.IsBetterRuins(record))
        {
            return
                IsGeneralRumorEligible(record.Kind) &&
                BetterRuinsRumorPolicy
                    .IsGeneralPoolEligible(record);
        }

        return IsGeneralRumorEligible(record.Kind);
    }

    public static int GetGeneralRumorWeight(
        StructureKind kind
    )
    {
        return RumorRuntimeSettings
            .GeneralRumors
            .GetWeight(kind);
    }

    public static int GetGeneralRumorWeight(
        RumorRecord record
    )
    {
        if (BetterRuinsRumorPolicy.IsBetterRuins(record))
        {
            return BetterRuinsRumorPolicy.GetWeight(record);
        }

        return GetGeneralRumorWeight(record.Kind);
    }

    public static string GetGeneralRumorCategoryKey(
        RumorRecord record
    )
    {
        if (BetterRuinsRumorPolicy.IsBetterRuins(record))
        {
            return BetterRuinsRumorPolicy
                .GetCategoryKey(record);
        }

        return "kind:" + record.Kind;
    }

    public static bool IsEligible(
        StructureKind kind
    )
    {
        return IsIndexable(kind);
    }
}
