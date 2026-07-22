using RumorNetwork.Caves;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class CaveSkyDebugCommands
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;
        private readonly CaveBoundaryScanner boundaryScanner;
        private readonly CaveSkyConnectionSearch skyConnectionSearch;
        private readonly StructureInspectionState inspectionState;

        public CaveSkyDebugCommands(
            ICoreServerAPI api,
            ILogger logger,
            CaveBoundaryScanner boundaryScanner,
            CaveSkyConnectionSearch skyConnectionSearch,
            StructureInspectionState inspectionState
        )
        {
            this.api = api;
            this.logger = logger;
            this.boundaryScanner = boundaryScanner;
            this.skyConnectionSearch = skyConnectionSearch;
            this.inspectionState = inspectionState;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("sky")
                .WithDescription(
                    "Procura uma rota das openings da estrutura até o céu."
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
                .HandleWith(SearchSkyConnection)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("skyhere")
                .WithDescription(
                    "Procura uma rota da célula do jogador até o céu."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(SearchSkyFromPlayer)
                .EndSubCommand();
        }

        private TextCommandResult SearchSkyConnection(
            TextCommandCallingArgs args
        )
        {
            int index = (int)args[0];

            if (!inspectionState.TryGet(
                    index,
                    out GeneratedStructure? structure
                ) || structure == null)
            {
                return TextCommandResult.Error(
                    $"Índice inválido. A última inspeção possui " +
                    $"{inspectionState.Count} resultados."
                );
            }

            CaveBoundaryScanResult boundaryResult =
                boundaryScanner.Scan(
                    structure.Location
                );

            CaveSkyConnectionResult skyResult =
                skyConnectionSearch.Search(
                    boundaryResult.ScannedBox,
                    boundaryResult.Openings
                );

            LogResult(
                index,
                structure,
                boundaryResult,
                skyResult
            );

            string skyText = skyResult.SkyPosition == null
                ? ""
                : $" Céu=({skyResult.SkyPosition.X}," +
                  $"{skyResult.SkyPosition.Y}," +
                  $"{skyResult.SkyPosition.Z}).";

            return TextCommandResult.Success(
                $"Sky [{index}]: {skyResult.Status}. " +
                $"Openings={boundaryResult.Openings.Count}, " +
                $"visitadas={skyResult.VisitedCellCount}." +
                skyText +
                " Veja o console."
            );
        }

        private TextCommandResult SearchSkyFromPlayer(
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

            BlockPos start = args.Caller.Entity.Pos.AsBlockPos;
            CaveSkyConnectionResult result =
                skyConnectionSearch.SearchFrom(start);

            logger.Notification(
                "=== Rumor Network: cave sky from player ==="
            );

            logger.Notification(
                $"Start=({start.X},{start.Y},{start.Z}) | " +
                $"Status={result.Status} | " +
                $"Visited={result.VisitedCellCount} | " +
                $"Unavailable={result.UnavailableNeighborCount} | " +
                $"Limited={result.LimitedNeighborCount}"
            );

            if (result.SkyPosition != null)
            {
                BlockPos sky = result.SkyPosition;

                logger.Notification(
                    $"SkyPosition=({sky.X},{sky.Y},{sky.Z})"
                );
            }

            string skyText = result.SkyPosition == null
                ? ""
                : $" Céu=({result.SkyPosition.X}," +
                  $"{result.SkyPosition.Y}," +
                  $"{result.SkyPosition.Z}).";

            return TextCommandResult.Success(
                $"SkyHere: {result.Status}. " +
                $"Visitadas={result.VisitedCellCount}." +
                skyText +
                " Veja o console."
            );
        }

        private void LogResult(
            int index,
            GeneratedStructure structure,
            CaveBoundaryScanResult boundaryResult,
            CaveSkyConnectionResult skyResult
        )
        {
            Cuboidi box = boundaryResult.ScannedBox;

            logger.Notification(
                "=== Rumor Network: cave sky connection ==="
            );

            logger.Notification(
                $"InspectionFilter={inspectionState.Filter} | " +
                $"Index={index} | " +
                $"Code={structure.Code ?? "(sem código)"} | " +
                $"Group={structure.Group ?? "(sem grupo)"}"
            );

            logger.Notification(
                $"Status={skyResult.Status} | " +
                $"BoundaryOpenings={boundaryResult.Openings.Count} | " +
                $"StartingOpenings={skyResult.StartingOpeningCount} | " +
                $"Visited={skyResult.VisitedCellCount} | " +
                $"Unavailable={skyResult.UnavailableNeighborCount} | " +
                $"Limited={skyResult.LimitedNeighborCount}"
            );

            logger.Notification(
                $"ScannedBox=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            if (skyResult.SkyPosition != null)
            {
                BlockPos sky = skyResult.SkyPosition;

                logger.Notification(
                    $"SkyPosition=({sky.X},{sky.Y},{sky.Z})"
                );
            }

            if (skyResult.SourceOpening != null)
            {
                CaveBoundaryOpening opening =
                    skyResult.SourceOpening;

                logger.Notification(
                    $"SourceOpeningFace={opening.Face} | " +
                    $"Inside=({opening.InsidePosition.X}," +
                    $"{opening.InsidePosition.Y}," +
                    $"{opening.InsidePosition.Z}) | " +
                    $"Outside=({opening.OutsidePosition.X}," +
                    $"{opening.OutsidePosition.Y}," +
                    $"{opening.OutsidePosition.Z})"
                );
            }
        }
    }
}
