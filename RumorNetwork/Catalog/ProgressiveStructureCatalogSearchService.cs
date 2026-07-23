using System;
using System.Collections.Generic;
using RumorNetwork.Configuration;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Catalog
{
    public sealed class ProgressiveStructureCatalogSearchService
    {
        private const int TickIntervalMs = 100;
        private const int ExpansionStepRegions = 8;
        private const int MaximumExpansionRadiusRegions = 64;

        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorRegistry rumorRegistry;
        private readonly SelectiveStructureCatalogService catalogService;
        private readonly RemoteStructureCatalogConfig config;

        private readonly List<ProgressiveCatalogSearch> searches =
            new();

        private long tickListenerId;
        private bool started;

        public int ActiveSearchCount => searches.Count;

        public bool IsWorking =>
            catalogService.IsWorking || searches.Count > 0;

        public int LargestRequestedRadiusRegions
        {
            get
            {
                int largestRadius = 0;

                foreach (ProgressiveCatalogSearch search in searches)
                {
                    largestRadius = Math.Max(
                        largestRadius,
                        search.CurrentRadiusRegions
                    );
                }

                return largestRadius;
            }
        }

        public ProgressiveStructureCatalogSearchService(
            ICoreServerAPI api,
            ILogger logger,
            RumorRegistry rumorRegistry,
            SelectiveStructureCatalogService catalogService,
            RemoteStructureCatalogConfig config
        )
        {
            this.api = api;
            this.logger = logger;
            this.rumorRegistry = rumorRegistry;
            this.catalogService = catalogService;
            this.config = config;
        }

        public void Start()
        {
            if (started || !config.Enabled)
            {
                return;
            }

            started = true;
            tickListenerId = api.Event.RegisterGameTickListener(
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
            searches.Clear();

            if (tickListenerId != 0)
            {
                api.Event.UnregisterGameTickListener(
                    tickListenerId
                );

                tickListenerId = 0;
            }
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

            long centerRegionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    blockX,
                    blockZ
                );

            foreach (ProgressiveCatalogSearch search in searches)
            {
                if (
                    search.Kind == kind &&
                    search.CenterRegionIndex == centerRegionIndex
                )
                {
                    return false;
                }
            }

            int initialRadius = Math.Max(
                1,
                config.BackfillRadiusRegions
            );

            int maximumRadius = Math.Max(
                initialRadius,
                MaximumExpansionRadiusRegions
            );

            ProgressiveCatalogSearch newSearch = new(
                kind,
                blockX,
                blockZ,
                centerRegionIndex,
                rumorRegistry.CountByKind(kind),
                initialRadius,
                maximumRadius
            );

            searches.Add(newSearch);

            int queued = catalogService.RequestBackfillAround(
                blockX,
                blockZ,
                initialRadius
            );

            logger.Notification(
                "Rumor Network iniciou busca progressiva por " +
                $"{kind}. Raio inicial={initialRadius} regiões | " +
                $"Raio máximo={maximumRadius} | " +
                $"Regiões novas enfileiradas={queued}."
            );

            return true;
        }

        public bool IsSearching(
            StructureKind kind,
            int blockX,
            int blockZ
        )
        {
            long centerRegionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    blockX,
                    blockZ
                );

            foreach (ProgressiveCatalogSearch search in searches)
            {
                if (
                    search.Kind == kind &&
                    search.CenterRegionIndex == centerRegionIndex
                )
                {
                    return true;
                }
            }

            return false;
        }

        private void ProcessSearches(float deltaTime)
        {
            if (
                !started ||
                searches.Count == 0 ||
                catalogService.IsWorking
            )
            {
                return;
            }

            for (int index = searches.Count - 1;
                index >= 0;
                index--)
            {
                ProgressiveCatalogSearch search = searches[index];
                int currentCount =
                    rumorRegistry.CountByKind(search.Kind);

                if (currentCount > search.InitialKindCount)
                {
                    logger.Notification(
                        "Rumor Network concluiu busca progressiva " +
                        $"por {search.Kind}: " +
                        $"{currentCount - search.InitialKindCount} " +
                        "nova(s) localização(ões) catalogada(s). " +
                        $"Raio final={search.CurrentRadiusRegions} regiões."
                    );

                    searches.RemoveAt(index);
                    continue;
                }

                if (
                    search.CurrentRadiusRegions >=
                    search.MaximumRadiusRegions
                )
                {
                    logger.Notification(
                        "Rumor Network encerrou busca progressiva " +
                        $"por {search.Kind} sem encontrar novos alvos. " +
                        $"Raio máximo={search.MaximumRadiusRegions} regiões."
                    );

                    searches.RemoveAt(index);
                    continue;
                }

                int nextRadius = Math.Min(
                    search.MaximumRadiusRegions,
                    search.CurrentRadiusRegions +
                        ExpansionStepRegions
                );

                search.CurrentRadiusRegions = nextRadius;

                int queued = catalogService.RequestBackfillAround(
                    search.CenterBlockX,
                    search.CenterBlockZ,
                    nextRadius
                );

                logger.Notification(
                    "Rumor Network ampliou busca progressiva por " +
                    $"{search.Kind} para {nextRadius} regiões. " +
                    $"Regiões novas enfileiradas={queued}."
                );

                if (queued > 0)
                {
                    return;
                }
            }
        }

        private static bool IsSupportedKind(
            StructureKind kind
        )
        {
            return
                kind == StructureKind.Trader ||
                kind == StructureKind.Translocator;
        }

        private sealed class ProgressiveCatalogSearch
        {
            public StructureKind Kind { get; }

            public int CenterBlockX { get; }

            public int CenterBlockZ { get; }

            public long CenterRegionIndex { get; }

            public int InitialKindCount { get; }

            public int CurrentRadiusRegions { get; set; }

            public int MaximumRadiusRegions { get; }

            public ProgressiveCatalogSearch(
                StructureKind kind,
                int centerBlockX,
                int centerBlockZ,
                long centerRegionIndex,
                int initialKindCount,
                int currentRadiusRegions,
                int maximumRadiusRegions
            )
            {
                Kind = kind;
                CenterBlockX = centerBlockX;
                CenterBlockZ = centerBlockZ;
                CenterRegionIndex = centerRegionIndex;
                InitialKindCount = initialKindCount;
                CurrentRadiusRegions = currentRadiusRegions;
                MaximumRadiusRegions = maximumRadiusRegions;
            }
        }
    }
}
