using RumorNetwork.Caves;
using RumorNetwork.Commands;
using RumorNetwork.Configuration;
using RumorNetwork.Purchases;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork
{
    public class RumorNetworkModSystem : ModSystem
    {
        private const string RumorRegistrySaveKey =
            "rumornetwork:registry-v1";

        private readonly RumorRegistry rumorRegistry =
            new();

        private ICoreServerAPI serverApi = null!;
        private RumorTargetResolver rumorTargetResolver = null!;
        private RumorDeliveryService rumorDeliveryService = null!;
        private RumorPurchaseService rumorPurchaseService = null!;
        private CaveCellClassifier caveCellClassifier = null!;
        private CaveBoundaryScanner caveBoundaryScanner = null!;
        private CaveSkyConnectionSearch caveSkyConnectionSearch = null!;

        private int RegionSearchRadius { get; set; } = 1;

        public override void StartServerSide(
            ICoreServerAPI api
        )
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

            rumorDeliveryService =
                new RumorDeliveryService(
                    api,
                    Mod.Logger,
                    rumorRegistry,
                    rumorTargetResolver
                );

            RumorPriceResolver priceResolver =
                new(
                    api.World,
                    config
                );

            RumorInventoryPaymentService
                paymentService =
                    new(
                        api,
                        Mod.Logger
                    );

            rumorPurchaseService =
                new RumorPurchaseService(
                    rumorDeliveryService,
                    priceResolver,
                    paymentService
                );

            api.Event.SaveGameLoaded +=
                OnSaveGameLoaded;

            api.Event.GameWorldSave +=
                OnGameWorldSave;

            RumorCommandRegistrar.Register(
                api,
                Mod.Logger,
                rumorRegistry,
                rumorTargetResolver,
                rumorDeliveryService,
                rumorPurchaseService,
                caveCellClassifier,
                caveBoundaryScanner,
                caveSkyConnectionSearch,
                RegionSearchRadius
            );

            Mod.Logger.Notification(
                "Rumor Network carregado no servidor."
            );
        }

        private void OnSaveGameLoaded()
        {
            RumorRegistrySaveData saveData =
                serverApi.WorldManager.SaveGame.GetData(
                    RumorRegistrySaveKey,
                    new RumorRegistrySaveData()
                );

            rumorRegistry.Import(saveData);

            Mod.Logger.Notification(
                $"Rumor Network carregou " +
                $"{rumorRegistry.Count} rumores persistidos."
            );
        }

        private void OnGameWorldSave()
        {
            RumorRegistrySaveData saveData =
                rumorRegistry.Export();

            serverApi.WorldManager.SaveGame.StoreData(
                RumorRegistrySaveKey,
                saveData
            );
        }
    }
}
