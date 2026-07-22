using RumorNetwork.Caves;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class StructureDebugOverlay
    {
        private const int BoundingBoxSlot = 91701;
        private const int CenterSlot = 91702;
        private const int InsideOpeningSlot = 91703;
        private const int OutsideOpeningSlot = 91704;

        private static readonly int BoundingBoxColor =
            ColorUtil.ToRgba(60, 0, 220, 255);

        private static readonly int CenterColor =
            ColorUtil.ToRgba(120, 255, 220, 0);

        private static readonly int InsideOpeningColor =
            ColorUtil.ToRgba(90, 0, 255, 80);

        private static readonly int OutsideOpeningColor =
            ColorUtil.ToRgba(90, 80, 140, 255);

        private readonly ICoreServerAPI api;

        public StructureDebugOverlay(
            ICoreServerAPI api
        )
        {
            this.api = api;
        }

        public void Show(
            IServerPlayer player,
            GeneratedStructure structure,
            CaveBoundaryScanResult boundaryResult
        )
        {
            Clear(player);

            Cuboidi box = boundaryResult.ScannedBox;
            Vec3i center = structure.Location.Center;

            Highlight(
                player,
                BoundingBoxSlot,
                new List<BlockPos>
                {
                    new BlockPos(
                        box.MinX,
                        box.MinY,
                        box.MinZ
                    ),
                    new BlockPos(
                        box.MaxX - 1,
                        box.MaxY - 1,
                        box.MaxZ - 1
                    )
                },
                BoundingBoxColor,
                EnumHighlightShape.Cube,
                1f
            );

            Highlight(
                player,
                CenterSlot,
                new List<BlockPos>
                {
                    new BlockPos(
                        center.X,
                        center.Y,
                        center.Z
                    )
                },
                CenterColor,
                EnumHighlightShape.Arbitrary,
                1.15f
            );

            List<BlockPos> insideOpenings = new();
            List<BlockPos> outsideOpenings = new();

            foreach (
                CaveBoundaryOpening opening
                in boundaryResult.Openings
            )
            {
                insideOpenings.Add(
                    opening.InsidePosition
                );

                outsideOpenings.Add(
                    opening.OutsidePosition
                );
            }

            Highlight(
                player,
                InsideOpeningSlot,
                insideOpenings,
                InsideOpeningColor,
                EnumHighlightShape.Arbitrary,
                1f
            );

            Highlight(
                player,
                OutsideOpeningSlot,
                outsideOpenings,
                OutsideOpeningColor,
                EnumHighlightShape.Arbitrary,
                1f
            );
        }

        public void Clear(
            IServerPlayer player
        )
        {
            ClearSlot(player, BoundingBoxSlot);
            ClearSlot(player, CenterSlot);
            ClearSlot(player, InsideOpeningSlot);
            ClearSlot(player, OutsideOpeningSlot);
        }

        private void Highlight(
            IServerPlayer player,
            int slot,
            List<BlockPos> positions,
            int color,
            EnumHighlightShape shape,
            float scale
        )
        {
            List<int> colors = new(
                positions.Count
            );

            for (
                int index = 0;
                index < positions.Count;
                index++
            )
            {
                colors.Add(color);
            }

            api.World.HighlightBlocks(
                player,
                slot,
                positions,
                colors,
                EnumHighlightBlocksMode.Absolute,
                shape,
                scale
            );
        }

        private void ClearSlot(
            IServerPlayer player,
            int slot
        )
        {
            api.World.HighlightBlocks(
                player,
                slot,
                new List<BlockPos>(),
                EnumHighlightBlocksMode.Absolute,
                EnumHighlightShape.Arbitrary
            );
        }
    }
}
