using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class StructureRegionDebugCommands
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly int regionSearchRadius;

        private int MaximumRegionCount =>
            (regionSearchRadius * 2 + 1) *
            (regionSearchRadius * 2 + 1);

        public StructureRegionDebugCommands(
            ICoreServerAPI api,
            ILogger logger,
            int regionSearchRadius
        )
        {
            this.api = api;
            this.logger = logger;
            this.regionSearchRadius = regionSearchRadius;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("dump")
                .WithDescription(
                    "Lista no log as estruturas registradas na região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(DumpCurrentRegion)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("classify")
                .WithDescription(
                    "Classifica as estruturas registradas na região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ClassifyCurrentRegion)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("group")
                .WithDescription(
                    "Agrupa estruturas compostas da região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(GroupCurrentRegion)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("sites")
                .WithDescription(
                    "Lista os locais elegíveis para rumores na região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ListRumorSites)
                .EndSubCommand();
        }

        private TextCommandResult DumpCurrentRegion(
            TextCommandCallingArgs args
        )
        {
            BlockPos playerPos =
                args.Caller.Entity.Pos.AsBlockPos;

            IMapRegion? region =
                GetCurrentRegion(playerPos);

            if (region == null)
            {
                return TextCommandResult.Error(
                    "A região atual não está carregada."
                );
            }

            List<GeneratedStructure> structures =
                region.GeneratedStructures;

            logger.Notification(
                $"=== Rumor Network: região do jogador em " +
                $"{playerPos.X}, {playerPos.Y}, {playerPos.Z} ==="
            );

            foreach (GeneratedStructure structure in structures)
            {
                Cuboidi box = structure.Location;
                Vec3i center = box.Center;

                logger.Notification(
                    $"Code={structure.Code ?? "(sem código)"} | " +
                    $"Group={structure.Group ?? "(sem grupo)"} | " +
                    $"Center={center.X},{center.Y},{center.Z} | " +
                    $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                    $"({box.X2},{box.Y2},{box.Z2})"
                );
            }

            logger.Notification(
                $"=== Total: {structures.Count} estruturas ==="
            );

            return TextCommandResult.Success(
                $"{structures.Count} estruturas encontradas. " +
                "Veja o Output/console do servidor."
            );
        }

        private TextCommandResult ClassifyCurrentRegion(
            TextCommandCallingArgs args
        )
        {
            BlockPos playerPos =
                args.Caller.Entity.Pos.AsBlockPos;

            IMapRegion? region =
                GetCurrentRegion(playerPos);

            if (region == null)
            {
                return TextCommandResult.Error(
                    "A região atual não está carregada."
                );
            }

            Dictionary<StructureKind, int> totals = new();

            foreach (
                GeneratedStructure structure
                in region.GeneratedStructures
            )
            {
                StructureKind kind =
                    StructureClassifier.Classify(structure);

                totals.TryGetValue(
                    kind,
                    out int current
                );

                totals[kind] = current + 1;

                if (kind == StructureKind.Unknown)
                {
                    Cuboidi box = structure.Location;
                    Vec3i center = box.Center;

                    logger.Notification(
                        "UNKNOWN | " +
                        $"Code={structure.Code ?? "(sem código)"} | " +
                        $"Group={structure.Group ?? "(sem grupo)"} | " +
                        $"Center={center.X},{center.Y},{center.Z}"
                    );
                }
            }

            logger.Notification(
                "=== Rumor Network: classificação da região ==="
            );

            foreach (
                StructureKind kind
                in Enum.GetValues<StructureKind>()
            )
            {
                totals.TryGetValue(
                    kind,
                    out int count
                );

                logger.Notification(
                    $"{kind}: {count}"
                );
            }

            return TextCommandResult.Success(
                $"{region.GeneratedStructures.Count} estruturas classificadas. " +
                "Veja o console."
            );
        }

        private TextCommandResult GroupCurrentRegion(
            TextCommandCallingArgs args
        )
        {
            BlockPos playerPos =
                args.Caller.Entity.Pos.AsBlockPos;

            IMapRegion? region =
                GetCurrentRegion(playerPos);

            if (region == null)
            {
                return TextCommandResult.Error(
                    "A região atual não está carregada."
                );
            }

            List<GroupedStructure> villageGroups =
                StructureGrouper.GroupVillageParts(
                    region.GeneratedStructures
                );

            logger.Notification(
                "=== Rumor Network: grupos de vilas ==="
            );

            for (
                int groupIndex = 0;
                groupIndex < villageGroups.Count;
                groupIndex++
            )
            {
                GroupedStructure group =
                    villageGroups[groupIndex];

                Cuboidi box = group.Location;
                Vec3i center = box.Center;

                logger.Notification(
                    $"Village [{groupIndex}] | " +
                    $"Family={group.Family} | " +
                    $"Parts={group.Parts.Count} | " +
                    $"Center={center.X},{center.Y},{center.Z} | " +
                    $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                    $"({box.X2},{box.Y2},{box.Z2})"
                );
            }

            return TextCommandResult.Success(
                $"{villageGroups.Count} vilas encontradas. " +
                "Veja o console."
            );
        }

        private TextCommandResult ListRumorSites(
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

            if (loadedRegionCount == 0)
            {
                return TextCommandResult.Error(
                    "Nenhuma map region carregada foi encontrada."
                );
            }

            List<RumorSite> sites =
                RumorSiteBuilder.Build(
                    structures
                );

            logger.Notification(
                $"=== Rumor Network: locais vendáveis | " +
                $"Regions={loadedRegionCount}/{MaximumRegionCount} | " +
                $"Structures={structures.Count} ==="
            );

            for (
                int index = 0;
                index < sites.Count;
                index++
            )
            {
                RumorSite site = sites[index];
                Cuboidi box = site.Location;
                Vec3i center = site.Center;

                logger.Notification(
                    $"[{index}] " +
                    $"Kind={site.Kind} | " +
                    $"Family={site.Family} | " +
                    $"Parts={site.PartCount} | " +
                    $"Center={center.X},{center.Y},{center.Z} | " +
                    $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                    $"({box.X2},{box.Y2},{box.Z2}) | " +
                    $"Id={site.Id}"
                );
            }

            return TextCommandResult.Success(
                $"{sites.Count} locais elegíveis encontrados em " +
                $"{loadedRegionCount} de {MaximumRegionCount} regiões. " +
                "Veja o console."
            );
        }

        private IMapRegion? GetCurrentRegion(
            BlockPos playerPos
        )
        {
            long regionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    playerPos.X,
                    playerPos.Z
                );

            return api.WorldManager.GetMapRegion(
                regionIndex
            );
        }
    }
}
