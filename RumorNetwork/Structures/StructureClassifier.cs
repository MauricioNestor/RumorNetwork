using System;
using RumorNetwork.Configuration;
using Vintagestory.API.Common;

namespace RumorNetwork.Structures;

public static class StructureClassifier
{
    private const string BetterRuinsPrefix = "betterruins:";

    private const string BetterRuinsUndergroundCode =
        "betterruins:undergroundruins";

    private const string BetterRuinsGatesCode =
        "betterruins:gates";

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

        bool isBetterRuins = code.StartsWith(
            BetterRuinsPrefix,
            StringComparison.OrdinalIgnoreCase
        );

        bool isBetterRuinsUnderground = string.Equals(
            code,
            BetterRuinsUndergroundCode,
            StringComparison.OrdinalIgnoreCase
        );

        bool isBetterRuinsGates = string.Equals(
            code,
            BetterRuinsGatesCode,
            StringComparison.OrdinalIgnoreCase
        );

        bool isBetterRuinsStory =
            isBetterRuins &&
            string.Equals(
                group,
                "storystructure",
                StringComparison.OrdinalIgnoreCase
            );

        if (
            isBetterRuins &&
            TryClassifyConfiguredExactCode(
                code,
                group,
                out StructureKind exactConfiguredKind
            )
        )
        {
            return exactConfiguredKind;
        }

        if (
            isBetterRuinsStory &&
            RumorRuntimeSettings
                .BetterRuins
                .ExcludeStoryStructures
        )
        {
            return StructureKind.StoryStructure;
        }

        if (isBetterRuinsGates)
        {
            return StructureKind.Gate;
        }

        if (
            isBetterRuinsUnderground &&
            RumorRuntimeSettings
                .StructureClassification
                .UseBuiltInRules
        )
        {
            return StructureKind.UndergroundRuin;
        }

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
        if (isBetterRuins)
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

    private static bool TryClassifyConfiguredExactCode(
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
                string.IsNullOrEmpty(rule.CodeExact) ||
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
