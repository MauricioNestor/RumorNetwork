using System;
using RumorNetwork.Configuration;
using RumorNetwork.Structures;

namespace RumorNetwork.Rumors;

public static class BetterRuinsRumorPolicy
{
    public const string ModId = "betterruins";

    private const string DomainPrefix = "betterruins:";
    private const string StoryGroup = "storystructure";
    private const string GatesCode = "betterruins:gates";

    public static bool DedicatedAvailable =>
        RumorRuntimeSettings.BetterRuinsInstalled &&
        RumorRuntimeSettings.BetterRuins.Enabled;

    public static bool IsBetterRuins(
        RumorRecord record
    )
    {
        return IsBetterRuins(record.SourceCode);
    }

    public static bool IsBetterRuins(
        string sourceCode
    )
    {
        return sourceCode?.StartsWith(
            DomainPrefix,
            StringComparison.OrdinalIgnoreCase
        ) == true;
    }

    public static bool IsDedicatedEligible(
        RumorRecord record
    )
    {
        if (
            !DedicatedAvailable ||
            !IsBetterRuins(record) ||
            IsExcludedStructure(record)
        )
        {
            return false;
        }

        BetterRuinsCategoryRuleConfig? category =
            FindCategory(record);

        return category?.Enabled ?? true;
    }

    public static bool IsGeneralPoolEligible(
        RumorRecord record
    )
    {
        BetterRuinsRumorConfig config =
            RumorRuntimeSettings.BetterRuins;

        if (
            !RumorRuntimeSettings.BetterRuinsInstalled ||
            !IsBetterRuins(record) ||
            IsExcludedStructure(record)
        )
        {
            return false;
        }

        BetterRuinsCategoryRuleConfig? category =
            FindCategory(record);

        if (category?.Enabled == false)
        {
            return false;
        }

        if (config.Enabled)
        {
            return config.IncludeInGeneralPool;
        }

        return
            config.IncludeSafeStructuresInGeneralPoolWhenDisabled &&
            (
                category?.GeneralPoolEligible ??
                config.DefaultGeneralPoolEligible
            );
    }

    public static int GetWeight(
        RumorRecord record
    )
    {
        BetterRuinsCategoryRuleConfig? category =
            FindCategory(record);

        return Math.Max(
            1,
            category?.Weight ??
                RumorRuntimeSettings.BetterRuins.DefaultWeight
        );
    }

    public static string GetCategoryKey(
        RumorRecord record
    )
    {
        BetterRuinsCategoryRuleConfig? category =
            FindCategory(record);

        return "betterruins:" +
            (category?.Name ?? "Regular");
    }

    public static bool IsStoryStructure(
        RumorRecord record
    )
    {
        return string.Equals(
            record.SourceGroup,
            StoryGroup,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static bool IsExcludedStructure(
        RumorRecord record
    )
    {
        BetterRuinsRumorConfig config =
            RumorRuntimeSettings.BetterRuins;

        if (
            config.ExcludeStoryStructures &&
            IsStoryStructure(record)
        )
        {
            return true;
        }

        return
            record.Kind == StructureKind.Translocator ||
            record.Kind == StructureKind.Gate ||
            string.Equals(
                record.SourceCode,
                GatesCode,
                StringComparison.OrdinalIgnoreCase
            ) ||
            record.SourceCode.Contains(
                "translocator",
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static BetterRuinsCategoryRuleConfig?
        FindCategory(
            RumorRecord record
        )
    {
        return RumorRuntimeSettings
            .BetterRuins
            .FindCategory(
                record.SourceCode ?? string.Empty,
                record.SourceGroup ?? string.Empty
            );
    }
}
