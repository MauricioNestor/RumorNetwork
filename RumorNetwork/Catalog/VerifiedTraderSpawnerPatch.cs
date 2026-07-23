using System;
using System.Collections.Generic;
using HarmonyLib;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RumorNetwork.Catalog
{
    public sealed class VerifiedTraderSpawnerPatch
        : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.verified-trader-spawners";

        private const int TraderSpawnRadius = 4;
        private const int TraderDiscoverySaveVersion = 2;

        private static ILogger? logger;
        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.9;
        }

        public override void StartServerSide(
            ICoreServerAPI api
        )
        {
            logger = api.Logger;
            harmony = new Harmony(HarmonyId);

            PatchTraderInspection(api);
            PatchDiscoveryImport(api);
            PatchDiscoveryExport(api);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            logger = null;
            base.Dispose();
        }

        private void PatchTraderInspection(
            ICoreServerAPI api
        )
        {
            var original = AccessTools.Method(
                typeof(VerifiedStructureDiscoveryService),
                "InspectTraderEntities"
            );

            if (original == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "InspectTraderEntities para substituir a " +
                    "validação antiga de traders."
                );

                return;
            }

            harmony!.Patch(
                original,
                prefix: new HarmonyMethod(
                    typeof(VerifiedTraderSpawnerPatch),
                    nameof(InspectTraderSpawners)
                )
            );
        }

        private void PatchDiscoveryImport(
            ICoreServerAPI api
        )
        {
            var original = AccessTools.Method(
                typeof(VerifiedStructureDiscoveryService),
                nameof(VerifiedStructureDiscoveryService.Import)
            );

            if (original == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou Import para " +
                    "invalidar o cache antigo de traders."
                );

                return;
            }

            harmony!.Patch(
                original,
                prefix: new HarmonyMethod(
                    typeof(VerifiedTraderSpawnerPatch),
                    nameof(MigrateDiscoveryCache)
                )
            );
        }

        private void PatchDiscoveryExport(
            ICoreServerAPI api
        )
        {
            var original = AccessTools.Method(
                typeof(VerifiedStructureDiscoveryService),
                nameof(VerifiedStructureDiscoveryService.Export)
            );

            if (original == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou Export para " +
                    "persistir a versão do catálogo de traders."
                );

                return;
            }

            harmony!.Patch(
                original,
                postfix: new HarmonyMethod(
                    typeof(VerifiedTraderSpawnerPatch),
                    nameof(MarkDiscoveryVersion)
                )
            );
        }

        private static bool InspectTraderSpawners(
            IServerChunk[] chunks,
            ICollection<RumorSite> sites,
            ISet<string> siteIds
        )
        {
            if (chunks == null)
            {
                return false;
            }

            foreach (IServerChunk chunk in chunks)
            {
                if (chunk == null)
                {
                    continue;
                }

                IMapRegion region =
                    chunk.MapChunk?.MapRegion;

                List<GeneratedStructure> structures =
                    region?.GeneratedStructures;

                if (
                    structures == null ||
                    structures.Count == 0
                )
                {
                    continue;
                }

                InspectBlockEntitySpawners(
                    chunk,
                    structures,
                    sites,
                    siteIds
                );

                InspectSpawnedTraderFallback(
                    chunk,
                    structures,
                    sites,
                    siteIds
                );
            }

            // The spawner is the persistent physical proof. Do not execute
            // the original implementation, which required EntityTrader to
            // already be materialized in chunk.Entities.
            return false;
        }

        private static void InspectBlockEntitySpawners(
            IServerChunk chunk,
            IEnumerable<GeneratedStructure> structures,
            ICollection<RumorSite> sites,
            ISet<string> siteIds
        )
        {
            Dictionary<BlockPos, BlockEntity> blockEntities =
                chunk.BlockEntities;

            if (
                blockEntities == null ||
                blockEntities.Count == 0
            )
            {
                return;
            }

            foreach (
                KeyValuePair<BlockPos, BlockEntity> pair
                in blockEntities
            )
            {
                if (
                    pair.Value is not BlockEntitySpawner spawner ||
                    !IsTraderSpawner(spawner)
                )
                {
                    continue;
                }

                BlockPos spawnPosition =
                    spawner.Pos ?? pair.Key;

                if (spawnPosition == null)
                {
                    continue;
                }

                GeneratedStructure? structure =
                    FindTraderStructure(
                        structures,
                        spawnPosition.X + 0.5,
                        spawnPosition.Y,
                        spawnPosition.Z + 0.5
                    );

                AddVerifiedTraderSite(
                    structure,
                    sites,
                    siteIds
                );
            }
        }

        private static bool IsTraderSpawner(
            BlockEntitySpawner spawner
        )
        {
            string[] entityCodes =
                spawner.Data?.EntityCodes;

            if (
                entityCodes == null ||
                entityCodes.Length == 0
            )
            {
                return false;
            }

            foreach (string entityCode in entityCodes)
            {
                if (
                    !string.IsNullOrWhiteSpace(entityCode) &&
                    entityCode.Contains(
                        "trader",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static void InspectSpawnedTraderFallback(
            IServerChunk chunk,
            IEnumerable<GeneratedStructure> structures,
            ICollection<RumorSite> sites,
            ISet<string> siteIds
        )
        {
            Entity[] entities = chunk.Entities;
            int entityCount = Math.Min(
                chunk.EntitiesCount,
                entities?.Length ?? 0
            );

            for (int index = 0;
                index < entityCount;
                index++)
            {
                if (entities[index] is not EntityTrader trader)
                {
                    continue;
                }

                double spawnX =
                    trader.Attributes.HasAttribute("spawnX")
                        ? trader.Attributes.GetDouble("spawnX")
                        : trader.Pos.X;

                double spawnY =
                    trader.Attributes.HasAttribute("spawnY")
                        ? trader.Attributes.GetDouble("spawnY")
                        : trader.Pos.Y;

                double spawnZ =
                    trader.Attributes.HasAttribute("spawnZ")
                        ? trader.Attributes.GetDouble("spawnZ")
                        : trader.Pos.Z;

                GeneratedStructure? structure =
                    FindTraderStructure(
                        structures,
                        spawnX,
                        spawnY,
                        spawnZ
                    );

                AddVerifiedTraderSite(
                    structure,
                    sites,
                    siteIds
                );
            }
        }

        private static GeneratedStructure? FindTraderStructure(
            IEnumerable<GeneratedStructure> structures,
            double spawnX,
            double spawnY,
            double spawnZ
        )
        {
            GeneratedStructure? best = null;
            double bestDistance = double.MaxValue;

            foreach (GeneratedStructure structure in structures)
            {
                if (
                    StructureClassifier.Classify(structure)
                    != StructureKind.Trader
                )
                {
                    continue;
                }

                Cuboidi location = structure.Location;

                if (!ContainsExpanded(
                        location,
                        spawnX,
                        spawnY,
                        spawnZ,
                        TraderSpawnRadius
                    ))
                {
                    continue;
                }

                Vec3i center = location.Center;
                double deltaX = center.X - spawnX;
                double deltaZ = center.Z - spawnZ;
                double distance =
                    deltaX * deltaX +
                    deltaZ * deltaZ;

                if (distance < bestDistance)
                {
                    best = structure;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static void AddVerifiedTraderSite(
            GeneratedStructure? structure,
            ICollection<RumorSite> sites,
            ISet<string> siteIds
        )
        {
            if (structure == null)
            {
                return;
            }

            string family =
                StructureGrouper.GetFamily(structure);

            Cuboidi location = structure.Location;

            string id =
                $"{StructureKind.Trader}|" +
                $"{family}|" +
                $"{location.X1}|" +
                $"{location.Y1}|" +
                $"{location.Z1}|" +
                $"{location.X2}|" +
                $"{location.Y2}|" +
                $"{location.Z2}";

            if (!siteIds.Add(id))
            {
                return;
            }

            sites.Add(
                new RumorSite(
                    id,
                    StructureKind.Trader,
                    family,
                    structure.Code ?? string.Empty,
                    location,
                    1
                )
            );
        }

        private static bool ContainsExpanded(
            Cuboidi location,
            double x,
            double y,
            double z,
            int radius
        )
        {
            return
                x >= location.X1 - radius &&
                x <= location.X2 + radius &&
                y >= location.Y1 - radius &&
                y <= location.Y2 + radius &&
                z >= location.Z1 - radius &&
                z <= location.Z2 + radius;
        }

        private static void MigrateDiscoveryCache(
            VerifiedStructureDiscoveryService __instance,
            VerifiedStructureDiscoverySaveData saveData
        )
        {
            if (
                saveData == null ||
                saveData.Version >= TraderDiscoverySaveVersion
            )
            {
                return;
            }

            saveData.Version = TraderDiscoverySaveVersion;
            saveData.InspectedChunkIndices?.Clear();

            RumorRegistry? registry =
                Traverse.Create(__instance)
                    .Field("rumorRegistry")
                    .GetValue<RumorRegistry>();

            int removed = registry?.RemoveByKind(
                StructureKind.Trader
            ) ?? 0;

            logger?.Notification(
                "Rumor Network invalidou o cache antigo de " +
                "traders baseado em entidades já materializadas. " +
                $"Traders removidos={removed}. " +
                "Os spawners persistentes serão inspecionados."
            );
        }

        private static void MarkDiscoveryVersion(
            ref VerifiedStructureDiscoverySaveData __result
        )
        {
            if (__result != null)
            {
                __result.Version =
                    TraderDiscoverySaveVersion;
            }
        }
    }
}
