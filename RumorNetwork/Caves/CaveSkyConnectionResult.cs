using Vintagestory.API.MathTools;

namespace RumorNetwork.Caves
{
    public sealed class CaveSkyConnectionResult
    {
        public CaveSkyConnectionStatus Status { get; }

        public BlockPos? SkyPosition { get; }

        public CaveBoundaryOpening? SourceOpening { get; }

        public int StartingOpeningCount { get; }

        public int VisitedCellCount { get; }

        public int UnavailableNeighborCount { get; }

        public int LimitedNeighborCount { get; }

        public bool IsConnected =>
            Status == CaveSkyConnectionStatus.ConnectedToSky;

        public CaveSkyConnectionResult(
            CaveSkyConnectionStatus status,
            BlockPos? skyPosition,
            CaveBoundaryOpening? sourceOpening,
            int startingOpeningCount,
            int visitedCellCount,
            int unavailableNeighborCount,
            int limitedNeighborCount
        )
        {
            Status = status;
            SkyPosition = skyPosition;
            SourceOpening = sourceOpening;
            StartingOpeningCount = startingOpeningCount;
            VisitedCellCount = visitedCellCount;
            UnavailableNeighborCount = unavailableNeighborCount;
            LimitedNeighborCount = limitedNeighborCount;
        }
    }
}
