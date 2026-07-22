using RumorNetwork.Structures;

namespace RumorNetwork.Rumors;

public static class RumorEligibilityPolicy
{
    public static bool IsIndexable(
        StructureKind kind
    )
    {
        return kind is
            StructureKind.Trader or
            StructureKind.UndergroundRuin or
            StructureKind.BetterRuin or
            StructureKind.SurfaceRuin or
            StructureKind.RuinedVillage or
            StructureKind.Translocator;
    }

    public static bool IsGeneralRumorEligible(
        StructureKind kind
    )
    {
        return kind is
            StructureKind.UndergroundRuin or
            StructureKind.BetterRuin or
            StructureKind.SurfaceRuin or
            StructureKind.RuinedVillage or
            StructureKind.Translocator;
    }

    public static bool IsEligible(
        StructureKind kind
    )
    {
        return IsIndexable(kind);
    }
}
