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

    public static int GetGeneralRumorWeight(
        StructureKind kind
    )
    {
        return RumorRuntimeSettings
            .GeneralRumors
            .GetWeight(kind);
    }

    public static bool IsEligible(
        StructureKind kind
    )
    {
        return IsIndexable(kind);
    }
}
