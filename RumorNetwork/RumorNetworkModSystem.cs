using RumorNetwork.Catalog;
using RumorNetwork.Caves;
using RumorNetwork.Commands;
using RumorNetwork.Configuration;
using RumorNetwork.Dialogue;
using RumorNetwork.Offers;
using RumorNetwork.Purchases;
using RumorNetwork.Rumors;
using RumorNetwork.Traders;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork
{
    public class RumorNetworkModSystem : ModSystem
    {
        private const string RumorRegistrySaveKey =
            "rumornetwork:registry-v1";

        private const string TraderKnowledgeSaveKey =
            "rumornetwork:trader-knowledge-v1";

        private const string VerifiedDiscoverySaveKey =
            "rumornetwork:verified-discovery-v1";

        private readonly RumorRegistry rumorRegistry = new();
        private readonly TraderKnowledgeRegistry traderKnowledgeRegistry = new();

        private ICoreServerAPI serverApi = null!;
        private RumorTargetResolver rumorTargetResolver = null!;
        private RumorDeliveryService rumorDeliveryService = null!;
        private RumorPurchaseService rumorPurchaseService = null!;
        private TranslocatorPurchaseService translocatorPurchaseService = null!;
        private RumorOfferService rumorOfferService = null!;
        private TraderLocationPurchaseService traderLocationPurchaseService = null!;
        private VerifiedStructureDiscoveryService discoveryService = null!;
        private CaveCellClassifier caveCellClassifier = null!;
        private CaveBoundaryScanner caveBoundaryScanner = null!;
        private CaveSkyConnectionSearch caveSkyConnectionSearch = null!;

        private int RegionSearchRadius { get; set; } = 1;

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;

            RumorNetworkConfig config =
                RumorConfigLoader.Load(
                    api,
                    Mod.Logger
                );

            caveCellClassifier =
                new CaveCellClassifier(
                    api.World.BlockAccessor
                );

            caveBoundaryScanner =
                new CaveBoundaryScanner(
                    api.World.BlockAccessor,
                    caveCellClassifier
                );

            caveSkyConnectionSearch =
                new CaveSkyConnectionSearch(
                    api.World.BlockAccessor,
                    caveCellClassifier
                );

            rumorTargetResolver =
                new RumorTargetResolver(
                    caveBoundaryScanner,
                    caveSkyConnectionSearch
                );

            rumorDeliveryService = new RumorDeliveryService(
                api,
                Mod.Logger,
                rumorRegistry,
                rumorTargetResolver
            );

            RumorPriceResolver priceResolver =
                new(api.World, config);

            RumorInventoryPaymentService paymentService =
                new(api, Mod.Logger);

            discoveryService =
                new VerifiedStructureDiscoveryService(
                    api,
                    Mod.Logger,
                    rumorRegistry,
                    config.RemoteCatalog
                );

            rumorPurchaseService = new RumorPurchaseService(
                api,
                rumorRegistry,
                RegionSearchRadius,
                rumorDeliveryService,
                priceResolver,
                paymentService
            );

            translocatorPurchaseService =
                new TranslocatorPurchaseService(
                    api,
                    rumorRegistry,
                    rumorTargetResolver,
                    priceResolver,
                    paymentService,
                    discoveryService
                );

            rumorOfferService =
                new RumorOfferService(
                    config,
                    priceResolver
                );

            TraderLocationSelector traderSelector =
                new(rumorRegistry);

            traderLocationPurchaseService =
                new TraderLocationPurchaseService(
                    api,
                    Mod.Logger,
                    config,
                    rumorTargetResolver,
                    priceResolver,
                    paymentService,
                    traderKnowledgeRegistry,
                    traderSelector,
                    discoveryService
                );

            RumorDialogueRuntime.Configure(
                rumorPurchaseService,
                translocatorPurchaseService,
                traderLocationPurchaseService,
                rumorRegistry
            );

            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameWorldSave;

            RumorCommandRegistrar.Register(
                api,
                Mod.Logger,
                rumorRegistry,
                rumorTargetResolver,
                rumorDeliveryService,
                rumorPurchaseService,
                rumorOfferService,
                traderLocationPurchaseService,
                traderKnowledgeRegistry,
                discoveryService,
                caveCellClassifier,
                caveBoundaryScanner,
                caveSkyConnectionSearch,
                RegionSearchRadius
            );

            Mod.Logger.Notification(
                "Rumor Network carregado no servidor."
            );
        }

        public override void Dispose()
        {
            RumorDialogueRuntime.Reset();
            discoveryService?.Stop();
            base.Dispose();
        }

        private void OnSaveGameLoaded()
        {
            RumorRegistrySaveData rumorSaveData =
                serverApi.WorldManager.SaveGame.GetData(
                    RumorRegistrySaveKey,
                    new RumorRegistrySaveData()
                );

            rumorRegistry.Import(rumorSaveData);

            TraderKnowledgeSaveData traderSaveData =
                serverApi.WorldManager.SaveGame.GetData(
                    TraderKnowledgeSaveKey,
                    new TraderKnowledgeSaveData()
                );

            traderKnowledgeRegistry.Import(
                traderSaveData
            );

            VerifiedStructureDiscoverySaveData
                discoverySaveData =
                    serverApi.WorldManager.SaveGame.GetData(
                        VerifiedDiscoverySaveKey,
                        new VerifiedStructureDiscoverySaveData()
                    );

            discoveryService.Import(discoverySaveData);
            discoveryService.Start();

            Mod.Logger.Notification(
                $"Rumor Network carregou " +
                $"{rumorRegistry.Count} rumores persistidos."
            );
        }

        private void OnGameWorldSave()
        {
            serverApi.WorldManager.SaveGame.StoreData(
                RumorRegistrySaveKey,
                rumorRegistry.Export()
            );

            serverApi.WorldManager.SaveGame.StoreData(
                TraderKnowledgeSaveKey,
                traderKnowledgeRegistry.Export()
            );

            serverApi.WorldManager.SaveGame.StoreData(
                VerifiedDiscoverySaveKey,
                discoveryService.Export()
            );
        }
    }
}
