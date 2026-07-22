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
            Cuboidi structureBox
        )
        {
            Cuboidi scannedBox =
                CreateEffectiveBoundaryBox(
                    structureBox
                );

            if (
                scannedBox.SizeX <= 0 ||
                scannedBox.SizeY <= 0 ||
                scannedBox.SizeZ <= 0
            )
            {
                throw new ArgumentException(
                    "A bounding box efetiva precisa ter volume positivo.",
                    nameof(structureBox)
                );
            }

            CaveBoundaryScanAccumulator accumulator = new();

            foreach (
                CaveBoundaryFace face
                in Enum.GetValues<CaveBoundaryFace>()
            )
            {
                faceScanner.Scan(
                    scannedBox,
                    face,
                    accumulator
                );
            }

            return accumulator.BuildResult(
                scannedBox
            );
        }

        private static Cuboidi CreateEffectiveBoundaryBox(
            Cuboidi structureBox
        )
        {
            return new Cuboidi(
                structureBox.MinX + 1,
                structureBox.MinY + 1,
                structureBox.MinZ,
                structureBox.MaxX - 1,
                structureBox.MaxY - 1,
                structureBox.MaxZ - 1
            );
        }
    }
}
