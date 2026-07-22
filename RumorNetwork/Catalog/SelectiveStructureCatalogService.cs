using System;
using System.Collections.Generic;
using RumorNetwork.Configuration;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Catalog
{
    public sealed class SelectiveStructureCatalogService
    {
        private const int TickIntervalMs = 50;

        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorRegistry rumorRegistry;
        private readonly RemoteStructureCatalogConfig config;

        private readonly HashSet<long> scannedRegionKeys =
            new();

        private readonly Queue<Vec2i> pendingRegions =
            new();

        private readonly HashSet<long> pendingRegionKeys =
            new();

        private readonly HashSet<long> activeRegionKeys =
            new();

        private readonly List<Vec2i> localChunkSearchOrder;

        private readonly object scheduledRegionLock = new();

        private readonly HashSet<long> scheduledLoadedRegions =
            new();

        private readonly int chunksPerRegion;
        private readonly int regionCountX;
        private readonly int regionCountZ;

        private long tickListenerId;
        private int activeRegionChecks;
        private bool started;

        public int ScannedRegionCount =>
            scannedRegionKeys.Count;

        public int PendingRegionCount =>
            pendingRegions.Count + activeRegionChecks;

        public bool IsWorking =>
            PendingRegionCount > 0;

        public SelectiveStructureCatalogService(
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

            chunksPerRegion = Math.Max(
                1,
                api.WorldManager.RegionSize /
                    api.WorldManager.ChunkSize
            );

            regionCountX = DivideRoundUp(
                api.WorldManager.MapSizeX,
                api.WorldManager.RegionSize
            );

            regionCountZ = DivideRoundUp(
                api.WorldManager.MapSizeZ,
                api.WorldManager.RegionSize
            );

            localChunkSearchOrder =
                CreateLocalChunkSearchOrder(
                    chunksPerRegion
                );
        }

        public void Import(
            SelectiveStructureCatalogSaveData saveData
        )
        {
            scannedRegionKeys.Clear();

            if (saveData?.ScannedRegionIndices == null)
            {
                return;
            }

            foreach (
                long regionKey
                in saveData.ScannedRegionIndices
            )
            {
                scannedRegionKeys.Add(regionKey);
            }
        }

        public SelectiveStructureCatalogSaveData Export()
        {
            return new SelectiveStructureCatalogSaveData
            {
                Version = 1,
                ScannedRegionIndices = new List<long>(
                    scannedRegionKeys
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

            api.Event.MapRegionLoaded +=
                OnMapRegionLoaded;

            api.Event.ChunkColumnLoaded +=
                OnChunkColumnLoaded;

            api.Event.PlayerReady +=
                OnPlayerReady;

            tickListenerId =
                api.Event.RegisterGameTickListener(
                    ProcessPendingRegions,
                    TickIntervalMs
                );

            CaptureCurrentlyLoadedRegions();
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }

            started = false;

            api.Event.MapRegionLoaded -=
                OnMapRegionLoaded;

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
        }

        public int RequestBackfillAround(
            int blockX,
            int blockZ
        )
        {
            return RequestBackfillAround(
                blockX,
                blockZ,
                config.BackfillRadiusRegions
            );
        }

        public int RequestBackfillAround(
            int blockX,
            int blockZ,
            int radiusRegions
        )
        {
            if (!started || !config.Enabled)
            {
                return 0;
            }

            long centerRegionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    blockX,
                    blockZ
                );

            Vec3i centerRegion =
                api.WorldManager.MapRegionPosFromIndex2D(
                    centerRegionIndex
                );

            int radius = Math.Max(0, radiusRegions);
            List<Vec2i> candidates = new();

            for (int deltaX = -radius;
                deltaX <= radius;
                deltaX++)
            {
                for (int deltaZ = -radius;
                    deltaZ <= radius;
                    deltaZ++)
                {
                    int regionX = centerRegion.X + deltaX;
                    int regionZ = centerRegion.Z + deltaZ;

                    if (!IsRegionInsideWorld(
                            regionX,
                            regionZ
                        ))
                    {
                        continue;
                    }

                    candidates.Add(
                        new Vec2i(regionX, regionZ)
                    );
                }
            }

            candidates.Sort(
                (left, right) =>
                    DistanceSquared(
                        left,
                        centerRegion.X,
                        centerRegion.Z
                    ).CompareTo(
                        DistanceSquared(
                            right,
                            centerRegion.X,
                            centerRegion.Z
                        )
                    )
            );

            int queuedCount = 0;

            foreach (Vec2i candidate in candidates)
            {
                long regionKey = CreateRegionKey(
                    candidate.X,
                    candidate.Y
                );

                if (
                    scannedRegionKeys.Contains(regionKey) ||
                    pendingRegionKeys.Contains(regionKey) ||
                    activeRegionKeys.Contains(regionKey)
                )
                {
                    continue;
                }

                pendingRegions.Enqueue(candidate);
                pendingRegionKeys.Add(regionKey);
                queuedCount++;
            }

            return queuedCount;
        }

        private void OnPlayerReady(
            IServerPlayer player
        )
        {
            if (!config.ScanOnPlayerReady)
            {
                return;
            }

            RequestBackfillAround(
                (int)player.Entity.Pos.X,
                (int)player.Entity.Pos.Z
            );
        }

        private void OnMapRegionLoaded(
            Vec2i mapCoord,
            IMapRegion region
        )
        {
            ScheduleLoadedRegionIndex(
                mapCoord.X,
                mapCoord.Y
            );
        }

        private void OnChunkColumnLoaded(
            Vec2i chunkCoord,
            IWorldChunk[] chunks
        )
        {
            int regionX = FloorDivide(
                chunkCoord.X,
                chunksPerRegion
            );

            int regionZ = FloorDivide(
                chunkCoord.Y,
                chunksPerRegion
            );

            ScheduleLoadedRegionIndex(
                regionX,
                regionZ
            );
        }

        private void ScheduleLoadedRegionIndex(
            int regionX,
            int regionZ
        )
        {
            long regionKey = CreateRegionKey(
                regionX,
                regionZ
            );

            lock (scheduledRegionLock)
            {
                if (!scheduledLoadedRegions.Add(regionKey))
                {
                    return;
                }
            }

            api.Event.EnqueueMainThreadTask(
                () =>
                {
                    lock (scheduledRegionLock)
                    {
                        scheduledLoadedRegions.Remove(
                            regionKey
                        );
                    }

                    IMapRegion region =
                        api.WorldManager.GetMapRegion(
                            regionX,
                            regionZ
                        );

                    if (region != null)
                    {
                        IndexRegion(
                            regionX,
                            regionZ,
                            region
                        );
                    }
                },
                "rumornetwork-index-loaded-region"
            );
        }

        private void CaptureCurrentlyLoadedRegions()
        {
            Dictionary<long, IMapRegion> loadedRegions =
                api.WorldManager.AllLoadedMapRegions;

            foreach (
                KeyValuePair<long, IMapRegion> pair
                in loadedRegions
            )
            {
                Vec3i regionPosition =
                    api.WorldManager.MapRegionPosFromIndex2D(
                        pair.Key
                    );

                IndexRegion(
                    regionPosition.X,
                    regionPosition.Z,
                    pair.Value
                );
            }
        }

        private void ProcessPendingRegions(
            float deltaTime
        )
        {
            if (!started)
            {
                return;
            }

            while (
                activeRegionChecks <
                    config.ConcurrentRegionChecks &&
                pendingRegions.Count > 0
            )
            {
                Vec2i region = pendingRegions.Dequeue();
                long regionKey = CreateRegionKey(
                    region.X,
                    region.Y
                );

                pendingRegionKeys.Remove(regionKey);
                activeRegionKeys.Add(regionKey);
                activeRegionChecks++;

                BeginRegionExistenceCheck(region);
            }
        }

        private void BeginRegionExistenceCheck(
            Vec2i region
        )
        {
            try
            {
                api.WorldManager.TestMapRegionExists(
                    region.X,
                    region.Y,
                    exists =>
                        api.Event.EnqueueMainThreadTask(
                            () => OnRegionExistenceTested(
                                region,
                                exists
                            ),
                            "rumornetwork-test-map-region"
                        )
                );
            }
            catch (Exception exception)
            {
                logger.Warning(
                    "Rumor Network não conseguiu testar " +
                    $"a região {region.X},{region.Y}: " +
                    exception.GetBaseException().Message
                );

                CompleteRegionCheck(
                    region,
                    false
                );
            }
        }

        private void OnRegionExistenceTested(
            Vec2i region,
            bool exists
        )
        {
            if (!exists)
            {
                CompleteRegionCheck(
                    region,
                    true
                );

                return;
            }

            IMapRegion loadedRegion =
                api.WorldManager.GetMapRegion(
                    region.X,
                    region.Y
                );

            if (loadedRegion != null)
            {
                IndexRegion(
                    region.X,
                    region.Y,
                    loadedRegion
                );

                CompleteRegionCheck(
                    region,
                    true
                );

                return;
            }

            FindExistingMapChunk(
                region,
                0
            );
        }

        private void FindExistingMapChunk(
            Vec2i region,
            int searchIndex
        )
        {
            if (searchIndex >= localChunkSearchOrder.Count)
            {
                CompleteRegionCheck(
                    region,
                    true
                );

                return;
            }

            Vec2i localChunk =
                localChunkSearchOrder[searchIndex];

            int chunkX =
                region.X * chunksPerRegion +
                localChunk.X;

            int chunkZ =
                region.Y * chunksPerRegion +
                localChunk.Y;

            try
            {
                api.WorldManager.TestMapChunkExists(
                    chunkX,
                    chunkZ,
                    exists =>
                        api.Event.EnqueueMainThreadTask(
                            () =>
                            {
                                if (exists)
                                {
                                    LoadExistingChunk(
                                        region,
                                        chunkX,
                                        chunkZ
                                    );
                                }
                                else
                                {
                                    FindExistingMapChunk(
                                        region,
                                        searchIndex + 1
                                    );
                                }
                            },
                            "rumornetwork-test-map-chunk"
                        )
                );
            }
            catch (Exception exception)
            {
                logger.Warning(
                    "Rumor Network não conseguiu testar " +
                    $"o map chunk {chunkX},{chunkZ}: " +
                    exception.GetBaseException().Message
                );

                CompleteRegionCheck(
                    region,
                    false
                );
            }
        }

        private void LoadExistingChunk(
            Vec2i region,
            int chunkX,
            int chunkZ
        )
        {
            try
            {
                ChunkLoadOptions options = new()
                {
                    KeepLoaded = false
                };

                options.OnLoaded = () =>
                    api.Event.EnqueueMainThreadTask(
                        () => OnBackfillChunkLoaded(
                            region
                        ),
                        "rumornetwork-load-existing-chunk"
                    );

                api.WorldManager.LoadChunkColumnPriority(
                    chunkX,
                    chunkZ,
                    options
                );
            }
            catch (Exception exception)
            {
                logger.Warning(
                    "Rumor Network não conseguiu carregar " +
                    $"temporariamente o chunk {chunkX},{chunkZ}: " +
                    exception.GetBaseException().Message
                );

                CompleteRegionCheck(
                    region,
                    false
                );
            }
        }

        private void OnBackfillChunkLoaded(
            Vec2i region
        )
        {
            IMapRegion loadedRegion =
                api.WorldManager.GetMapRegion(
                    region.X,
                    region.Y
                );

            if (loadedRegion == null)
            {
                logger.Warning(
                    "Rumor Network carregou um chunk existente, " +
                    "mas a map region correspondente continuou " +
                    $"indisponível: {region.X},{region.Y}."
                );

                CompleteRegionCheck(
                    region,
                    false
                );

                return;
            }

            IndexRegion(
                region.X,
                region.Y,
                loadedRegion
            );

            CompleteRegionCheck(
                region,
                true
            );
        }

        private void CompleteRegionCheck(
            Vec2i region,
            bool markScanned
        )
        {
            long regionKey = CreateRegionKey(
                region.X,
                region.Y
            );

            if (markScanned)
            {
                scannedRegionKeys.Add(regionKey);
            }

            activeRegionKeys.Remove(regionKey);

            if (activeRegionChecks > 0)
            {
                activeRegionChecks--;
            }
        }

        private void IndexRegion(
            int regionX,
            int regionZ,
            IMapRegion region
        )
        {
            List<GeneratedStructure> structures =
                region.GeneratedStructures == null
                    ? new List<GeneratedStructure>()
                    : new List<GeneratedStructure>(
                        region.GeneratedStructures
                    );

            List<RumorSite> builtSites =
                RumorSiteBuilder.Build(structures);

            List<RumorSite> selectedSites = new();

            foreach (RumorSite site in builtSites)
            {
                if (IsCatalogKind(site.Kind))
                {
                    selectedSites.Add(site);
                }
            }

            int addedCount =
                rumorRegistry.Merge(selectedSites);

            scannedRegionKeys.Add(
                CreateRegionKey(
                    regionX,
                    regionZ
                )
            );

            if (addedCount <= 0)
            {
                return;
            }

            int traderCount =
                rumorRegistry.CountByKind(
                    StructureKind.Trader
                );

            int translocatorCount =
                rumorRegistry.CountByKind(
                    StructureKind.Translocator
                );

            logger.Notification(
                "Rumor Network catalogou automaticamente " +
                $"{addedCount} localizações remotas na região " +
                $"{regionX},{regionZ}. " +
                $"Traders={traderCount} | " +
                $"Translocators={translocatorCount}."
            );
        }

        private bool IsRegionInsideWorld(
            int regionX,
            int regionZ
        )
        {
            return
                regionX >= 0 &&
                regionZ >= 0 &&
                regionX < regionCountX &&
                regionZ < regionCountZ;
        }

        private static bool IsCatalogKind(
            StructureKind kind
        )
        {
            return
                kind == StructureKind.Trader ||
                kind == StructureKind.Translocator;
        }

        private static List<Vec2i>
            CreateLocalChunkSearchOrder(
                int chunksPerRegion
            )
        {
            List<Vec2i> chunks = new();
            double center = (chunksPerRegion - 1) / 2d;

            for (int chunkX = 0;
                chunkX < chunksPerRegion;
                chunkX++)
            {
                for (int chunkZ = 0;
                    chunkZ < chunksPerRegion;
                    chunkZ++)
                {
                    chunks.Add(
                        new Vec2i(chunkX, chunkZ)
                    );
                }
            }

            chunks.Sort(
                (left, right) =>
                {
                    double leftX = left.X - center;
                    double leftZ = left.Y - center;
                    double rightX = right.X - center;
                    double rightZ = right.Y - center;

                    double leftDistance =
                        leftX * leftX +
                        leftZ * leftZ;

                    double rightDistance =
                        rightX * rightX +
                        rightZ * rightZ;

                    return leftDistance.CompareTo(
                        rightDistance
                    );
                }
            );

            return chunks;
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

        private static long CreateRegionKey(
            int regionX,
            int regionZ
        )
        {
            return
                ((long)regionX << 32) |
                (uint)regionZ;
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
    }
}
