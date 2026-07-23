using System;
using HarmonyLib;
using RumorNetwork.Configuration;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Structures
{
    public sealed class BetterRuinsClassificationCompatibilityPatch :
        ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.betterruins-classification-compat";

        private const string BetterRuinsPrefix = "betterruins:";

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.92;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            harmony = new Harmony(HarmonyId);

            var classify = AccessTools.Method(
                typeof(StructureClassifier),
                nameof(StructureClassifier.Classify),
                new[] { typeof(GeneratedStructure) }
            );

            var import = AccessTools.Method(
                typeof(RumorRegistry),
                nameof(RumorRegistry.Import)
            );

            if (classify != null)
            {
                harmony.Patch(
                    classify,
                    prefix: new HarmonyMethod(
                        typeof(BetterRuinsClassificationCompatibilityPatch),
                        nameof(ClassifyBetterRuins)
                    )
                );
            }
            else
            {
                api.Logger.Error(
                    "Rumor Network não encontrou o classificador para " +
                    "corrigir as categorias do BetterRuins."
                );
            }

            if (import != null)
            {
                harmony.Patch(
                    import,
                    prefix: new HarmonyMethod(
                        typeof(BetterRuinsClassificationCompatibilityPatch),
                        nameof(MigratePersistedRecords)
                    )
                );
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private static bool ClassifyBetterRuins(
            GeneratedStructure structure,
            ref StructureKind __result
        )
        {
            string code = Normalize(structure?.Code);
            string group = Normalize(structure?.Group);

            if (!IsBetterRuins(code))
            {
                return true;
            }

            StructureKind? corrected = ClassifyKnownCategory(
                code,
                group
            );

            if (!corrected.HasValue)
            {
                return true;
            }

            __result = corrected.Value;
            return false;
        }

        private static void MigratePersistedRecords(
            RumorRegistrySaveData saveData
        )
        {
            if (saveData?.Records == null)
            {
                return;
            }

            foreach (RumorRecord record in saveData.Records)
            {
                string code = Normalize(record.SourceCode);
                string group = Normalize(record.SourceGroup);

                if (!IsBetterRuins(code))
                {
                    continue;
                }

                StructureKind? corrected = ClassifyKnownCategory(
                    code,
                    group
                );

                if (
                    !corrected.HasValue ||
                    corrected.Value == record.Kind
                )
                {
                    continue;
                }

                record.Kind = corrected.Value;
                record.Id = ReplaceKindPrefix(
                    record.Id,
                    corrected.Value
                );
            }
        }

        private static StructureKind? ClassifyKnownCategory(
            string code,
            string group
        )
        {
            if (
                RumorRuntimeSettings.BetterRuins.ExcludeStoryStructures &&
                string.Equals(
                    group,
                    "storystructure",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return StructureKind.StoryStructure;
            }

            string leaf = GetCodeLeaf(code);

            if (string.Equals(
                    leaf,
                    "gates",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return StructureKind.Gate;
            }

            if (string.Equals(
                    leaf,
                    "undergroundruins",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return StructureKind.UndergroundRuin;
            }

            return null;
        }

        private static bool IsBetterRuins(string code)
        {
            return code.StartsWith(
                BetterRuinsPrefix,
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static string GetCodeLeaf(string code)
        {
            int slash = code.LastIndexOf('/');
            int colon = code.LastIndexOf(':');
            int separator = Math.Max(slash, colon);

            return separator >= 0 && separator + 1 < code.Length
                ? code[(separator + 1)..]
                : code;
        }

        private static string ReplaceKindPrefix(
            string id,
            StructureKind kind
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            int separator = id.IndexOf('|');

            return separator < 0
                ? id
                : kind + id[separator..];
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }
}
