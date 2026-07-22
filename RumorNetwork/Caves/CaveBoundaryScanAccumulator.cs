using System.Collections.Generic;

namespace RumorNetwork.Caves
{
    internal sealed class CaveBoundaryScanAccumulator
    {
        private readonly List<CaveBoundaryOpening> openings = new();

        public int ScannedPairCount { get; private set; }

        public int UnknownPairCount { get; private set; }

        public int UnavailablePairCount { get; private set; }

        public void AddUnavailablePair()
        {
            ScannedPairCount++;
            UnavailablePairCount++;
        }

        public void AddClassifiedPair(
            CaveBoundaryOpening opening
        )
        {
            ScannedPairCount++;

            if (
                opening.InsideCell.IsTraversable &&
                opening.OutsideCell.IsTraversable
            )
            {
                openings.Add(opening);
                return;
            }

            if (
                opening.InsideCell.Traversal
                    == CaveTraversalKind.Blocked ||
                opening.OutsideCell.Traversal
                    == CaveTraversalKind.Blocked
            )
            {
                return;
            }

            UnknownPairCount++;
        }

        public CaveBoundaryScanResult BuildResult()
        {
            CaveBoundaryScanStatus status;

            if (openings.Count > 0)
            {
                status = CaveBoundaryScanStatus.Connected;
            }
            else if (UnavailablePairCount > 0)
            {
                status = CaveBoundaryScanStatus.ChunksUnavailable;
            }
            else if (UnknownPairCount > 0)
            {
                status = CaveBoundaryScanStatus.Indeterminate;
            }
            else
            {
                status = CaveBoundaryScanStatus.Sealed;
            }

            return new CaveBoundaryScanResult(
                status,
                openings.AsReadOnly(),
                ScannedPairCount,
                UnknownPairCount,
                UnavailablePairCount
            );
        }
    }
}
