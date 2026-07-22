using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class RumorRegistryCommands
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorRegistry rumorRegistry;
        private readonly int regionSearchRadius;

        private int MaximumRegionCount =>
            (regionSearchRadius * 2 + 1) *
            (regionSearchRadius * 2 + 1);

        public RumorRegistryCommands(
            ICoreServerAPI api,
            ILogger logger,
            RumorRegistry rumorRegistry,
            int regionSearchRadius
        )
        {
            this.api = api;
            this.logger = logger;
            this.rumorRegistry = rumorRegistry;
            this.regionSearchRadius = regionSearchRadius;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("index")
                .WithDescription(
                    "Adiciona ao registro persistente os locais " +
                    "elegíveis das regiões carregadas."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(IndexRumorSites)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("registry")
                .WithDescription(
                    "Mostra o estado do registro persistente de rumores."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ShowRumorRegistry)
                .EndSubCommand();
        }

        private TextCommandResult IndexRumorSites(
            TextCommandCallingArgs args
        )
        {
            BlockPos playerPos =
                args.Caller.Entity.Pos.AsBlockPos;

            List<GeneratedStructure> structures =
                MapRegionStructureCollector
                    .CollectLoadedNeighborhood(
                        api,
                        playerPos,
                        regionSearchRadius,
                        out int loadedRegionCount
                    );

            List<RumorSite> sites =
                RumorSiteBuilder.Build(
                    structures
                );

            int addedCount =
                rumorRegistry.Merge(sites);

            int traderCount =
                rumorRegistry.CountByKind(
                    StructureKind.Trader
                );

            int translocatorCount =
                rumorRegistry.CountByKind(
                    StructureKind.Translocator
                );

            logger.Notification(
                $"=== Rumor Network: indexação | " +
                $"Regions={loadedRegionCount}/" +
                $"{MaximumRegionCount} | " +
                $"Structures={structures.Count} | " +
                $"Sites={sites.Count} | " +
                $"Added={addedCount} | " +
                $"Registry={rumorRegistry.Count} | " +
                $"Traders={traderCount} | " +
                $"Translocators={translocatorCount} ==="
            );

            return TextCommandResult.Success(
                $"{addedCount} novos locais adicionados. " +
                $"Registro total: {rumorRegistry.Count}. " +
                $"Traders: {traderCount}. " +
                $"Translocators: {translocatorCount}."
            );
        }

        private TextCommandResult ShowRumorRegistry(
            TextCommandCallingArgs args
        )
        {
            int notSold =
                rumorRegistry.CountByKnowledge(
                    RumorKnowledgeLevel.NotSold
                );

            int approximate =
                rumorRegistry.CountByKnowledge(
                    RumorKnowledgeLevel.Approximate
                );

            int exact =
                rumorRegistry.CountByKnowledge(
                    RumorKnowledgeLevel.Exact
                );

            int traders =
                rumorRegistry.CountByKind(
                    StructureKind.Trader
                );

            int translocators =
                rumorRegistry.CountByKind(
                    StructureKind.Translocator
                );

            logger.Notification(
                "=== Rumor Network: registro persistente ==="
            );

            logger.Notification(
                $"Total: {rumorRegistry.Count}"
            );

            logger.Notification(
                $"Traders: {traders}"
            );

            logger.Notification(
                $"Translocators: {translocators}"
            );

            logger.Notification(
                $"NotSold: {notSold}"
            );

            logger.Notification(
                $"Approximate: {approximate}"
            );

            logger.Notification(
                $"Exact: {exact}"
            );

            return TextCommandResult.Success(
                $"{rumorRegistry.Count} rumores registrados. " +
                $"Traders={traders} | " +
                $"Translocators={translocators}."
            );
        }
    }
}
