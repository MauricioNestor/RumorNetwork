using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class StructureInspectionCommands
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly RumorTargetResolver rumorTargetResolver;
        private readonly StructureInspectionState inspectionState;

        public StructureInspectionCommands(
            ICoreServerAPI api,
            ILogger logger,
            RumorTargetResolver rumorTargetResolver,
            StructureInspectionState inspectionState
        )
        {
            this.api = api;
            this.logger = logger;
            this.rumorTargetResolver = rumorTargetResolver;
            this.inspectionState = inspectionState;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
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
                .HandleWith(InspectCurrentRegion)
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
                .HandleWith(GoToInspectedStructure)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("resolve")
                .WithDescription(
                    "Resolve o alvo de uma estrutura retornada pelo último inspect."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    api.ChatCommands.Parsers.IntRange(
                        "index",
                        0,
                        int.MaxValue
                    )
                )
                .HandleWith(ResolveInspectedStructure)
                .EndSubCommand();
        }

        private TextCommandResult InspectCurrentRegion(
            TextCommandCallingArgs args
        )
        {
            string filter =
                ((string)args[0]).Trim();

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

            List<GeneratedStructure> matches = new();

            foreach (
                GeneratedStructure structure
                in region.GeneratedStructures
            )
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
                    matches.Add(structure);
                }
            }

            inspectionState.Replace(filter, matches);

            logger.Notification(
                $"=== Rumor inspect: \"{filter}\" ==="
            );

            for (
                int index = 0;
                index < inspectionState.Count;
                index++
            )
            {
                GeneratedStructure structure =
                    inspectionState.Structures[index];

                Cuboidi box = structure.Location;
                Vec3i center = box.Center;

                logger.Notification(
                    $"[{index}] " +
                    $"Code={structure.Code ?? "(sem código)"} | " +
                    $"Group={structure.Group ?? "(sem grupo)"} | " +
                    $"Center={center.X},{center.Y},{center.Z} | " +
                    $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                    $"({box.X2},{box.Y2},{box.Z2})"
                );
            }

            return TextCommandResult.Success(
                $"{inspectionState.Count} estruturas encontradas. " +
                "Use /rumor goto [índice], " +
                "/rumor resolve [índice], " +
                "/rumor boundary [índice] ou " +
                "/rumor overlay [índice]."
            );
        }

        private TextCommandResult GoToInspectedStructure(
            TextCommandCallingArgs args
        )
        {
            int index = (int)args[0];

            if (!inspectionState.TryGet(
                    index,
                    out GeneratedStructure? structure
                ) || structure == null)
            {
                return InvalidIndexResult();
            }

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

        private TextCommandResult ResolveInspectedStructure(
            TextCommandCallingArgs args
        )
        {
            int index = (int)args[0];

            if (!inspectionState.TryGet(
                    index,
                    out GeneratedStructure? structure
                ) || structure == null)
            {
                return InvalidIndexResult();
            }

            Cuboidi box = structure.Location;
            Vec3i center = box.Center;
            StructureKind kind =
                StructureClassifier.Classify(structure);
            string family =
                StructureGrouper.GetFamily(structure);

            RumorSite debugSite = new(
                $"debug|{kind}|{index}",
                kind,
                family,
                structure.Code ?? string.Empty,
                box,
                1
            );

            RumorRecord debugRecord =
                RumorRecord.FromSite(debugSite);

            bool resolved =
                rumorTargetResolver.TryResolve(
                    debugRecord,
                    out RumorTarget? target,
                    out string resolveError
                );

            if (!resolved || target == null)
            {
                return TextCommandResult.Error(
                    resolveError
                );
            }

            logger.Notification(
                "=== Rumor Network: resolução de debug ==="
            );

            logger.Notification(
                $"Index={index} | " +
                $"Kind={kind} | " +
                $"Family={family} | " +
                $"Code={structure.Code ?? "(sem código)"} | " +
                $"Group={structure.Group ?? "(sem grupo)"}"
            );

            logger.Notification(
                $"TrueCenter=(" +
                $"{center.X}; " +
                $"{center.Y}; " +
                $"{center.Z}) | " +
                $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            logger.Notification(
                $"ResolvedTarget=(" +
                $"{target.Position.X:0.0}; " +
                $"{target.Position.Y:0.0}; " +
                $"{target.Position.Z:0.0}) | " +
                $"TargetKind={target.Kind}"
            );

            return TextCommandResult.Success(
                $"[{index}] {kind} resolvido como " +
                $"{target.Kind}. Veja o console."
            );
        }

        private TextCommandResult InvalidIndexResult()
        {
            return TextCommandResult.Error(
                $"Índice inválido. A última inspeção possui " +
                $"{inspectionState.Count} resultados."
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
