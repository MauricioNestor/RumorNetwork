using RumorNetwork.Caves;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class CaveDebugCommands
    {
        private const string UndergroundRuinFilter =
            "undergroundruin";

        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly IBlockAccessor blockAccessor;
        private readonly CaveCellClassifier caveCellClassifier;
        private readonly CaveBoundaryScanner caveBoundaryScanner;

        public CaveDebugCommands(
            ICoreServerAPI api,
            ILogger logger,
            IBlockAccessor blockAccessor,
            CaveCellClassifier caveCellClassifier,
            CaveBoundaryScanner caveBoundaryScanner
        )
        {
            this.api = api;
            this.logger = logger;
            this.blockAccessor = blockAccessor;
            this.caveCellClassifier = caveCellClassifier;
            this.caveBoundaryScanner = caveBoundaryScanner;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("probe")
                .WithDescription(
                    "Classifica a célula apontada para diagnóstico de cavernas."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ProbeCell)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("boundary")
                .WithDescription(
                    "Escaneia as bordas de uma underground ruin retornada pelo inspect."
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
                .HandleWith(ScanUndergroundRuinBoundary)
                .EndSubCommand();
        }

        private TextCommandResult ProbeCell(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return TextCommandResult.Error(
                    "O comando precisa ser executado por um jogador."
                );
            }

            BlockSelection? selection =
                player.CurrentBlockSelection;

            BlockPos position;
            string source;

            if (selection?.Position != null)
            {
                position = selection.Position;
                source = "AimedBlock";
            }
            else
            {
                position = args.Caller.Entity.Pos.AsBlockPos;
                source = "PlayerCell";
            }

            Block solidBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.Solid
                );

            Block mostSolidBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.MostSolid
                );

            Block fluidBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.Fluid
                );

            Cuboidf[]? collisionBoxes =
                mostSolidBlock.GetCollisionBoxes(
                    blockAccessor,
                    position
                );

            CaveCellInfo cellInfo =
                caveCellClassifier.Classify(position);

            logger.Notification(
                "=== Rumor Network: cave cell probe ==="
            );

            logger.Notification(
                $"Source={source} | " +
                $"Position=({position.X},{position.Y},{position.Z})"
            );

            logger.Notification(
                $"Solid={FormatBlock(solidBlock)} | " +
                $"MostSolid={FormatBlock(mostSolidBlock)} | " +
                $"Fluid={FormatBlock(fluidBlock)}"
            );

            logger.Notification(
                $"CollisionBoxes={collisionBoxes?.Length ?? 0} | " +
                $"Traversal={cellInfo.Traversal} | " +
                $"Medium={cellInfo.Medium} | " +
                $"Traversable={cellInfo.IsTraversable}"
            );

            return TextCommandResult.Success(
                $"Célula {position.X}, {position.Y}, {position.Z}: " +
                $"{cellInfo.Traversal}, {cellInfo.Medium}. " +
                "Veja o console."
            );
        }

        private TextCommandResult ScanUndergroundRuinBoundary(
            TextCommandCallingArgs args
        )
        {
            int index = (int)args[0];
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

            List<GeneratedStructure> undergroundRuins =
                CollectUndergroundRuins(region);

            if (
                index < 0 ||
                index >= undergroundRuins.Count
            )
            {
                return TextCommandResult.Error(
                    $"Índice inválido. A região possui " +
                    $"{undergroundRuins.Count} underground ruins."
                );
            }

            GeneratedStructure structure =
                undergroundRuins[index];
            Cuboidi box = structure.Location;

            CaveBoundaryScanResult result =
                caveBoundaryScanner.Scan(box);

            logger.Notification(
                "=== Rumor Network: underground ruin boundary ==="
            );

            logger.Notification(
                $"Index={index} | " +
                $"Code={structure.Code ?? "(sem código)"} | " +
                $"Group={structure.Group ?? "(sem grupo)"} | " +
                $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            CaveBoundaryDebugReporter.Log(
                logger,
                result
            );

            return TextCommandResult.Success(
                $"Boundary [{index}]: {result.Status}. " +
                $"Aberturas={result.Openings.Count}, " +
                $"desconhecidas={result.UnknownPairCount}, " +
                $"indisponíveis={result.UnavailablePairCount}. " +
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

        private static List<GeneratedStructure>
            CollectUndergroundRuins(
                IMapRegion region
            )
        {
            List<GeneratedStructure> undergroundRuins = new();

            foreach (
                GeneratedStructure structure
                in region.GeneratedStructures
            )
            {
                bool codeMatches =
                    structure.Code?.Contains(
                        UndergroundRuinFilter,
                        StringComparison.OrdinalIgnoreCase
                    ) == true;

                bool groupMatches =
                    structure.Group?.Contains(
                        UndergroundRuinFilter,
                        StringComparison.OrdinalIgnoreCase
                    ) == true;

                if (codeMatches || groupMatches)
                {
                    undergroundRuins.Add(structure);
                }
            }

            return undergroundRuins;
        }

        private static string FormatBlock(
            Block block
        )
        {
            return block.Code?.ToString()
                ?? $"(sem código, id={block.Id})";
        }
    }
}
