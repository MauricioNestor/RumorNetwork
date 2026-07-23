using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RumorNetwork.Rumors;

namespace RumorNetwork.Configuration
{
    public sealed partial class RumorNetworkConfig
    {
        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public GeneralRumorConfig GeneralRumors =
            GeneralRumorConfig.CreateDefault();

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public StructureClassificationConfig
            StructureClassification =
                StructureClassificationConfig.CreateDefault();

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public RumorWaypointConfig Waypoints =
            RumorWaypointConfig.CreateDefault();

        private void NormalizeAdvanced(
            int sourceVersion
        )
        {
            if (sourceVersion < 5)
            {
                GeneralRumors =
                    GeneralRumorConfig.CreateDefault();

                StructureClassification =
                    StructureClassificationConfig.CreateDefault();

                Waypoints =
                    RumorWaypointConfig.CreateDefault();
            }

            GeneralRumors ??=
                GeneralRumorConfig.CreateDefault();

            StructureClassification ??=
                StructureClassificationConfig.CreateDefault();

            Waypoints ??=
                RumorWaypointConfig.CreateDefault();

            GeneralRumors.Normalize();
            StructureClassification.Normalize();
            Waypoints.Normalize();
        }
    }

    public sealed class GeneralRumorConfig
    {
        public bool Enabled = true;

        public bool ApproximateEnabled = true;

        public bool ExactEnabled = true;

        public bool TranslocatorEnabled = true;

        public bool AllowApproximateToExactUpgrade = false;

        public int LocalRegionSearchRadius = 1;

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public Dictionary<string, GeneralRumorKindConfig>
            Structures = new();

        public static GeneralRumorConfig CreateDefault()
        {
            GeneralRumorConfig config = new();

            config.Structures[
                StructureKind.UndergroundRuin.ToString()
            ] = GeneralRumorKindConfig.EnabledWithWeight(1);

            config.Structures[
                StructureKind.BetterRuin.ToString()
            ] = GeneralRumorKindConfig.EnabledWithWeight(1);

            config.Structures[
                StructureKind.SurfaceRuin.ToString()
            ] = GeneralRumorKindConfig.EnabledWithWeight(1);

            config.Structures[
                StructureKind.RuinedVillage.ToString()
            ] = GeneralRumorKindConfig.EnabledWithWeight(1);

            return config;
        }

        public bool IsKnowledgeEnabled(
            RumorKnowledgeLevel knowledge
        )
        {
            if (!Enabled)
            {
                return false;
            }

            return knowledge switch
            {
                RumorKnowledgeLevel.Approximate =>
                    ApproximateEnabled,

                RumorKnowledgeLevel.Exact =>
                    ExactEnabled,

                _ => false
            };
        }

        public bool IsStructureEnabled(
            StructureKind kind
        )
        {
            return
                Enabled &&
                TryGetStructure(kind, out GeneralRumorKindConfig? rule) &&
                rule != null &&
                rule.Enabled &&
                rule.Weight > 0;
        }

        public int GetWeight(
            StructureKind kind
        )
        {
            if (!IsStructureEnabled(kind))
            {
                return 0;
            }

            TryGetStructure(
                kind,
                out GeneralRumorKindConfig? rule
            );

            return rule?.Weight ?? 0;
        }

        public void Normalize()
        {
            if (LocalRegionSearchRadius < 0)
            {
                LocalRegionSearchRadius = 0;
            }

            if (LocalRegionSearchRadius > 16)
            {
                LocalRegionSearchRadius = 16;
            }

            Structures ??= new();

            foreach (
                GeneralRumorKindConfig rule
                in Structures.Values
            )
            {
                rule?.Normalize();
            }
        }

        private bool TryGetStructure(
            StructureKind kind,
            out GeneralRumorKindConfig? rule
        )
        {
            string requestedKey = kind.ToString();

            foreach (
                KeyValuePair<string, GeneralRumorKindConfig> pair
                in Structures
            )
            {
                if (string.Equals(
                        pair.Key,
                        requestedKey,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    rule = pair.Value;
                    return true;
                }
            }

            rule = null;
            return false;
        }
    }

    public sealed class GeneralRumorKindConfig
    {
        public bool Enabled = true;

        public int Weight = 1;

        public static GeneralRumorKindConfig EnabledWithWeight(
            int weight
        )
        {
            return new GeneralRumorKindConfig
            {
                Enabled = true,
                Weight = weight
            };
        }

