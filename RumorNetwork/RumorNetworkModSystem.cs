using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using RumorNetwork.Structures;

namespace RumorNetwork
{
    public class RumorNetworkModSystem : ModSystem
    {
        private readonly List<GeneratedStructure> lastInspection = new();

        public override void StartServerSide(ICoreServerAPI api)
        {
            IChatCommand rumorCommand = api.ChatCommands
                .Create("rumor")
                .WithDescription("Comandos do Rumor Network.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(_ =>
                    TextCommandResult.Success(
                        "Rumor Network está funcionando."
                    )
                );

            rumorCommand
                .BeginSubCommand("dump")
                .WithDescription(
                    "Lista no log as estruturas registradas na região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => DumpCurrentRegion(api, args))
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("inspect")
                .WithDescription(
                    "Lista estruturas da região atual cujo código ou grupo contenha o filtro."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    api.ChatCommands.Parsers.All("filter")
                )
                .HandleWith(args => InspectCurrentRegion(api, args))
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("goto")
                .WithDescription(
                    "Teleporta para uma estrutura retornada pelo último inspect."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.tp)
                .WithArgs(
                    api.ChatCommands.Parsers.IntRange(
                        "index",
                        0,
                        int.MaxValue
                    )
                )
                .HandleWith(args => GoToInspectedStructure(args))
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("classify")
                .WithDescription(
                    "Classifica as estruturas registradas na região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => ClassifyCurrentRegion(api, args))
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("group")
                .WithDescription(
                    "Agrupa estruturas compostas da região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => GroupCurrentRegion(api, args))
                .EndSubCommand();

            Mod.Logger.Notification(
                "Rumor Network carregado no servidor."
            );
        }

        private TextCommandResult DumpCurrentRegion(
            ICoreServerAPI api,
            TextCommandCallingArgs args
        )
        {
            BlockPos playerPos = args.Caller.Entity.Pos.AsBlockPos;

            long regionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    playerPos.X,
                    playerPos.Z
                );

            IMapRegion region =
                api.WorldManager.GetMapRegion(regionIndex);

            if (region == null)
            {
                return TextCommandResult.Error(
                    "A região atual não está carregada."
                );
            }

            var structures = region.GeneratedStructures;

            Mod.Logger.Notification(
                $"=== Rumor Network: região do jogador em " +
                $"{playerPos.X}, {playerPos.Y}, {playerPos.Z} ==="
            );

            foreach (GeneratedStructure structure in structures)
            {
                Cuboidi box = structure.Location;
                Vec3i center = box.Center;

                Mod.Logger.Notification(
                    $"Code={structure.Code ?? "(sem código)"} | " +
                    $"Group={structure.Group ?? "(sem grupo)"} | " +
                    $"Center={center.X},{center.Y},{center.Z} | " +
                    $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                    $"({box.X2},{box.Y2},{box.Z2})"
                );
            }

            Mod.Logger.Notification(
                $"=== Total: {structures.Count} estruturas ==="
            );

            return TextCommandResult.Success(
                $"{structures.Count} estruturas encontradas. " +
                "Veja o Output/console do servidor."
            );
        }
        private TextCommandResult InspectCurrentRegion(
            ICoreServerAPI api,
            TextCommandCallingArgs args
        )
        {
            string filter = ((string)args[0]).Trim();

            BlockPos playerPos = args.Caller.Entity.Pos.AsBlockPos;

            long regionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    playerPos.X,
                    playerPos.Z
                );

            IMapRegion region =
                api.WorldManager.GetMapRegion(regionIndex);

            if (region == null)
            {
                return TextCommandResult.Error(
                    "A região atual não está carregada."
                );
            }

            lastInspection.Clear();

            foreach (GeneratedStructure structure in region.GeneratedStructures)
            {
                bool codeMatches =
                    structure.Code?.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    ) == true;

                bool groupMatches =
                    structure.Group?.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    ) == true;

                if (codeMatches || groupMatches)
                {
                    lastInspection.Add(structure);
                }
            }

