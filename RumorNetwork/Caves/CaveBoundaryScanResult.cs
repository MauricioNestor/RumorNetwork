using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Caves
{
    public sealed class CaveBoundaryScanResult
    {
        public CaveBoundaryScanStatus Status { get; }

        public Cuboidi ScannedBox { get; }

        public IReadOnlyList<CaveBoundaryOpening> Openings { get; }

        public int ScannedPairCount { get; }

        public int UnknownPairCount { get; }

        public int UnavailablePairCount { get; }

        public bool HasOpenings => Openings.Count > 0;

        public CaveBoundaryScanResult(
            CaveBoundaryScanStatus status,
            Cuboidi scannedBox,
            IReadOnlyList<CaveBoundaryOpening> openings,
            int scannedPairCount,
            int unknownPairCount,
            int unavailablePairCount
        )
        {
            Status = status;
            ScannedBox = new Cuboidi(
                scannedBox.X1,
                scannedBox.Y1,
                scannedBox.Z1,
                scannedBox.X2,
                scannedBox.Y2,
                scannedBox.Z2
            );
            Openings = openings;
            ScannedPairCount = scannedPairCount;
            UnknownPairCount = unknownPairCount;
            UnavailablePairCount = unavailablePairCount;
        }
    }
}
