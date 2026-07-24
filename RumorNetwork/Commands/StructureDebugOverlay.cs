using RumorNetwork.Caves;
using RumorNetwork.Rumors;
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
        private const int ResolvedTargetSlot = 91705;
        private const int WaypointSlot = 91706;

        private static readonly int BoundingBoxColor =
            ColorUtil.ToRgba(60, 0, 220, 255);

        private static readonly int CenterColor =
            ColorUtil.ToRgba(120, 255, 220, 0);

        private static readonly int InsideOpeningColor =
            ColorUtil.ToRgba(90, 0, 255, 80);

        private static readonly int OutsideOpeningColor =
            ColorUtil.ToRgba(90, 80, 140, 255);

        private static readonly int ResolvedTargetColor =
            ColorUtil.ToRgba(140, 255, 80, 80);

        private static readonly int WaypointColor =
            ColorUtil.ToRgba(140, 255, 255, 255);

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
            ShowCore(
                player,
                boundaryResult.ScannedBox,
                structure.Location.Center,
                boundaryResult,
                new List<BlockPos>(),
                new List<BlockPos>()
            );
        }

        public void ShowRecord(
            IServerPlayer player,
            RumorDebugDeliverySnapshot snapshot,
            CaveBoundaryScanResult? boundaryResult
        )
        {
            Cuboidi structureBox =
                snapshot.Record.CreateLocation();

            List<BlockPos> resolvedTargets = new();

            foreach (RumorTarget target in snapshot.Targets)
            {
                resolvedTargets.Add(
                    ToBlockPos(target.Position)
                );
            }

            List<BlockPos> waypointPositions = new();

            foreach (Vec3d position in snapshot.WaypointPositions)
            {
                waypointPositions.Add(
                    ToBlockPos(position)
                );
            }

            ShowCore(
                player,
                boundaryResult?.ScannedBox ?? structureBox,
                structureBox.Center,
                boundaryResult,
                resolvedTargets,
                waypointPositions
            );
        }

        private void ShowCore(
            IServerPlayer player,
            Cuboidi box,
            Vec3i center,
            CaveBoundaryScanResult? boundaryResult,
            List<BlockPos> resolvedTargets,
            List<BlockPos> waypointPositions
        )
        {
            Clear(player);

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
                        box.MaxX,
                        box.MaxY,
                        box.MaxZ
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

            if (boundaryResult != null)
            {
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

            Highlight(
                player,
                ResolvedTargetSlot,
                resolvedTargets,
                ResolvedTargetColor,
                EnumHighlightShape.Arbitrary,
                1.25f
            );

            Highlight(
                player,
                WaypointSlot,
                waypointPositions,
                WaypointColor,
                EnumHighlightShape.Arbitrary,
                1.4f
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
            ClearSlot(player, ResolvedTargetSlot);
            ClearSlot(player, WaypointSlot);
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

        private static BlockPos ToBlockPos(
            Vec3d position
        )
        {
            return new BlockPos(
                (int)System.Math.Floor(position.X),
                (int)System.Math.Floor(position.Y),
                (int)System.Math.Floor(position.Z)
            );
        }
    }
}
