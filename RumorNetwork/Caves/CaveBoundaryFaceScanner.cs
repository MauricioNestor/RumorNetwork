using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Caves
{
    internal sealed class CaveBoundaryFaceScanner
    {
        private readonly IBlockAccessor blockAccessor;
        private readonly CaveCellClassifier cellClassifier;

        public CaveBoundaryFaceScanner(
            IBlockAccessor blockAccessor,
            CaveCellClassifier cellClassifier
        )
        {
            this.blockAccessor = blockAccessor;
            this.cellClassifier = cellClassifier;
        }

        public void Scan(
            Cuboidi box,
            CaveBoundaryFace face,
            CaveBoundaryScanAccumulator accumulator
        )
        {
            GetIterationRanges(
                box,
                face,
                out int firstMin,
                out int firstMax,
                out int secondMin,
                out int secondMax
            );

            for (int first = firstMin; first < firstMax; first++)
            {
                for (
                    int second = secondMin;
                    second < secondMax;
                    second++
                )
                {
                    CreateBoundaryPair(
                        box,
                        face,
                        first,
                        second,
                        out BlockPos insidePosition,
                        out BlockPos outsidePosition
                    );

                    if (
                        !IsAvailable(insidePosition) ||
                        !IsAvailable(outsidePosition)
                    )
                    {
                        accumulator.AddUnavailablePair();
                        continue;
                    }

                    CaveCellInfo insideCell =
                        cellClassifier.Classify(
                            insidePosition
                        );

                    CaveCellInfo outsideCell =
                        cellClassifier.Classify(
                            outsidePosition
                        );

                    accumulator.AddClassifiedPair(
                        new CaveBoundaryOpening(
                            face,
                            insidePosition,
                            outsidePosition,
                            insideCell,
                            outsideCell
                        )
                    );
                }
            }
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

        private static void GetIterationRanges(
            Cuboidi box,
            CaveBoundaryFace face,
            out int firstMin,
            out int firstMax,
            out int secondMin,
            out int secondMax
        )
        {
            switch (face)
            {
                case CaveBoundaryFace.MinX:
                case CaveBoundaryFace.MaxX:
                    firstMin = box.MinY;
                    firstMax = box.MaxY;
                    secondMin = box.MinZ;
                    secondMax = box.MaxZ;
                    return;

                case CaveBoundaryFace.MinY:
                case CaveBoundaryFace.MaxY:
                    firstMin = box.MinX;
                    firstMax = box.MaxX;
                    secondMin = box.MinZ;
                    secondMax = box.MaxZ;
                    return;

                case CaveBoundaryFace.MinZ:
                case CaveBoundaryFace.MaxZ:
                    firstMin = box.MinX;
                    firstMax = box.MaxX;
                    secondMin = box.MinY;
                    secondMax = box.MaxY;
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(face),
                        face,
                        null
                    );
            }
        }

        private static void CreateBoundaryPair(
            Cuboidi box,
            CaveBoundaryFace face,
            int first,
            int second,
            out BlockPos insidePosition,
            out BlockPos outsidePosition
        )
        {
            switch (face)
            {
                case CaveBoundaryFace.MinX:
                    insidePosition = new BlockPos(
                        box.MinX,
                        first,
                        second
                    );
                    outsidePosition = new BlockPos(
                        box.MinX - 1,
                        first,
                        second
                    );
                    return;

                case CaveBoundaryFace.MaxX:
                    insidePosition = new BlockPos(
                        box.MaxX - 1,
                        first,
                        second
                    );
                    outsidePosition = new BlockPos(
                        box.MaxX,
                        first,
                        second
                    );
                    return;

                case CaveBoundaryFace.MinY:
                    insidePosition = new BlockPos(
                        first,
                        box.MinY,
                        second
                    );
                    outsidePosition = new BlockPos(
                        first,
                        box.MinY - 1,
                        second
                    );
                    return;

                case CaveBoundaryFace.MaxY:
                    insidePosition = new BlockPos(
                        first,
                        box.MaxY - 1,
                        second
                    );
                    outsidePosition = new BlockPos(
                        first,
                        box.MaxY,
                        second
                    );
                    return;

                case CaveBoundaryFace.MinZ:
                    insidePosition = new BlockPos(
                        first,
                        second,
                        box.MinZ
                    );
                    outsidePosition = new BlockPos(
                        first,
                        second,
                        box.MinZ - 1
                    );
                    return;

                case CaveBoundaryFace.MaxZ:
                    insidePosition = new BlockPos(
                        first,
                        second,
                        box.MaxZ - 1
                    );
                    outsidePosition = new BlockPos(
                        first,
                        second,
                        box.MaxZ
                    );
                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(face),
                        face,
                        null
                    );
            }
        }
    }
}
