using Vintagestory.API.MathTools;

namespace RumorNetwork.Caves
{
    public sealed class CaveBoundaryOpening
    {
        public CaveBoundaryFace Face { get; }

        public BlockPos InsidePosition { get; }

        public BlockPos OutsidePosition { get; }

        public CaveCellInfo InsideCell { get; }

        public CaveCellInfo OutsideCell { get; }

        public CaveBoundaryOpening(
            CaveBoundaryFace face,
            BlockPos insidePosition,
            BlockPos outsidePosition,
            CaveCellInfo insideCell,
            CaveCellInfo outsideCell
        )
        {
            Face = face;
            InsidePosition = insidePosition;
            OutsidePosition = outsidePosition;
            InsideCell = insideCell;
            OutsideCell = outsideCell;
        }
    }
}
