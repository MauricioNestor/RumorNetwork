using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Caves
{
    public sealed class CaveSkyConnectionSearch
    {
        private const int MaximumVisitedCells = 32768;
        private const int MaximumHorizontalDistance = 192;
        private const int MaximumVerticalDistance = 192;

        private static readonly Vec3i[] NeighborOffsets =
        {
            new(1, 0, 0),
            new(-1, 0, 0),
            new(0, 1, 0),
            new(0, -1, 0),
            new(0, 0, 1),
            new(0, 0, -1)
        };

        private readonly IBlockAccessor blockAccessor;
        private readonly CaveCellClassifier cellClassifier;

        public CaveSkyConnectionSearch(
            IBlockAccessor blockAccessor,
            CaveCellClassifier cellClassifier
        )
        {
            this.blockAccessor = blockAccessor;
            this.cellClassifier = cellClassifier;
        }

        public CaveSkyConnectionResult Search(
            Cuboidi sourceBox,
            IReadOnlyList<CaveBoundaryOpening> openings
        )
        {
            if (openings.Count == 0)
            {
                return BuildResult(
                    CaveSkyConnectionStatus.NoOpenings,
                    null,
                    null,
                    0,
                    0,
                    0,
                    0
                );
            }

            Vec3i sourceCenter = sourceBox.Center;
            Queue<SearchNode> frontier = new();
            HashSet<(int X, int Y, int Z)> examined = new();

            int startingOpeningCount = 0;
            int visitedCellCount = 0;
            int unavailableNeighborCount = 0;
            int limitedNeighborCount = 0;

            foreach (CaveBoundaryOpening opening in openings)
            {
                BlockPos start = opening.OutsidePosition;
                var key = ToKey(start);

                if (!examined.Add(key))
                {
                    continue;
                }

                if (!IsAvailable(start))
                {
                    unavailableNeighborCount++;
                    continue;
                }

                CaveCellInfo startCell =
                    cellClassifier.Classify(start);

                if (!startCell.IsTraversable)
                {
                    continue;
                }

                frontier.Enqueue(
                    new SearchNode(
                        Copy(start),
                        opening
                    )
                );

                startingOpeningCount++;
            }

            if (frontier.Count == 0)
            {
                CaveSkyConnectionStatus emptyStatus =
                    unavailableNeighborCount > 0
                        ? CaveSkyConnectionStatus.ChunksUnavailable
                        : CaveSkyConnectionStatus.NoOpenings;

                return BuildResult(
                    emptyStatus,
                    null,
                    null,
                    startingOpeningCount,
                    visitedCellCount,
                    unavailableNeighborCount,
                    limitedNeighborCount
                );
            }

            while (frontier.Count > 0)
            {
                if (visitedCellCount >= MaximumVisitedCells)
                {
                    return BuildResult(
                        CaveSkyConnectionStatus.SearchLimitReached,
                        null,
                        null,
                        startingOpeningCount,
                        visitedCellCount,
                        unavailableNeighborCount,
                        limitedNeighborCount
                    );
                }

                SearchNode current = frontier.Dequeue();
                visitedCellCount++;

                if (IsSkyExposed(current.Position))
                {
                    return BuildResult(
                        CaveSkyConnectionStatus.ConnectedToSky,
                        Copy(current.Position),
                        current.SourceOpening,
                        startingOpeningCount,
                        visitedCellCount,
                        unavailableNeighborCount,
                        limitedNeighborCount
                    );
                }

                foreach (Vec3i offset in NeighborOffsets)
                {
                    BlockPos neighbor = new(
                        current.Position.X + offset.X,
                        current.Position.Y + offset.Y,
                        current.Position.Z + offset.Z
                    );

                    var neighborKey = ToKey(neighbor);

                    if (!examined.Add(neighborKey))
                    {
                        continue;
                    }

                    if (!IsInsideSearchBounds(
                            neighbor,
                            sourceCenter
                        ))
                    {
                        limitedNeighborCount++;
                        continue;
                    }

                    if (!IsAvailable(neighbor))
                    {
                        unavailableNeighborCount++;
                        continue;
                    }

                    CaveCellInfo neighborCell =
                        cellClassifier.Classify(neighbor);

                    if (!neighborCell.IsTraversable)
                    {
                        continue;
                    }

                    frontier.Enqueue(
                        new SearchNode(
                            neighbor,
                            current.SourceOpening
                        )
                    );
                }
            }

            CaveSkyConnectionStatus status;

            if (limitedNeighborCount > 0)
            {
                status = CaveSkyConnectionStatus.SearchLimitReached;
            }
            else if (unavailableNeighborCount > 0)
            {
                status = CaveSkyConnectionStatus.ChunksUnavailable;
            }
            else
            {
                status = CaveSkyConnectionStatus.Enclosed;
            }

            return BuildResult(
                status,
                null,
                null,
                startingOpeningCount,
                visitedCellCount,
                unavailableNeighborCount,
                limitedNeighborCount
            );
        }

        private bool IsSkyExposed(
            BlockPos position
        )
        {
            int rainBlockingHeight =
                blockAccessor.GetRainMapHeightAt(
                    position.X,
                    position.Z
                );

            return position.Y > rainBlockingHeight;
        }

        private bool IsAvailable(
            BlockPos position
        )
        {
            if (
                position.X < 0 ||
                position.X >= blockAccessor.MapSizeX ||
                position.Y < 0 ||
                position.Y >= blockAccessor.MapSizeY ||
                position.Z < 0 ||
                position.Z >= blockAccessor.MapSizeZ
            )
            {
                return false;
            }

            return blockAccessor.GetChunkAtBlockPos(position)
                != null;
        }

        private static bool IsInsideSearchBounds(
            BlockPos position,
            Vec3i sourceCenter
        )
        {
            return
                Math.Abs(position.X - sourceCenter.X)
                    <= MaximumHorizontalDistance &&
                Math.Abs(position.Z - sourceCenter.Z)
                    <= MaximumHorizontalDistance &&
                Math.Abs(position.Y - sourceCenter.Y)
                    <= MaximumVerticalDistance;
        }

        private static (int X, int Y, int Z) ToKey(
            BlockPos position
        )
        {
            return (
                position.X,
                position.Y,
                position.Z
            );
        }

        private static BlockPos Copy(
            BlockPos position
        )
        {
            return new BlockPos(
                position.X,
                position.Y,
                position.Z
            );
        }

        private static CaveSkyConnectionResult BuildResult(
            CaveSkyConnectionStatus status,
            BlockPos? skyPosition,
            CaveBoundaryOpening? sourceOpening,
            int startingOpeningCount,
            int visitedCellCount,
            int unavailableNeighborCount,
            int limitedNeighborCount
        )
        {
            return new CaveSkyConnectionResult(
                status,
                skyPosition,
                sourceOpening,
                startingOpeningCount,
                visitedCellCount,
                unavailableNeighborCount,
                limitedNeighborCount
            );
        }

        private readonly struct SearchNode
        {
            public BlockPos Position { get; }

            public CaveBoundaryOpening SourceOpening { get; }

            public SearchNode(
                BlockPos position,
                CaveBoundaryOpening sourceOpening
            )
            {
                Position = position;
                SourceOpening = sourceOpening;
            }
        }
    }
}