        public void Normalize()
        {
            if (Weight < 0)
            {
                Weight = 0;
            }
        }
    }

    public sealed class StructureClassificationConfig
    {
        public bool UseBuiltInRules = true;

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public List<StructureClassificationRuleConfig>
            Rules = new();

        public static StructureClassificationConfig
            CreateDefault()
        {
            StructureClassificationConfig config = new();

            // Keep BetterRuins visible in the generated config while the
            // built-in classifier remains as a compatibility fallback.
            config.Rules.Add(
                new StructureClassificationRuleConfig
                {
                    CodePrefix = "betterruins:",
                    Kind = StructureKind.BetterRuin.ToString()
                }
            );

            return config;
        }

        public void Normalize()
        {
            Rules ??= new();
            Rules.RemoveAll(rule => rule == null);

            foreach (
                StructureClassificationRuleConfig rule
                in Rules
            )
            {
                rule.Normalize();
            }
        }
    }

    public sealed class StructureClassificationRuleConfig
    {
        public bool Enabled = true;

        public string Kind = StructureKind.Unknown.ToString();

        public string CodeExact = string.Empty;

        public string CodePrefix = string.Empty;

        public string CodeContains = string.Empty;

        public string GroupExact = string.Empty;

        public string GroupPrefix = string.Empty;

        public void Normalize()
        {
            Kind = NormalizeText(
                Kind,
                StructureKind.Unknown.ToString()
            );

            CodeExact = NormalizeText(CodeExact);
            CodePrefix = NormalizeText(CodePrefix);
            CodeContains = NormalizeText(CodeContains);
            GroupExact = NormalizeText(GroupExact);
            GroupPrefix = NormalizeText(GroupPrefix);
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

    public sealed class RumorWaypointConfig
    {
        public double ApproximateMinimumOffset = 128;

        public double ApproximateMaximumOffset = 384;

        public string ApproximateRuinColor = "#4DA6FF";

        public string ExactRuinColor = "#66CC66";

        public string TranslocatorColor = "#4DA6FF";

        public string TraderColor = "#F2C94C";

        public string RuinIcon = "ruins";

        public string TranslocatorIcon = "translocator";

        public string TraderIcon = "trader";

        public string FallbackIcon = "circle";

        public string ApproximateRuinTitleKey =
            "rumornetwork:waypoint-ruins-approximate";

        public string ExactRuinTitleKey =
            "rumornetwork:waypoint-ruins-exact";

        public string TranslocatorTitleKey =
            "rumornetwork:waypoint-translocator";

        public string TraderTitleKey =
            "rumornetwork:waypoint-trader";

        public static RumorWaypointConfig CreateDefault()
        {
            return new RumorWaypointConfig();
        }

        public void Normalize()
        {
            if (ApproximateMinimumOffset < 0)
            {
                ApproximateMinimumOffset = 0;
            }

            if (
                ApproximateMaximumOffset <
                ApproximateMinimumOffset
            )
            {
                ApproximateMaximumOffset =
                    ApproximateMinimumOffset;
            }

            ApproximateRuinColor = NormalizeText(
                ApproximateRuinColor,
                "#4DA6FF"
            );

            ExactRuinColor = NormalizeText(
                ExactRuinColor,
                "#66CC66"
            );

            TranslocatorColor = NormalizeText(
                TranslocatorColor,
                "#4DA6FF"
            );

            TraderColor = NormalizeText(
                TraderColor,
                "#F2C94C"
            );

            RuinIcon = NormalizeText(RuinIcon, "ruins");
            TranslocatorIcon = NormalizeText(
                TranslocatorIcon,
                "translocator"
            );
            TraderIcon = NormalizeText(TraderIcon, "trader");
            FallbackIcon = NormalizeText(FallbackIcon, "circle");

            ApproximateRuinTitleKey = NormalizeText(
                ApproximateRuinTitleKey,
                "rumornetwork:waypoint-ruins-approximate"
            );

            ExactRuinTitleKey = NormalizeText(
                ExactRuinTitleKey,
                "rumornetwork:waypoint-ruins-exact"
            );

            TranslocatorTitleKey = NormalizeText(
                TranslocatorTitleKey,
                "rumornetwork:waypoint-translocator"
            );

            TraderTitleKey = NormalizeText(
                TraderTitleKey,
                "rumornetwork:waypoint-trader"
            );
        }

        private static string NormalizeText(
            string? value,
            string fallback
        )
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }
    }
}