            Mod.Logger.Notification(
                $"=== Rumor inspect: \"{filter}\" ==="
            );

            for (int index = 0; index < lastInspection.Count; index++)
            {
                GeneratedStructure structure = lastInspection[index];
                Cuboidi box = structure.Location;
                Vec3i center = box.Center;

                Mod.Logger.Notification(
                    $"[{index}] " +
                    $"Code={structure.Code ?? "(sem código)"} | " +
                    $"Group={structure.Group ?? "(sem grupo)"} | " +
                    $"Center={center.X},{center.Y},{center.Z} | " +
                    $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                    $"({box.X2},{box.Y2},{box.Z2})"
                );
            }

            return TextCommandResult.Success(
                $"{lastInspection.Count} estruturas encontradas. " +
                $"Use /rumor goto [índice]."
            );
        }

        private TextCommandResult GoToInspectedStructure(
            TextCommandCallingArgs args
        )
        {
            int index = (int)args[0];

            if (index < 0 || index >= lastInspection.Count)
            {
                return TextCommandResult.Error(
                    $"Índice inválido. A última inspeção possui " +
                    $"{lastInspection.Count} resultados."
                );
            }

            GeneratedStructure structure = lastInspection[index];
            Cuboidi box = structure.Location;
            Vec3i center = box.Center;

            double targetX = center.X + 0.5;
            double targetY = box.Y2 + 2;
            double targetZ = center.Z + 0.5;

            args.Caller.Entity.TeleportToDouble(
                targetX,
                targetY,
                targetZ
            );

            return TextCommandResult.Success(
                $"Indo para [{index}] {structure.Code}. " +
                $"Centro: {center.X}, {center.Y}, {center.Z}."
            );
        }

        private TextCommandResult ClassifyCurrentRegion(
            ICoreServerAPI api,
            TextCommandCallingArgs args
        )
        {
            BlockPos playerPos = args.Caller.Entity.Pos.AsBlockPos;

            long regionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    playerPos.X,
                    playerPos.Z
                );

            IMapRegion region =
                api.WorldManager.GetMapRegion(regionIndex);

            if (region == null)
            {
                return TextCommandResult.Error(
                    "A região atual não está carregada."
                );
            }

            Dictionary<StructureKind, int> totals = new();

            foreach (GeneratedStructure structure in region.GeneratedStructures)
            {
                StructureKind kind =
                    StructureClassifier.Classify(structure);

                totals.TryGetValue(kind, out int current);
                totals[kind] = current + 1;

                if (kind == StructureKind.Unknown)
                {
                    Cuboidi box = structure.Location;
                    Vec3i center = box.Center;

                    Mod.Logger.Notification(
                        $"UNKNOWN | " +
                        $"Code={structure.Code ?? "(sem código)"} | " +
                        $"Group={structure.Group ?? "(sem grupo)"} | " +
                        $"Center={center.X},{center.Y},{center.Z}"
                    );
                }
            }

            Mod.Logger.Notification(
                "=== Rumor Network: classificação da região ==="
            );

            foreach (StructureKind kind in Enum.GetValues<StructureKind>())
            {
                totals.TryGetValue(kind, out int count);

                Mod.Logger.Notification(
                    $"{kind}: {count}"
                );
            }

            return TextCommandResult.Success(
                $"{region.GeneratedStructures.Count} estruturas classificadas. " +
                "Veja o console."
            );
        }

        private TextCommandResult GroupCurrentRegion(
            ICoreServerAPI api,
            TextCommandCallingArgs args
        )
        {
            BlockPos playerPos =
                args.Caller.Entity.Pos.AsBlockPos;

            long regionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(
                    playerPos.X,
                    playerPos.Z
                );

            IMapRegion region =
                api.WorldManager.GetMapRegion(regionIndex);

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

            Mod.Logger.Notification(
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

                Mod.Logger.Notification(
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

    }
}