using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RumorNetwork.Caves
{
    internal sealed class CaveObstacleClassifier
    {
        private const float FullBlockTolerance = 0.0001f;

        private readonly IBlockAccessor blockAccessor;

        public CaveObstacleClassifier(
            IBlockAccessor blockAccessor
        )
        {
            this.blockAccessor = blockAccessor;
        }

        public CaveTraversalKind Classify(
            Block block,
            BlockPos position
        )
        {
            BlockBehaviorDoor? doorBehavior =
                block.GetBehavior<BlockBehaviorDoor>();

            if (doorBehavior != null)
            {
                return ClassifyDoor(
                    doorBehavior,
                    position
                );
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

            if (HasFullBlockCollision(collisionBoxes))
            {
                return CaveTraversalKind.Blocked;
            }

            if (IsChiseledBlock(block))
            {
                return CaveTraversalKind.Partial;
            }

            return CaveTraversalKind.Unknown;
        }

        private CaveTraversalKind ClassifyDoor(
            BlockBehaviorDoor doorBehavior,
            BlockPos position
        )
        {
            if (!doorBehavior.handopenable)
            {
                return CaveTraversalKind.Blocked;
            }

            BlockEntity? blockEntity =
                blockAccessor.GetBlockEntity(position);

            bool isBarLocked =
                blockEntity?
                    .GetBehavior<BEBehaviorDoorBarLock>()
                != null;

            return isBarLocked
                ? CaveTraversalKind.Blocked
                : CaveTraversalKind.Openable;
        }

        private static bool IsChiseledBlock(
            Block block
        )
        {
            return string.Equals(
                block.Code?.Path,
                "chiseledblock",
                StringComparison.OrdinalIgnoreCase
            );
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
