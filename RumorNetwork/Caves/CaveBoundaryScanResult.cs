using System.Collections.Generic;

namespace RumorNetwork.Caves
{
    public sealed class CaveBoundaryScanResult
    {
        public CaveBoundaryScanStatus Status { get; }

        public IReadOnlyList<CaveBoundaryOpening> Openings { get; }

        public int ScannedPairCount { get; }

        public int UnknownPairCount { get; }

        public int UnavailablePairCount { get; }

        public bool HasOpenings => Openings.Count > 0;

        public CaveBoundaryScanResult(
            CaveBoundaryScanStatus status,
            IReadOnlyList<CaveBoundaryOpening> openings,
            int scannedPairCount,
            int unknownPairCount,
            int unavailablePairCount
        )
        {
            Status = status;
            Openings = openings;
            ScannedPairCount = scannedPairCount;
            UnknownPairCount = unknownPairCount;
            UnavailablePairCount = unavailablePairCount;
        }
    }
}
