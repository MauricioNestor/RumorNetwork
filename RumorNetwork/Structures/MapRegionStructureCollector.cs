using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Structures;

public static class MapRegionStructureCollector
{
    public static List<GeneratedStructure> CollectLoadedNeighborhood(
        ICoreServerAPI api,
        BlockPos centerPosition,
        int radius,
        out int loadedRegionCount
    )
    {
        long centerRegionIndex =
            api.WorldManager.MapRegionIndex2DByBlockPos(
                centerPosition.X,
                centerPosition.Z
            );

        Vec3i centerRegionPosition =
            api.WorldManager.MapRegionPosFromIndex2D(
                centerRegionIndex
            );

        List<GeneratedStructure> structures = new();

        loadedRegionCount = 0;

        for (
            int offsetX = -radius;
            offsetX <= radius;
            offsetX++
        )
        {
            for (
                int offsetZ = -radius;
                offsetZ <= radius;
                offsetZ++
            )
            {
                int regionX =
                    centerRegionPosition.X + offsetX;

                int regionZ =
                    centerRegionPosition.Z + offsetZ;

                IMapRegion region =
                    api.WorldManager.GetMapRegion(
                        regionX,
                        regionZ
                    );

                if (region == null)
                {
                    continue;
                }

                loadedRegionCount++;

                structures.AddRange(
                    region.GeneratedStructures
                );
            }
        }

        return structures;
    }
}