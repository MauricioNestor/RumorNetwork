using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RumorNetwork.Caves
{
    public sealed class CaveCellClassifier
    {
        private const float FullBlockTolerance = 0.0001f;

        private readonly IBlockAccessor blockAccessor;

        public CaveCellClassifier(
            IBlockAccessor blockAccessor
        )
        {
            this.blockAccessor = blockAccessor;
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
                ClassifyMedium(fluidBlock);

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
                ClassifyObstacle(
                    obstacleBlock,
                    position
                );

            return new CaveCellInfo(
                traversal,
                medium
            );
        }

        private CaveTraversalKind ClassifyObstacle(
            Block block,
            BlockPos position
        )
        {
            BlockBehaviorDoor? doorBehavior =
                block.GetBehavior<BlockBehaviorDoor>();

            if (doorBehavior != null)
            {
                if (!doorBehavior.handopenable)
                {
                    return CaveTraversalKind.Blocked;
                }

                if (IsBarLockedDoor(position))
                {
                    return CaveTraversalKind.Blocked;
                }

                return CaveTraversalKind.Openable;
            }

            Cuboidf[]? collisionBoxes =
                block.GetCollisionBoxes(
                    blockAccessor,
                    position
                );

            if (
                collisionBoxes == null ||
                collisionBoxes.Length == 0
            )
            {
                return CaveTraversalKind.Open;
            }

            return HasFullBlockCollision(collisionBoxes)
                ? CaveTraversalKind.Blocked
                : CaveTraversalKind.Unknown;
        }

        private bool IsBarLockedDoor(
            BlockPos position
        )
        {
            BlockEntity? blockEntity =
                blockAccessor.GetBlockEntity(position);

            return blockEntity?
                .GetBehavior<BEBehaviorDoorBarLock>()
                != null;
        }

        private static CaveMedium ClassifyMedium(
            Block fluidBlock
        )
        {
            if (
                fluidBlock.Id == 0 ||
                !fluidBlock.IsLiquid()
            )
            {
                return CaveMedium.Dry;
            }

            if (
                fluidBlock is IBlockFlowing flowingBlock &&
                flowingBlock.IsLava
            )
            {
                return CaveMedium.Lava;
            }

            if (
                string.Equals(
                    fluidBlock.LiquidCode,
                    "water",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return CaveMedium.Water;
            }

            return CaveMedium.OtherFluid;
        }

        private static bool HasFullBlockCollision(
            Cuboidf[] collisionBoxes
        )
        {
            foreach (Cuboidf box in collisionBoxes)
            {
                if (IsFullBlock(box))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFullBlock(
            Cuboidf box
        )
        {
            return
                box.X1 <= FullBlockTolerance &&
                box.Y1 <= FullBlockTolerance &&
                box.Z1 <= FullBlockTolerance &&
                box.X2 >= 1 - FullBlockTolerance &&
                box.Y2 >= 1 - FullBlockTolerance &&
                box.Z2 >= 1 - FullBlockTolerance;
        }
    }
}
