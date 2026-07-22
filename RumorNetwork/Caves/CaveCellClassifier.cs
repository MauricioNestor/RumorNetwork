using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Caves
{
    public sealed class CaveCellClassifier
    {
        private readonly IBlockAccessor blockAccessor;
        private readonly CaveObstacleClassifier obstacleClassifier;

        public CaveCellClassifier(
            IBlockAccessor blockAccessor
        )
        {
            this.blockAccessor = blockAccessor;
            obstacleClassifier =
                new CaveObstacleClassifier(
                    blockAccessor
                );
        }

        public CaveCellInfo Classify(
            BlockPos position
        )
        {
            Block fluidBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.Fluid
                );

            CaveMedium medium =
                CaveMediumClassifier.Classify(
                    fluidBlock
                );

            if (medium == CaveMedium.Lava)
            {
                return new CaveCellInfo(
                    CaveTraversalKind.Blocked,
                    medium
                );
            }

            if (medium == CaveMedium.OtherFluid)
            {
                return new CaveCellInfo(
                    CaveTraversalKind.Unknown,
                    medium
                );
            }

            Block obstacleBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.MostSolid
                );

            CaveTraversalKind traversal =
                obstacleClassifier.Classify(
                    obstacleBlock,
                    position
                );

            return new CaveCellInfo(
                traversal,
                medium
            );
        }
    }
}
