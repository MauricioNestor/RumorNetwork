using System;
using RumorNetwork.Configuration;
using Vintagestory.API.Common;

namespace RumorNetwork.Structures;

public static class StructureClassifier
{
    public static StructureKind Classify(
        GeneratedStructure structure
    )
    {
        string code =
            structure.Code?.ToLowerInvariant() ??
            string.Empty;

        string group =
            structure.Group?.ToLowerInvariant() ??
            string.Empty;

        if (TryClassifyConfigured(
                code,
                group,
                out StructureKind configuredKind
            ))
        {
            return configuredKind;
        }

        if (!RumorRuntimeSettings
                .StructureClassification
                .UseBuiltInRules)
        {
            return StructureKind.Unknown;
        }

        // More specific built-in rules first.
        if (string.Equals(
                code,
                "betterruins:undergroundruins",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return StructureKind.UndergroundRuin;
        }

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

    private static bool TryClassifyConfigured(
        string code,
        string group,
        out StructureKind kind
    )
    {
        foreach (
            StructureClassificationRuleConfig rule
            in RumorRuntimeSettings
                .StructureClassification
                .Rules
        )
        {
            if (
                !rule.Enabled ||
                !HasMatcher(rule) ||
                !Matches(rule, code, group) ||
                !Enum.TryParse(
                    rule.Kind,
                    true,
                    out kind
                )
            )
            {
                continue;
            }

            return true;
        }

        kind = StructureKind.Unknown;
        return false;
    }

    private static bool HasMatcher(
        StructureClassificationRuleConfig rule
    )
    {
        return
            !string.IsNullOrEmpty(rule.CodeExact) ||
            !string.IsNullOrEmpty(rule.CodePrefix) ||
            !string.IsNullOrEmpty(rule.CodeContains) ||
            !string.IsNullOrEmpty(rule.GroupExact) ||
            !string.IsNullOrEmpty(rule.GroupPrefix);
    }

    private static bool Matches(
        StructureClassificationRuleConfig rule,
        string code,
        string group
    )
    {
        return
            MatchesExact(rule.CodeExact, code) &&
            MatchesPrefix(rule.CodePrefix, code) &&
            MatchesContains(rule.CodeContains, code) &&
            MatchesExact(rule.GroupExact, group) &&
            MatchesPrefix(rule.GroupPrefix, group);
    }

    private static bool MatchesExact(
        string configured,
        string actual
    )
    {
        return
            string.IsNullOrEmpty(configured) ||
            string.Equals(
                configured,
                actual,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static bool MatchesPrefix(
        string configured,
        string actual
    )
    {
        return
            string.IsNullOrEmpty(configured) ||
            actual.StartsWith(
                configured,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static bool MatchesContains(
        string configured,
        string actual
    )
    {
        return
            string.IsNullOrEmpty(configured) ||
            actual.Contains(
                configured,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
