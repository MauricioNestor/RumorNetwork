using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RumorNetwork.Configuration
{
    public sealed partial class RumorNetworkConfig
    {
        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public BetterRuinsRumorConfig BetterRuins =
            BetterRuinsRumorConfig.CreateDefault();

        private void NormalizeBetterRuins()
        {
            BetterRuins ??=
                BetterRuinsRumorConfig.CreateDefault();

            BetterRuins.Normalize();
        }
    }

    public sealed class BetterRuinsRumorConfig
    {
        public bool Enabled = true;

        public bool IncludeInGeneralPool = false;

        public bool IncludeSafeStructuresInGeneralPoolWhenDisabled =
            true;

        public bool ExcludeStoryStructures = true;

        public int DefaultWeight = 3;

        public bool DefaultGeneralPoolEligible = true;

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public RumorPriceConfig ExactPrice =
            RumorPriceConfig.Single(
                "game:gear-rusty",
                5
            );

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public List<BetterRuinsCategoryRuleConfig>
            Categories = CreateDefaultCategories();

        public static BetterRuinsRumorConfig CreateDefault()
        {
            return new BetterRuinsRumorConfig();
        }

        public BetterRuinsCategoryRuleConfig? FindCategory(
            string sourceCode,
            string sourceGroup
        )
        {
            foreach (
                BetterRuinsCategoryRuleConfig category
                in Categories
            )
            {
                if (category.Matches(
                        sourceCode,
                        sourceGroup
                    ))
                {
                    return category;
                }
            }

            return null;
        }

        public void Normalize()
        {
            if (DefaultWeight <= 0)
            {
                DefaultWeight = 1;
            }

            ExactPrice ??=
                RumorPriceConfig.Single(
                    "game:gear-rusty",
                    5
                );

            ExactPrice.Normalize();

            Categories ??= CreateDefaultCategories();
            Categories.RemoveAll(category => category == null);

            foreach (
                BetterRuinsCategoryRuleConfig category
                in Categories
            )
            {
                category.Normalize();
            }
        }

        private static List<BetterRuinsCategoryRuleConfig>
            CreateDefaultCategories()
        {
            return new List<BetterRuinsCategoryRuleConfig>
            {
                Category(
                    "Megastructures",
                    1,
                    false,
                    codeContains: "megastructure"
                ),
                Category(
                    "Megastructures",
                    1,
                    false,
                    groupContains: "megastructure"
                ),
                Category(
                    "LargeRuins",
                    1,
                    false,
                    codeContains: "largeruin"
                ),
                Category(
                    "LargeRuins",
                    1,
                    false,
                    groupContains: "largeruin"
                ),
                Category(
                    "Rank5",
                    1,
                    false,
                    groupExact: "rank-5"
                ),
                Category(
                    "Rank4",
                    2,
                    false,
                    groupExact: "rank-4"
                ),
                Category(
                    "Rare",
                    1,
                    false,
                    groupContains: "rare"
                ),
                Category(
                    "Rank3",
                    3,
                    false,
                    groupExact: "rank-3"
                ),
                Category(
                    "Rank2",
                    4,
                    false,
                    groupExact: "rank-2"
                ),
                Category(
                    "Rank1",
                    5,
                    true,
                    groupExact: "rank-1"
                ),
                Category(
                    "Abundant",
                    6,
                    true,
                    groupContains: "abundant"
                ),
                Category(
                    "VeryCommon",
                    6,
                    true,
                    groupContains: "verycommon"
                ),
                Category(
                    "Uncommon",
                    3,
                    false,
                    groupContains: "uncommon"
                ),
                Category(
                    "Common",
                    5,
                    true,
                    groupContains: "common"
                )
            };
        }

        private static BetterRuinsCategoryRuleConfig Category(
            string name,
            int weight,
            bool generalPoolEligible,
            string codeContains = "",
            string groupExact = "",
            string groupContains = ""
        )
        {
            return new BetterRuinsCategoryRuleConfig
            {
                Name = name,
                Weight = weight,
                GeneralPoolEligible = generalPoolEligible,
                CodeContains = codeContains,
                GroupExact = groupExact,
                GroupContains = groupContains
            };
        }
    }

    public sealed class BetterRuinsCategoryRuleConfig
    {
        public bool Enabled = true;

        public string Name = "Regular";

        public int Weight = 1;

        public bool GeneralPoolEligible = true;

        public string CodeExact = string.Empty;

        public string CodeContains = string.Empty;

        public string GroupExact = string.Empty;

        public string GroupContains = string.Empty;

        public bool Matches(
            string sourceCode,
            string sourceGroup
        )
        {
            if (!HasMatcher())
            {
                return false;
            }

            return
                MatchesExact(CodeExact, sourceCode) &&
                MatchesContains(CodeContains, sourceCode) &&
                MatchesExact(GroupExact, sourceGroup) &&
                MatchesContains(GroupContains, sourceGroup);
        }

        public void Normalize()
        {
            Name = NormalizeText(Name, "Regular");

            if (Weight <= 0)
            {
                Weight = 1;
            }

            CodeExact = NormalizeText(CodeExact);
            CodeContains = NormalizeText(CodeContains);
            GroupExact = NormalizeText(GroupExact);
            GroupContains = NormalizeText(GroupContains);
        }

        private bool HasMatcher()
        {
            return
                !string.IsNullOrEmpty(CodeExact) ||
                !string.IsNullOrEmpty(CodeContains) ||
                !string.IsNullOrEmpty(GroupExact) ||
                !string.IsNullOrEmpty(GroupContains);
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

        private static string NormalizeText(
            string? value,
            string fallback = ""
        )
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }
    }
}
