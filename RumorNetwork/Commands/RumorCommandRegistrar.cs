using RumorNetwork.Catalog;
using RumorNetwork.Caves;
using RumorNetwork.Offers;
using RumorNetwork.Purchases;
using RumorNetwork.Rumors;
using RumorNetwork.Traders;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public static class RumorCommandRegistrar
    {
        public static void Register(
            ICoreServerAPI api,
            ILogger logger,
            RumorRegistry rumorRegistry,
            RumorTargetResolver rumorTargetResolver,
            RumorDeliveryService rumorDeliveryService,
            RumorPurchaseService rumorPurchaseService,
            RumorOfferService rumorOfferService,
            TraderLocationPurchaseService
                traderLocationPurchaseService,
            TraderKnowledgeRegistry traderKnowledgeRegistry,
            VerifiedStructureDiscoveryService
                discoveryService,
            CaveCellClassifier caveCellClassifier,
            CaveBoundaryScanner caveBoundaryScanner,
            CaveSkyConnectionSearch caveSkyConnectionSearch,
            int regionSearchRadius
        )
        {
            IChatCommand rumorCommand = api.ChatCommands
                .Create("rumor")
                .WithDescription(
                    "Comandos do Rumor Network."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(_ =>
                    TextCommandResult.Success(
                        "Rumor Network está funcionando."
                    )
                );

            StructureInspectionState inspectionState =
                new();

            StructureDebugOverlay debugOverlay =
                new(api);

            new StructureInspectionCommands(
                api,
                logger,
                rumorTargetResolver,
                inspectionState
            ).Register(rumorCommand);

            new StructureRegionDebugCommands(
                api,
                logger,
                regionSearchRadius
            ).Register(rumorCommand);

            new CaveDebugCommands(
                api,
                logger,
                api.World.BlockAccessor,
                caveCellClassifier,
                caveBoundaryScanner,
                inspectionState,
                debugOverlay
            ).Register(rumorCommand);

            new CaveSkyDebugCommands(
                api,
                logger,
                caveBoundaryScanner,
                caveSkyConnectionSearch,
                inspectionState
            ).Register(rumorCommand);

            new RumorRegistryCommands(
                api,
                logger,
                rumorRegistry,
                regionSearchRadius
            ).Register(rumorCommand);

            new RumorCatalogCommands(
                rumorRegistry,
                discoveryService
            ).Register(rumorCommand);

            new RumorDeliveryCommands(
                api,
                rumorDeliveryService,
                rumorPurchaseService
            ).Register(rumorCommand);

            new RumorOfferCommands(
                rumorOfferService,
                traderLocationPurchaseService,
                traderKnowledgeRegistry
            ).Register(rumorCommand);
        }
    }
}
