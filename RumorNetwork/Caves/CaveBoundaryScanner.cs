using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Caves
{
    public sealed class CaveBoundaryScanner
    {
        private readonly CaveBoundaryFaceScanner faceScanner;

        public CaveBoundaryScanner(
            IBlockAccessor blockAccessor,
            CaveCellClassifier cellClassifier
        )
        {
            faceScanner = new CaveBoundaryFaceScanner(
                blockAccessor,
                cellClassifier
            );
        }

        public CaveBoundaryScanResult Scan(
            Cuboidi box
        )
        {
            if (
                box.SizeX <= 0 ||
                box.SizeY <= 0 ||
                box.SizeZ <= 0
            )
            {
                throw new ArgumentException(
                    "A bounding box precisa ter volume positivo.",
                    nameof(box)
                );
            }

            CaveBoundaryScanAccumulator accumulator = new();

            foreach (
                CaveBoundaryFace face
                in Enum.GetValues<CaveBoundaryFace>()
            )
            {
                faceScanner.Scan(
                    box,
                    face,
                    accumulator
                );
            }

            return accumulator.BuildResult();
        }
    }
}
