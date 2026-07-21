using Vintagestory.API.Common;

namespace RumorNetwork.Structures;

public static class StructureClassifier
{
    public static StructureKind Classify(GeneratedStructure structure)
    {
        string code = structure.Code?.ToLowerInvariant() ?? string.Empty;
        string group = structure.Group?.ToLowerInvariant() ?? string.Empty;

        // Regras mais específicas primeiro.
        if (code.StartsWith("betterruins:"))
        {
            return StructureKind.BetterRuin;
        }

        if (group == "trader" || code.Contains("/trader-"))
        {
            return StructureKind.Trader;
        }

        if (code.Contains("translocator"))
        {
            return StructureKind.Translocator;
        }

        if (code.Contains("/undergroundruin"))
        {
            return StructureKind.UndergroundRuin;
        }

        if (code.Contains(" vugs"))
        {
            return StructureKind.Vug;
        }

        if (
            code.Contains("-village") ||
            code.Contains("-town") ||
            group.StartsWith("villages-")
            )
        {
            return StructureKind.VillagePart;
        }

        if (code.Contains("/buriedtreasurechest"))
        {
            return StructureKind.BuriedTreasure;
        }

        if (code.Contains("/gates"))
        {
            return StructureKind.Gate;
        }

        if (code.Contains("/lakes"))
        {
            return StructureKind.UndergroundLake;
        }
        if (
            code.Contains("/surrfaceruins") ||
            code.Contains("/surfaceruins")
        )
        {
            return StructureKind.SurfaceRuin;
        }

        if (
            group == "storystructure" ||
            code.Contains(":game:story/")
        )
        {
            return StructureKind.StoryStructure;
        }

        return StructureKind.Unknown;
    }
}