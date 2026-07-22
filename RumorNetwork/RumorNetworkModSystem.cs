using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork
{
    public class RumorNetworkModSystem : ModSystem
    {

        private readonly List<GeneratedStructure> lastInspection = new();
        private int RegionSearchRadius { get; set; } = 1;

        private int MaximumRegionCount =>
            (RegionSearchRadius * 2 + 1) *
            (RegionSearchRadius * 2 + 1);

        private const string RumorRegistrySaveKey = "rumornetwork:registry-v1";

        private readonly RumorRegistry rumorRegistry = new();

        private ICoreServerAPI serverApi = null!;

        private readonly RumorTargetResolver rumorTargetResolver = new();

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;

            api.Event.SaveGameLoaded +=
                OnSaveGameLoaded;

            api.Event.GameWorldSave +=
                OnGameWorldSave;

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

            rumorCommand
                .BeginSubCommand("sites")
                .WithDescription(
                    "Lista os locais elegíveis para rumores na região atual."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => ListRumorSites(api, args))
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("index")
                .WithDescription(
                    "Adiciona ao registro persistente os locais " +
                    "elegíveis das regiões carregadas."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args =>
                    IndexRumorSites(api, args)
                )
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

            rumorCommand
                .BeginSubCommand("draw")
                .WithDescription(
                    "Sorteia um rumor ainda não vendido."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    api.ChatCommands.Parsers.Word("knowledge")
                )
                .HandleWith(args =>
                    DrawRumor(api, args)
                )
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

        private TextCommandResult ListRumorSites(
            ICoreServerAPI api,
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
            RegionSearchRadius,
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

            Mod.Logger.Notification(
                $"=== Rumor Network: locais vendáveis | " +
                $"Regions={loadedRegionCount}/{MaximumRegionCount} | " +
                $"Structures={structures.Count} ==="
            );

            for (int index = 0; index < sites.Count; index++)
            {
                RumorSite site = sites[index];
                Cuboidi box = site.Location;
                Vec3i center = site.Center;

                Mod.Logger.Notification(
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

        private TextCommandResult IndexRumorSites(
            ICoreServerAPI api,
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
                        RegionSearchRadius,
                        out int loadedRegionCount
                    );

            List<RumorSite> sites =
                RumorSiteBuilder.Build(
                    structures
                );

            int addedCount =
                rumorRegistry.Merge(sites);

            Mod.Logger.Notification(
                $"=== Rumor Network: indexação | " +
                $"Regions={loadedRegionCount}/" +
                $"{MaximumRegionCount} | " +
                $"Structures={structures.Count} | " +
                $"Sites={sites.Count} | " +
                $"Added={addedCount} | " +
                $"Registry={rumorRegistry.Count} ==="
            );

            return TextCommandResult.Success(
                $"{addedCount} novos locais adicionados. " +
                $"Registro total: {rumorRegistry.Count}."
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

            Mod.Logger.Notification(
                "=== Rumor Network: registro persistente ==="
            );

            Mod.Logger.Notification(
                $"Total: {rumorRegistry.Count}"
            );

            Mod.Logger.Notification(
                $"NotSold: {notSold}"
            );

            Mod.Logger.Notification(
                $"Approximate: {approximate}"
            );

            Mod.Logger.Notification(
                $"Exact: {exact}"
            );

            return TextCommandResult.Success(
                $"{rumorRegistry.Count} rumores registrados."
            );
        }

        private TextCommandResult DrawRumor(
            ICoreServerAPI api,
            TextCommandCallingArgs args
        )
        {
            string requestedKnowledge =
                ((string)args[0]).Trim();

            if (!TryParseKnowledgeLevel(
                    requestedKnowledge,
                    out RumorKnowledgeLevel knowledge
                ))
            {
                return TextCommandResult.Error(
                    "Tipo inválido. " +
                    "Use approximate ou exact."
                );
            }

            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return TextCommandResult.Error(
                    "O comando precisa ser " +
                    "executado por um jogador."
                );
            }

            bool selected =
                rumorRegistry.TryPickRandomNotSold(
                    api.World.Rand,
                    out RumorRecord? record
                );

            if (!selected || record == null)
            {
                return TextCommandResult.Error(
                    "Não existem rumores " +
                    "ainda não vendidos."
                );
            }

            bool targetResolved =
                rumorTargetResolver.TryResolve(
                    record,
                    out RumorTarget? target,
                    out string targetError
                );

            if (
                !targetResolved ||
                target == null
            )
            {
                return TextCommandResult.Error(
                    targetError
                );
            }

            bool waypointAdded =
                RumorWaypointService.TryAddWaypoint(
                    api,
                    player,
                    record,
                    knowledge,
                    target,
                    api.World.Rand,
                    out Vec3d waypointPosition,
                    out string waypointError
                );

            if (!waypointAdded)
            {
                return TextCommandResult.Error(
                    waypointError
                );
            }

            bool committed =
                rumorRegistry.TryMarkSold(
                    record.Id,
                    knowledge
                );

            if (!committed)
            {
                Mod.Logger.Error(
                    $"O waypoint do rumor {record.Id} " +
                    "foi criado, mas o registro não pôde " +
                    "ser marcado como vendido."
                );

                return TextCommandResult.Error(
                    "A localização foi adicionada ao mapa, " +
                    "mas o rumor não pôde ser registrado " +
                    "como vendido."
                );
            }

            Cuboidi box =
                record.CreateLocation();

            Vec3i center =
                box.Center;

            Mod.Logger.Notification(
                "=== Rumor Network: rumor sorteado ==="
            );

            Mod.Logger.Notification(
                $"Id={record.Id}"
            );

            Mod.Logger.Notification(
                $"Knowledge={knowledge} | " +
                $"Kind={record.Kind} | " +
                $"Family={record.Family} | " +
                $"Parts={record.PartCount}"
            );

            Mod.Logger.Notification(
                $"TrueCenter=(" +
                $"{center.X}; " +
                $"{center.Y}; " +
                $"{center.Z}) | " +
                $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            Mod.Logger.Notification(
                $"ResolvedTarget=(" +
                $"{target.Position.X:0.0}; " +
                $"{target.Position.Y:0.0}; " +
                $"{target.Position.Z:0.0}) | " +
                $"TargetKind={target.Kind}"
            );

            Mod.Logger.Notification(
                $"Waypoint=(" +
                $"{waypointPosition.X:0.0}; " +
                $"{waypointPosition.Y:0.0}; " +
                $"{waypointPosition.Z:0.0})"
            );

            string precisionText =
                knowledge
                == RumorKnowledgeLevel.Approximate
                    ? "aproximada"
                    : "exata";

            return TextCommandResult.Success(
                $"Rumor sorteado: {record.Kind}. " +
                $"Localização {precisionText} " +
                "adicionada ao mapa."
            );
        }
        private static bool TryParseKnowledgeLevel(
            string value,
            out RumorKnowledgeLevel knowledge
        )
        {
            switch (value.ToLowerInvariant())
            {
                case "approximate":
                case "approx":
                    knowledge =
                        RumorKnowledgeLevel.Approximate;

                    return true;

                case "exact":
                    knowledge =
                        RumorKnowledgeLevel.Exact;

                    return true;

                default:
                    knowledge =
                        RumorKnowledgeLevel.NotSold;

                    return false;
            }
        }
    }
}