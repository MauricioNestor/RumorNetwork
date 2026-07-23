using System;
using System.Collections.Generic;
using RumorNetwork.Configuration;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RumorNetwork.Catalog
{
    public sealed class VerifiedStructureDiscoveryService
    {
        private const int TickIntervalMs = 50;
        private const int TraderSpawnRadius = 4;
        private const int SaveDataVersion = 1;

        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorRegistry rumorRegistry;
        private readonly RemoteStructureCatalogConfig config;

        private readonly HashSet<long> inspectedChunkKeys =
            new();

        private readonly HashSet<long> activeChunkKeys =
            new();

        private readonly HashSet<string> exhaustedSearchKeys =
            new(StringComparer.Ordinal);

        private readonly List<DiscoverySearch> searches =
            new();

        private readonly List<int> translocatorBlockIds =
            new();

        private readonly int chunkSize;
        private readonly int chunkCountX;
        private readonly int chunkCountZ;

        private long tickListenerId;
        private long nextPeekAtMs;
        private int activePeekCount;
        private int nextSearchIndex;
        private bool started;

        public int InspectedChunkCount =>
            inspectedChunkKeys.Count;

        public int ActivePeekCount =>
            activePeekCount;

        public int ActiveSearchCount =>
            searches.Count;

        public bool IsWorking =>
            activePeekCount > 0 || searches.Count > 0;

        public int PendingPeekBudget
        {
            get
            {
                int pending = 0;

                foreach (DiscoverySearch search in searches)
                {
                    pending += Math.Max(
                        0,
                        config.MaxPeekColumnsPerSearch -
                            search.PeekCallsStarted
                    );
                }

                return pending;
            }
        }

        public int LargestSearchRadiusChunks
        {
            get
            {
                int largest = 0;

                foreach (DiscoverySearch search in searches)
                {
                    largest = Math.Max(
                        largest,
                        search.MaximumRadiusChunks
                    );
                }

                return largest;
            }
        }

        public VerifiedStructureDiscoveryService(
            ICoreServerAPI api,
            ILogger logger,
            RumorRegistry rumorRegistry,
            RemoteStructureCatalogConfig config
        )
        {
            this.api = api;
            this.logger = logger;
            this.rumorRegistry = rumorRegistry;
            this.config = config;

            chunkSize = Math.Max(
                1,
                api.WorldManager.ChunkSize
            );

            chunkCountX = DivideRoundUp(
                api.WorldManager.MapSizeX,
                chunkSize
            );

            chunkCountZ = DivideRoundUp(
                api.WorldManager.MapSizeZ,
                chunkSize
            );
        }

        public void Import(
            VerifiedStructureDiscoverySaveData saveData
        )
        {
            inspectedChunkKeys.Clear();

            if (
                saveData == null ||
                saveData.Version < SaveDataVersion
            )
            {
                int removed = rumorRegistry.RemoveByKind(
                    StructureKind.Trader,
                    StructureKind.Translocator
                );

                if (removed > 0)
                {
                    logger.Notification(
                        "Rumor Network removeu " +
                        $"{removed} alvos remotos antigos sem " +
                        "validação física. Eles serão " +
                        "redescobertos por peek."
                    );
                }

                return;
            }

            if (saveData.InspectedChunkIndices == null)
            {
                return;
            }

            foreach (
                long chunkKey
                in saveData.InspectedChunkIndices
            )
            {
                inspectedChunkKeys.Add(chunkKey);
            }
        }

        public VerifiedStructureDiscoverySaveData Export()
        {
            return new VerifiedStructureDiscoverySaveData
            {
                Version = SaveDataVersion,
                InspectedChunkIndices = new List<long>(
                    inspectedChunkKeys
                )
            };
        }

        public void Start()
        {
            if (started || !config.Enabled)
            {
                return;
            }

            started = true;
            ResolveTranslocatorBlockIds();

            api.Event.ChunkColumnLoaded +=
                OnChunkColumnLoaded;

            api.Event.PlayerReady +=
                OnPlayerReady;

            tickListenerId =
                api.Event.RegisterGameTickListener(
                    ProcessSearches,
                    TickIntervalMs
                );
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }

            started = false;

            api.Event.ChunkColumnLoaded -=
                OnChunkColumnLoaded;

            api.Event.PlayerReady -=
                OnPlayerReady;

            if (tickListenerId != 0)
            {
                api.Event.UnregisterGameTickListener(
                    tickListenerId
                );

                tickListenerId = 0;
            }

            searches.Clear();
            activeChunkKeys.Clear();
            activePeekCount = 0;
        }

        public bool RequestAdditional(
            StructureKind kind,
            int blockX,
            int blockZ
        )
        {
            if (
                !started ||
                !config.Enabled ||
                !IsSupportedKind(kind)
            )
            {
                return false;
            }

            int centerChunkX = FloorDivide(
                blockX,
                chunkSize
            );

            int centerChunkZ = FloorDivide(
                blockZ,
                chunkSize
            );

            int initialCount =
                rumorRegistry.CountByKind(kind);

            string searchKey = CreateSearchKey(
                kind,
                centerChunkX,
                centerChunkZ,
                initialCount
            );

            if (exhaustedSearchKeys.Contains(searchKey))
            {
                return false;
            }

            foreach (DiscoverySearch search in searches)
            {
                if (search.Key == searchKey)
                {
                    return false;
                }
            }

            int radius = Math.Max(
                1,
                config.MaxSearchRadiusChunks
            );

            List<Vec2i> candidates =
                CreateCandidates(
                    centerChunkX,
                    centerChunkZ,
                    radius
                );

            DiscoverySearch newSearch = new(
                searchKey,
                kind,
                centerChunkX,
                centerChunkZ,
                initialCount,
                radius,
                candidates
            );

            searches.Add(newSearch);

            logger.Notification(
                "Rumor Network iniciou descoberta verificada " +
                $"de {kind}. Centro=({centerChunkX},{centerChunkZ}) | " +
                $"Raio máximo={radius} chunks | " +
                $"Orçamento={config.MaxPeekColumnsPerSearch} peeks | " +
                $"Intervalo={config.PeekIntervalMs}ms | " +
                $"Concorrência={config.MaxConcurrentPeeks}."
            );

            return true;
        }

        public bool IsSearching(
            StructureKind kind,
            int blockX,
            int blockZ
        )
        {
            int centerChunkX = FloorDivide(
                blockX,
                chunkSize
            );

            int centerChunkZ = FloorDivide(
                blockZ,
                chunkSize
            );

            int currentCount =
                rumorRegistry.CountByKind(kind);

            string searchKey = CreateSearchKey(
                kind,
                centerChunkX,
                centerChunkZ,
                currentCount
            );

            foreach (DiscoverySearch search in searches)
            {
                if (search.Key == searchKey)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnPlayerReady(
            IServerPlayer player
        )
        {
            if (!config.ScanOnPlayerReady)
            {
                return;
            }

            int blockX = (int)player.Entity.Pos.X;
            int blockZ = (int)player.Entity.Pos.Z;

            RequestAdditional(
                StructureKind.Trader,
                blockX,
                blockZ
            );

            RequestAdditional(
                StructureKind.Translocator,
                blockX,
                blockZ
            );
        }

        private void OnChunkColumnLoaded(
            Vec2i chunkCoord,
            IWorldChunk[] chunks
        )
        {
            if (!started || chunks == null)
            {
                return;
            }

            IServerChunk[] serverChunks =
                new IServerChunk[chunks.Length];

            bool hasServerChunk = false;

            for (int index = 0;
                index < chunks.Length;
                index++)
            {
                if (chunks[index] is IServerChunk serverChunk)
                {
                    serverChunks[index] = serverChunk;
                    hasServerChunk = true;
                }
            }

            if (!hasServerChunk)
            {
                return;
            }

            Dictionary<Vec2i, IServerChunk[]> columns =
                new()
                {
                    [new Vec2i(
                        chunkCoord.X,
                        chunkCoord.Y
                    )] = serverChunks
                };

            InspectionResult inspection =
                InspectColumns(columns);

            inspectedChunkKeys.Add(
                CreateChunkKey(
                    chunkCoord.X,
                    chunkCoord.Y
                )
            );

            MergeVerifiedSites(
                inspection.Sites,
                "chunk carregado"
            );
        }

        private void ProcessSearches(float deltaTime)
        {
            if (!started || searches.Count == 0)
            {
                return;
            }

            CompleteSatisfiedSearches();

            if (searches.Count == 0)
            {
                return;
            }

            if (
                api.WorldManager.CurrentGeneratingChunkCount >
                    config.PauseWhenGeneratingChunksAbove
            )
            {
                return;
            }

            long now = api.World.ElapsedMilliseconds;

            if (now < nextPeekAtMs)
            {
                return;
            }

            if (
                activePeekCount >=
                    config.MaxConcurrentPeeks
            )
            {
                return;
            }

            int attempts = searches.Count;

            while (
                attempts-- > 0 &&
                searches.Count > 0
            )
            {
                if (nextSearchIndex >= searches.Count)
                {
                    nextSearchIndex = 0;
                }

                DiscoverySearch search =
                    searches[nextSearchIndex];

                nextSearchIndex++;

                if (TryStartNextPeek(search))
                {
                    nextPeekAtMs =
                        now + config.PeekIntervalMs;

                    return;
                }
            }
        }

        private void CompleteSatisfiedSearches()
        {
            for (int index = searches.Count - 1;
                index >= 0;
                index--)
            {
                DiscoverySearch search = searches[index];

                int discovered =
                    rumorRegistry.CountByKind(search.Kind) -
                    search.InitialKindCount;

                if (
                    discovered <
                    config.StopAfterNewTargets
                )
                {
                    continue;
                }

                logger.Notification(
                    "Rumor Network concluiu descoberta " +
                    $"verificada de {search.Kind}: " +
                    $"{discovered} novo(s) alvo(s). " +
                    $"Peeks iniciados={search.PeekCallsStarted} | " +
                    $"concluídos={search.PeekCallsCompleted}."
                );

                searches.RemoveAt(index);
            }

            if (nextSearchIndex > searches.Count)
            {
                nextSearchIndex = 0;
            }
        }

        private bool TryStartNextPeek(
            DiscoverySearch search
        )
        {
            while (
                search.CandidateIndex <
                    search.Candidates.Count &&
                search.PeekCallsStarted <
                    config.MaxPeekColumnsPerSearch
            )
            {
                Vec2i candidate =
                    search.Candidates[
                        search.CandidateIndex++
                    ];

                long chunkKey = CreateChunkKey(
                    candidate.X,
                    candidate.Y
                );

                if (
                    inspectedChunkKeys.Contains(chunkKey) ||
                    activeChunkKeys.Contains(chunkKey)
                )
                {
                    continue;
                }

                activeChunkKeys.Add(chunkKey);
                activePeekCount++;
                search.ActivePeekCount++;
                search.PeekCallsStarted++;

                try
                {
                    ChunkPeekOptions options = new()
                    {
                        UntilPass =
                            EnumWorldGenPass.TerrainFeatures,

                        OnGenerated = columns =>
                            OnPeekGeneratedWorker(
                                search,
                                chunkKey,
                                columns
                            )
                    };

                    api.WorldManager.PeekChunkColumn(
                        candidate.X,
                        candidate.Y,
                        options
                    );

                    return true;
                }
                catch (Exception exception)
                {
                    activeChunkKeys.Remove(chunkKey);
                    activePeekCount = Math.Max(
                        0,
                        activePeekCount - 1
                    );

                    search.ActivePeekCount = Math.Max(
                        0,
                        search.ActivePeekCount - 1
                    );

                    search.FailedPeekCount++;

                    logger.Warning(
                        "Rumor Network não conseguiu iniciar " +
                        $"o peek de {candidate.X},{candidate.Y}: " +
                        exception.GetBaseException().Message
                    );
                }
            }

            if (search.ActivePeekCount > 0)
            {
                return false;
            }

            ExhaustSearch(search);
            return false;
        }

        private void OnPeekGeneratedWorker(
            DiscoverySearch search,
            long requestedChunkKey,
            Dictionary<Vec2i, IServerChunk[]>
                columns
        )
        {
            InspectionResult inspection;
            string? failure = null;

            try
            {
                inspection = InspectColumns(columns);
            }
            catch (Exception exception)
            {
                inspection = new InspectionResult();
                failure =
                    exception.GetBaseException().Message;
            }

            api.Event.EnqueueMainThreadTask(
                () => CompletePeek(
                    search,
                    requestedChunkKey,
                    inspection,
                    failure
                ),
                "rumornetwork-complete-verified-peek"
            );
        }

        private void CompletePeek(
            DiscoverySearch search,
            long requestedChunkKey,
            InspectionResult inspection,
            string? failure
        )
        {
            activeChunkKeys.Remove(requestedChunkKey);
            activePeekCount = Math.Max(
                0,
                activePeekCount - 1
            );

            search.ActivePeekCount = Math.Max(
                0,
                search.ActivePeekCount - 1
            );

            search.PeekCallsCompleted++;

            if (failure != null)
            {
                search.FailedPeekCount++;

                logger.Warning(
                    "Rumor Network falhou ao analisar um " +
                    $"peek: {failure}"
                );

                return;
            }

            inspectedChunkKeys.Add(requestedChunkKey);

            foreach (long chunkKey in inspection.ChunkKeys)
            {
                inspectedChunkKeys.Add(chunkKey);
            }

            MergeVerifiedSites(
                inspection.Sites,
                "peek temporário"
            );
        }

        private void ExhaustSearch(
            DiscoverySearch search
        )
        {
            exhaustedSearchKeys.Add(search.Key);
            searches.Remove(search);

            logger.Notification(
                "Rumor Network encerrou descoberta " +
                $"verificada de {search.Kind} sem encontrar " +
                "um novo alvo. " +
                $"Peeks iniciados={search.PeekCallsStarted} | " +
                $"concluídos={search.PeekCallsCompleted} | " +
                $"falhos={search.FailedPeekCount} | " +
                $"raio={search.MaximumRadiusChunks} chunks."
            );

            if (nextSearchIndex > searches.Count)
            {
                nextSearchIndex = 0;
            }
        }

        private InspectionResult InspectColumns(
            Dictionary<Vec2i, IServerChunk[]> columns
        )
        {
            InspectionResult result = new();

            if (columns == null)
            {
                return result;
            }

            HashSet<string> siteIds =
                new(StringComparer.Ordinal);

            foreach (
                KeyValuePair<Vec2i, IServerChunk[]> pair
                in columns
            )
            {
                Vec2i chunkCoord = pair.Key;
                IServerChunk[] chunks = pair.Value;

                result.ChunkKeys.Add(
                    CreateChunkKey(
                        chunkCoord.X,
                        chunkCoord.Y
                    )
                );

                if (chunks == null)
                {
                    continue;
                }

                InspectTraderEntities(
                    chunks,
                    result.Sites,
                    siteIds
                );

                InspectTranslocatorBlocks(
                    chunkCoord,
                    chunks,
                    result.Sites,
                    siteIds
                );
            }

            return result;
        }

        private void InspectTraderEntities(
            IServerChunk[] chunks,
            ICollection<RumorSite> sites,
            ISet<string> siteIds
        )
        {
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

                    if (structure == null)
                    {
                        continue;
                    }

                    RumorSite site = CreateVerifiedSite(
                        structure,
                        StructureKind.Trader
                    );

                    if (siteIds.Add(site.Id))
                    {
                        sites.Add(site);
                    }
                }
            }
        }

        private void InspectTranslocatorBlocks(
            Vec2i chunkCoord,
            IServerChunk[] chunks,
            ICollection<RumorSite> sites,
            ISet<string> siteIds
        )
        {
            if (translocatorBlockIds.Count == 0)
            {
                return;
            }

            for (int chunkY = 0;
                chunkY < chunks.Length;
                chunkY++)
            {
                IServerChunk chunk = chunks[chunkY];

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

                bool containsTranslocator = false;

                foreach (int blockId in translocatorBlockIds)
                {
                    if (chunk.Data.ContainsBlock(blockId))
                    {
                        containsTranslocator = true;
                        break;
                    }
                }

                if (!containsTranslocator)
                {
                    continue;
                }

                chunk.Unpack_ReadOnly();

                int baseX = chunkCoord.X * chunkSize;
                int baseY = chunkY * chunkSize;
                int baseZ = chunkCoord.Y * chunkSize;

                for (int localY = 0;
                    localY < chunkSize;
                    localY++)
                {
                    for (int localZ = 0;
                        localZ < chunkSize;
                        localZ++)
                    {
                        for (int localX = 0;
                            localX < chunkSize;
                            localX++)
                        {
                            int index3d =
                                (localY * chunkSize + localZ) *
                                    chunkSize + localX;

                            int blockId = chunk.Data[index3d];

                            if (
                                blockId < 0 ||
                                blockId >= api.World.Blocks.Count
                            )
                            {
                                continue;
                            }

                            if (
                                api.World.Blocks[blockId]
                                is not BlockStaticTranslocator
                            )
                            {
                                continue;
                            }

                            int blockX = baseX + localX;
                            int blockY = baseY + localY;
                            int blockZ = baseZ + localZ;

                            GeneratedStructure? structure =
                                FindTranslocatorStructure(
                                    structures,
                                    blockX,
                                    blockY,
                                    blockZ
                                );

                            if (structure == null)
                            {
                                continue;
                            }

                            RumorSite site = CreateVerifiedSite(
                                structure,
                                StructureKind.Translocator
                            );

                            if (siteIds.Add(site.Id))
                            {
                                sites.Add(site);
                            }
                        }
                    }
                }
            }
        }

        private GeneratedStructure? FindTraderStructure(
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

        private static GeneratedStructure?
            FindTranslocatorStructure(
                IEnumerable<GeneratedStructure> structures,
                int blockX,
                int blockY,
                int blockZ
            )
        {
            foreach (GeneratedStructure structure in structures)
            {
                if (!IsTranslocatorCandidate(structure))
                {
                    continue;
                }

                if (ContainsExpanded(
                        structure.Location,
                        blockX,
                        blockY,
                        blockZ,
                        1
                    ))
                {
                    return structure;
                }
            }

            return null;
        }

        private static bool IsTranslocatorCandidate(
            GeneratedStructure structure
        )
        {
            StructureKind classified =
                StructureClassifier.Classify(structure);

            if (
                classified == StructureKind.Translocator ||
                classified == StructureKind.Gate
            )
            {
                return true;
            }

            string code =
                structure.Code?.ToLowerInvariant() ??
                string.Empty;

            return code.Contains("gates");
        }

        private RumorSite CreateVerifiedSite(
            GeneratedStructure structure,
            StructureKind kind
        )
        {
            string family =
                StructureGrouper.GetFamily(structure);

            Cuboidi location = structure.Location;

            string id =
                $"{kind}|" +
                $"{family}|" +
                $"{location.X1}|" +
                $"{location.Y1}|" +
                $"{location.Z1}|" +
                $"{location.X2}|" +
                $"{location.Y2}|" +
                $"{location.Z2}";

            return new RumorSite(
                id,
                kind,
                family,
                structure.Code ?? string.Empty,
                location,
                1
            );
        }

        private void MergeVerifiedSites(
            IReadOnlyCollection<RumorSite> sites,
            string source
        )
        {
            if (sites.Count == 0)
            {
                return;
            }

            int added = rumorRegistry.Merge(sites);

            if (added <= 0)
            {
                return;
            }

            logger.Notification(
                "Rumor Network confirmou fisicamente " +
                $"{added} alvo(s) remoto(s) por {source}. " +
                "Traders=" +
                rumorRegistry.CountByKind(
                    StructureKind.Trader
                ) +
                " | Translocators=" +
                rumorRegistry.CountByKind(
                    StructureKind.Translocator
                ) +
                "."
            );
        }

        private void ResolveTranslocatorBlockIds()
        {
            translocatorBlockIds.Clear();

            foreach (Block block in api.World.Blocks)
            {
                if (block is BlockStaticTranslocator)
                {
                    translocatorBlockIds.Add(block.Id);
                }
            }

            logger.Notification(
                "Rumor Network encontrou " +
                $"{translocatorBlockIds.Count} variantes de " +
                "bloco de translocador para validação."
            );
        }

        private List<Vec2i> CreateCandidates(
            int centerChunkX,
            int centerChunkZ,
            int radius
        )
        {
            List<Vec2i> candidates = new();

            for (int deltaX = -radius;
                deltaX <= radius;
                deltaX++)
            {
                for (int deltaZ = -radius;
                    deltaZ <= radius;
                    deltaZ++)
                {
                    int chunkX = centerChunkX + deltaX;
                    int chunkZ = centerChunkZ + deltaZ;

                    if (!IsChunkInsideWorld(
                            chunkX,
                            chunkZ
                        ))
                    {
                        continue;
                    }

                    candidates.Add(
                        new Vec2i(chunkX, chunkZ)
                    );
                }
            }

            candidates.Sort(
                (left, right) =>
                {
                    int leftDistance = DistanceSquared(
                        left,
                        centerChunkX,
                        centerChunkZ
                    );

                    int rightDistance = DistanceSquared(
                        right,
                        centerChunkX,
                        centerChunkZ
                    );

                    int comparison =
                        leftDistance.CompareTo(
                            rightDistance
                        );

                    if (comparison != 0)
                    {
                        return comparison;
                    }

                    comparison = left.X.CompareTo(right.X);

                    return comparison != 0
                        ? comparison
                        : left.Y.CompareTo(right.Y);
                }
            );

            return candidates;
        }

        private bool IsChunkInsideWorld(
            int chunkX,
            int chunkZ
        )
        {
            return
                chunkX >= 0 &&
                chunkZ >= 0 &&
                chunkX < chunkCountX &&
                chunkZ < chunkCountZ;
        }

        private static bool IsSupportedKind(
            StructureKind kind
        )
        {
            return
                kind == StructureKind.Trader ||
                kind == StructureKind.Translocator;
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

        private static int DistanceSquared(
            Vec2i position,
            int centerX,
            int centerZ
        )
        {
            int deltaX = position.X - centerX;
            int deltaZ = position.Y - centerZ;

            return
                deltaX * deltaX +
                deltaZ * deltaZ;
        }

        private static string CreateSearchKey(
            StructureKind kind,
            int centerChunkX,
            int centerChunkZ,
            int initialCount
        )
        {
            return
                $"{kind}|" +
                $"{centerChunkX}|" +
                $"{centerChunkZ}|" +
                $"{initialCount}";
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

        private static int DivideRoundUp(
            int value,
            int divisor
        )
        {
            return (value + divisor - 1) / divisor;
        }

        private static int FloorDivide(
            int value,
            int divisor
        )
        {
            int quotient = value / divisor;
            int remainder = value % divisor;

            if (remainder != 0 && value < 0)
            {
                quotient--;
            }

            return quotient;
        }

        private sealed class DiscoverySearch
        {
            public string Key { get; }

            public StructureKind Kind { get; }

            public int CenterChunkX { get; }

            public int CenterChunkZ { get; }

            public int InitialKindCount { get; }

            public int MaximumRadiusChunks { get; }

            public List<Vec2i> Candidates { get; }

            public int CandidateIndex { get; set; }

            public int PeekCallsStarted { get; set; }

            public int PeekCallsCompleted { get; set; }

            public int ActivePeekCount { get; set; }

            public int FailedPeekCount { get; set; }

            public DiscoverySearch(
                string key,
                StructureKind kind,
                int centerChunkX,
                int centerChunkZ,
                int initialKindCount,
                int maximumRadiusChunks,
                List<Vec2i> candidates
            )
            {
                Key = key;
                Kind = kind;
                CenterChunkX = centerChunkX;
                CenterChunkZ = centerChunkZ;
                InitialKindCount = initialKindCount;
                MaximumRadiusChunks = maximumRadiusChunks;
                Candidates = candidates;
            }
        }

        private sealed class InspectionResult
        {
            public List<RumorSite> Sites { get; } =
                new();

            public HashSet<long> ChunkKeys { get; } =
                new();
        }
    }
}
