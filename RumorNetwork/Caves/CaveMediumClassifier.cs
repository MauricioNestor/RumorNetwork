using System;
using Vintagestory.API.Common;

namespace RumorNetwork.Caves
{
    internal static class CaveMediumClassifier
    {
        public static CaveMedium Classify(
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
    }
}
