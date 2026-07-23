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
        private const int TraderDiscoverySaveVersion = 3;
        private const int LoadedChunkRescanDelayMs = 1500;

        private static readonly object SyncRoot = new();

        private static readonly HashSet<long>
            ScheduledLoadedChunkKeys = new();

        private static readonly Dictionary<string, bool>
            TraderEntityCodeCache =
                new(StringComparer.OrdinalIgnoreCase);

        private static ICoreServerAPI? serverApi;
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
            serverApi = api;
            logger = api.Logger;
            harmony = new Harmony(HarmonyId);

            PatchTraderInspection(api);
            PatchLoadedChunkRescan(api);
            PatchDiscoveryImport(api);
            PatchDiscoveryExport(api);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;

            lock (SyncRoot)
            {
                ScheduledLoadedChunkKeys.Clear();
                TraderEntityCodeCache.Clear();
            }

            serverApi = null;
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

        private void PatchLoadedChunkRescan(
            ICoreServerAPI api
        )
        {
            var original = AccessTools.Method(
                typeof(VerifiedStructureDiscoveryService),
                "OnChunkColumnLoaded"
            );

            if (original == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "OnChunkColumnLoaded para agendar a " +
                    "segunda inspeção de traders."
                );

                return;
            }

            harmony!.Patch(
                original,
                postfix: new HarmonyMethod(
                    typeof(VerifiedTraderSpawnerPatch),
                    nameof(ScheduleLoadedChunkRescan)
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
                if (
                    chunk == null ||
                    chunk.Disposed
                )
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

            // A prova persistente é o spawner importado. A entidade viva é
            // apenas fallback, porque pode ainda não existir ou estar no
            // intervalo de respawn.
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
                    !IsActiveTraderSpawner(spawner)
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
                    spawnPosition.X,
                    spawnPosition.Y,
                    spawnPosition.Z,
                    sites,
                    siteIds
                );
            }
        }

        private static bool IsActiveTraderSpawner(
            BlockEntitySpawner spawner
        )
        {
            BESpawnerData? data = spawner.Data;

            if (
                data == null ||
                data.EntityCodes == null ||
                data.EntityCodes.Length == 0 ||
                data.MaxCount <= 0
            )
            {
                return false;
            }

            // Meta-spawners not fully imported during a speculative or
            // incomplete schematic pass are not proof that the final world
            // will contain a working trader post.
            if (
                data.SpawnOnlyAfterImport &&
                !data.WasImported
            )
            {
                return false;
            }

            foreach (string entityCode in data.EntityCodes)
            {
                if (IsEntityCodeTrader(entityCode))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEntityCodeTrader(
            string entityCode
        )
        {
            if (string.IsNullOrWhiteSpace(entityCode))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (
                    TraderEntityCodeCache.TryGetValue(
                        entityCode,
                        out bool cached
                    )
                )
                {
                    return cached;
                }
            }

            bool isTrader = false;

            try
            {
                ICoreServerAPI? api = serverApi;

                if (api != null)
                {
                    EntityProperties? properties =
                        api.World.GetEntityType(
                            new AssetLocation(entityCode)
                        );

                    isTrader = string.Equals(
                        properties?.Class,
                        "EntityTrader",
                        StringComparison.OrdinalIgnoreCase
                    );
                }
            }
            catch (Exception exception)
            {
                logger?.Debug(
                    "Rumor Network não conseguiu resolver o " +
                    $"tipo de entidade do spawner {entityCode}: " +
                    exception.GetBaseException().Message
                );
            }

            lock (SyncRoot)
            {
                TraderEntityCodeCache[entityCode] = isTrader;
            }

            return isTrader;
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
                    (int)Math.Floor(spawnX),
                    (int)Math.Floor(spawnY),
                    (int)Math.Floor(spawnZ),
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
                if (!IsTraderStructure(structure))
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

        private static bool IsTraderStructure(
            GeneratedStructure structure
        )
        {
            if (structure?.Location == null)
            {
                return false;
            }

            // The old classifier also accepted broad code substrings. For a
            // verified trader target we require the vanilla worldgen group.
            return string.Equals(
                structure.Group?.Trim(),
                "trader",
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static void AddVerifiedTraderSite(
            GeneratedStructure? structure,
            int anchorX,
            int anchorY,
            int anchorZ,
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

            Cuboidi structureLocation = structure.Location;
            Cuboidi targetLocation = new(
                anchorX,
                anchorY,
                anchorZ,
                anchorX,
                anchorY,
                anchorZ
            );

            string id =
                $"{StructureKind.Trader}|" +
                $"{family}|" +
                $"{structureLocation.X1}|" +
                $"{structureLocation.Y1}|" +
                $"{structureLocation.Z1}|" +
                $"{structureLocation.X2}|" +
                $"{structureLocation.Y2}|" +
                $"{structureLocation.Z2}|" +
                $"spawn={anchorX},{anchorY},{anchorZ}";

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
                    targetLocation,
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

        private static void ScheduleLoadedChunkRescan(
            VerifiedStructureDiscoveryService __instance,
            Vec2i chunkCoord
        )
        {
            ICoreServerAPI? api = serverApi;

            if (api == null)
            {
                return;
            }

            int chunkX = chunkCoord.X;
            int chunkZ = chunkCoord.Y;
            long chunkKey = CreateChunkKey(chunkX, chunkZ);

            lock (SyncRoot)
            {
                if (!ScheduledLoadedChunkKeys.Add(chunkKey))
                {
                    return;
                }
            }

            api.Event.RegisterCallback(
                _ => InspectLoadedChunkAfterInitialization(
                    __instance,
                    chunkX,
                    chunkZ,
                    chunkKey
                ),
                LoadedChunkRescanDelayMs,
                true
            );
        }

        private static void InspectLoadedChunkAfterInitialization(
            VerifiedStructureDiscoveryService service,
            int chunkX,
            int chunkZ,
            long chunkKey
        )
        {
            lock (SyncRoot)
            {
                ScheduledLoadedChunkKeys.Remove(chunkKey);
            }

            ICoreServerAPI? api = serverApi;

            if (api == null)
            {
                return;
            }

            int chunkSize = Math.Max(
                1,
                api.WorldManager.ChunkSize
            );

            int chunkCountY =
                (api.WorldManager.MapSizeY + chunkSize - 1) /
                chunkSize;

            IServerChunk[] chunks =
                new IServerChunk[chunkCountY];

            bool hasLoadedChunk = false;

            for (int chunkY = 0;
                chunkY < chunkCountY;
                chunkY++)
            {
                IServerChunk loadedChunk =
                    api.WorldManager.GetChunk(
                        chunkX,
                        chunkY,
                        chunkZ
                    );

                chunks[chunkY] = loadedChunk;
                hasLoadedChunk |= loadedChunk != null;
            }

            if (!hasLoadedChunk)
            {
                return;
            }

            List<RumorSite> sites = new();
            HashSet<string> siteIds =
                new(StringComparer.Ordinal);

            InspectTraderSpawners(
                chunks,
                sites,
                siteIds
            );

            if (sites.Count == 0)
            {
                return;
            }

            RumorRegistry? registry =
                Traverse.Create(service)
                    .Field("rumorRegistry")
                    .GetValue<RumorRegistry>();

            int added = registry?.Merge(sites) ?? 0;

            if (added > 0)
            {
                logger?.Notification(
                    "Rumor Network confirmou " +
                    $"{added} trader(s) após a segunda inspeção " +
                    $"do chunk carregado {chunkX},{chunkZ}."
                );
            }
        }

        private static long CreateChunkKey(
            int chunkX,
            int chunkZ
        )
        {
            return
                ((long)chunkX << 32) |
                (uint)chunkZ;
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
                "Rumor Network invalidou traders descobertos " +
                "pela validação anterior. " +
                $"Traders removidos={removed}. " +
                "O catálogo agora exige spawner importado, " +
                "tipo EntityTrader exato e usa a posição real " +
                "do spawner como waypoint."
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
