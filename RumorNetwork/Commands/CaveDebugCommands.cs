using RumorNetwork.Caves;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class CaveDebugCommands
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly IBlockAccessor blockAccessor;
        private readonly CaveCellClassifier caveCellClassifier;
        private readonly CaveBoundaryScanner caveBoundaryScanner;
        private readonly StructureInspectionState inspectionState;
        private readonly StructureDebugOverlay debugOverlay;

        public CaveDebugCommands(
            ICoreServerAPI api,
            ILogger logger,
            IBlockAccessor blockAccessor,
            CaveCellClassifier caveCellClassifier,
            CaveBoundaryScanner caveBoundaryScanner,
            StructureInspectionState inspectionState,
            StructureDebugOverlay debugOverlay
        )
        {
            this.api = api;
            this.logger = logger;
            this.blockAccessor = blockAccessor;
            this.caveCellClassifier = caveCellClassifier;
            this.caveBoundaryScanner = caveBoundaryScanner;
            this.inspectionState = inspectionState;
            this.debugOverlay = debugOverlay;
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
                    "Escaneia as bordas de uma estrutura retornada pelo último inspect."
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
                .HandleWith(ScanInspectedBoundary)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("overlay")
                .WithDescription(
                    "Exibe uma estrutura inspecionada, um waypoint debug dN, ou limpa overlays."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(
                    api.ChatCommands.Parsers.Word(
                        "indexDebugTokenOrClear"
                    )
                )
                .HandleWith(ShowOrClearOverlay)
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
                return PlayerRequiredResult();
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

        private TextCommandResult ScanInspectedBoundary(
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
            CaveBoundaryScanResult result =
                caveBoundaryScanner.Scan(box);

            logger.Notification(
                "=== Rumor Network: inspected structure boundary ==="
            );

            logger.Notification(
                $"InspectionFilter={inspectionState.Filter} | " +
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

        private TextCommandResult ShowOrClearOverlay(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return PlayerRequiredResult();
            }

            string requestedTarget =
                ((string)args[0]).Trim();

            if (string.Equals(
                    requestedTarget,
                    "clear",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                debugOverlay.Clear(player);

                return TextCommandResult.Success(
                    "Overlays de estrutura removidos."
                );
            }

            if (TryParseDebugToken(
                    requestedTarget,
                    out int debugToken
                ))
            {
                return ShowDebugDeliveryOverlay(
                    player,
                    debugToken
                );
            }

            if (!int.TryParse(
                    requestedTarget,
                    out int index
                ) || index < 0)
            {
                return TextCommandResult.Error(
                    "Use /rumor overlay [índice], " +
                    "/rumor overlay d[token] ou " +
                    "/rumor overlay clear."
                );
            }

            if (!inspectionState.TryGet(
                    index,
                    out GeneratedStructure? structure
                ) || structure == null)
            {
                return InvalidIndexResult();
            }

            CaveBoundaryScanResult boundaryResult =
                caveBoundaryScanner.Scan(
                    structure.Location
                );

            debugOverlay.Show(
                player,
                structure,
                boundaryResult
            );

            logger.Notification(
                "=== Rumor Network: structure overlay ==="
            );

            logger.Notification(
                $"InspectionFilter={inspectionState.Filter} | " +
                $"Index={index} | " +
                $"Code={structure.Code ?? "(sem código)"} | " +
                $"Boundary={boundaryResult.Status} | " +
                $"Openings={boundaryResult.Openings.Count}"
            );

            return TextCommandResult.Success(
                $"Overlay [{index}] exibido. " +
                "Ciano=bounding box, amarelo=centro, " +
                "verde=opening interna, azul=opening externa."
            );
        }

        private TextCommandResult ShowDebugDeliveryOverlay(
            IServerPlayer player,
            int debugToken
        )
        {
            if (!RumorDebugDeliveryRegistry.TryGet(
                    debugToken,
                    player.PlayerUID,
                    out RumorDebugDeliverySnapshot? snapshot
                ) || snapshot == null)
            {
                return TextCommandResult.Error(
                    $"Token d{debugToken} não existe nesta sessão " +
                    "para este jogador."
                );
            }

            RumorRecord record = snapshot.Record;
            Cuboidi box = record.CreateLocation();
            Vec3i center = box.Center;

            CaveBoundaryScanResult? boundaryResult =
                record.Kind == StructureKind.UndergroundRuin
                    ? caveBoundaryScanner.Scan(box)
                    : null;

            debugOverlay.ShowRecord(
                player,
                snapshot,
                boundaryResult
            );

            string category = BetterRuinsRumorPolicy.IsBetterRuins(record)
                ? BetterRuinsRumorPolicy.GetCategoryKey(record)
                : "(not-betterruins)";

            logger.Notification(
                "=== Rumor Network: purchased rumor debug overlay ==="
            );

            logger.Notification(
                $"DebugToken=d{debugToken} | " +
                $"Knowledge={snapshot.Knowledge} | " +
                $"Kind={record.Kind} | " +
                $"Category={category} | " +
                $"Parts={record.PartCount}"
            );

            logger.Notification(
                $"Id={record.Id}"
            );

            logger.Notification(
                $"Family={record.Family} | " +
                $"SourceCode={record.SourceCode} | " +
                $"SourceGroup={record.SourceGroup}"
            );

            logger.Notification(
                $"TrueCenter=({center.X},{center.Y},{center.Z}) | " +
                $"Box=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            for (int index = 0;
                index < snapshot.Targets.Count;
                index++)
            {
                RumorTarget target = snapshot.Targets[index];

                logger.Notification(
                    $"ResolvedTarget[{index}]=(" +
                    $"{target.Position.X:0.0}," +
                    $"{target.Position.Y:0.0}," +
                    $"{target.Position.Z:0.0}) | " +
                    $"TargetKind={target.Kind}"
                );
            }

            for (int index = 0;
                index < snapshot.WaypointPositions.Count;
                index++)
            {
                Vec3d position =
                    snapshot.WaypointPositions[index];

                logger.Notification(
                    $"Waypoint[{index}]=(" +
                    $"{position.X:0.0}," +
                    $"{position.Y:0.0}," +
                    $"{position.Z:0.0})"
                );
            }

            if (boundaryResult != null)
            {
                CaveBoundaryDebugReporter.Log(
                    logger,
                    boundaryResult
                );
            }

            return TextCommandResult.Success(
                $"Overlay d{debugToken} exibido. " +
                "Ciano=box, amarelo=centro, vermelho=alvo resolvido, " +
                "branco=waypoint; detalhes no console."
            );
        }

        private TextCommandResult InvalidIndexResult()
        {
            return TextCommandResult.Error(
                $"Índice inválido. A última inspeção possui " +
                $"{inspectionState.Count} resultados."
            );
        }

        private static bool TryParseDebugToken(
            string requestedTarget,
            out int token
        )
        {
            token = 0;

            return
                requestedTarget.Length > 1 &&
                (requestedTarget[0] == 'd' ||
                    requestedTarget[0] == 'D') &&
                int.TryParse(
                    requestedTarget[1..],
                    out token
                ) &&
                token > 0;
        }

        private static TextCommandResult PlayerRequiredResult()
        {
            return TextCommandResult.Error(
                "O comando precisa ser executado por um jogador."
            );
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
