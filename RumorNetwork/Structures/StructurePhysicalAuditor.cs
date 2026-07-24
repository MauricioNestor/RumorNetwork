using System;
using System.Collections.Generic;
using System.Linq;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Structures
{
    public sealed class StructurePhysicalAuditResult
    {
        public bool ChunksLoaded { get; init; }
        public long DeclaredVolume { get; init; }
        public int SampleStride { get; init; }
        public int SampledBlocks { get; init; }
        public int AirBlocks { get; init; }
        public int NaturalBlocks { get; init; }
        public int ArtificialBlocks { get; init; }
        public int MetaBlocks { get; init; }
        public int BlockEntities { get; init; }
        public IReadOnlyList<KeyValuePair<string, int>> TopBlocks { get; init; } =
            Array.Empty<KeyValuePair<string, int>>();

        public bool HasStructuralEvidence =>
            ArtificialBlocks >= 2 || BlockEntities > 0;

        public string FormatTopBlocks(int maximum = 12)
        {
            if (TopBlocks.Count == 0)
            {
                return "(nenhum bloco não-ar amostrado)";
            }

            return string.Join(
                ", ",
                TopBlocks
                    .Take(Math.Max(1, maximum))
                    .Select(pair => $"{pair.Key}={pair.Value}")
            );
        }
    }

    public static class StructurePhysicalAuditor
    {
        private const int MaximumSamples = 32768;

        public static StructurePhysicalAuditResult Audit(
            ICoreServerAPI api,
            RumorRecord record
        )
        {
            Cuboidi box = record.CreateLocation();
            int sizeX = Math.Max(1, box.X2 - box.X1);
            int sizeY = Math.Max(1, box.Y2 - box.Y1);
            int sizeZ = Math.Max(1, box.Z2 - box.Z1);
            long volume = (long)sizeX * sizeY * sizeZ;

            if (!AreChunksLoaded(api, box))
            {
                return new StructurePhysicalAuditResult
                {
                    ChunksLoaded = false,
                    DeclaredVolume = volume,
                    SampleStride = 1
                };
            }

            int stride = CalculateStride(volume);
            IBlockAccessor accessor = api.World.BlockAccessor;
            Dictionary<string, int> topBlocks =
                new(StringComparer.OrdinalIgnoreCase);

            int sampled = 0;
            int air = 0;
            int natural = 0;
            int artificial = 0;
            int meta = 0;
            int blockEntities = 0;
            BlockPos position = new();

            for (int x = box.X1; x < box.X2; x += stride)
            {
                for (int y = box.Y1; y < box.Y2; y += stride)
                {
                    for (int z = box.Z1; z < box.Z2; z += stride)
                    {
                        position.Set(x, y, z);
                        sampled++;

                        Block block = accessor.GetBlock(
                            position,
                            BlockLayersAccess.MostSolid
                        );

                        if (block == null || block.Id == 0)
                        {
                            air++;
                            continue;
                        }

                        string code =
                            block.Code?.ToString() ??
                            $"(block-id:{block.Id})";

                        if (!topBlocks.TryGetValue(code, out int count))
                        {
                            count = 0;
                        }

                        topBlocks[code] = count + 1;

                        string path =
                            block.Code?.Path?.ToLowerInvariant() ??
                            string.Empty;

                        if (path.StartsWith("meta-", StringComparison.Ordinal))
                        {
                            meta++;
                        }
                        else if (IsNatural(path))
                        {
                            natural++;
                        }
                        else
                        {
                            artificial++;
                        }

                        if (accessor.GetBlockEntity(position) != null)
                        {
                            blockEntities++;
                        }
                    }
                }
            }

            return new StructurePhysicalAuditResult
            {
                ChunksLoaded = true,
                DeclaredVolume = volume,
                SampleStride = stride,
                SampledBlocks = sampled,
                AirBlocks = air,
                NaturalBlocks = natural,
                ArtificialBlocks = artificial,
                MetaBlocks = meta,
                BlockEntities = blockEntities,
                TopBlocks = topBlocks
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(24)
                    .ToList()
                    .AsReadOnly()
            };
        }

        private static bool AreChunksLoaded(
            ICoreServerAPI api,
            Cuboidi box
        )
        {
            int chunkSize = GlobalConstants.ChunkSize;
            int minChunkX = box.X1 / chunkSize;
            int maxChunkX = Math.Max(box.X1, box.X2 - 1) / chunkSize;
            int minChunkY = box.Y1 / chunkSize;
            int maxChunkY = Math.Max(box.Y1, box.Y2 - 1) / chunkSize;
            int minChunkZ = box.Z1 / chunkSize;
            int maxChunkZ = Math.Max(box.Z1, box.Z2 - 1) / chunkSize;

            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
                {
                    for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                    {
                        IServerChunk? chunk = api.WorldManager.GetChunk(
                            chunkX,
                            chunkY,
                            chunkZ
                        );

                        if (chunk == null || chunk.Disposed)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static int CalculateStride(long volume)
        {
            if (volume <= MaximumSamples)
            {
                return 1;
            }

            double ratio = volume / (double)MaximumSamples;
            return Math.Max(1, (int)Math.Ceiling(Math.Pow(ratio, 1d / 3d)));
        }

        private static bool IsNatural(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            return
                path == "air" ||
                path.StartsWith("air-", StringComparison.Ordinal) ||
                path.StartsWith("rock-", StringComparison.Ordinal) ||
                path.StartsWith("soil-", StringComparison.Ordinal) ||
                path.StartsWith("sand-", StringComparison.Ordinal) ||
                path.StartsWith("gravel-", StringComparison.Ordinal) ||
                path.StartsWith("ore-", StringComparison.Ordinal) ||
                path.StartsWith("clay-", StringComparison.Ordinal) ||
                path.StartsWith("water", StringComparison.Ordinal) ||
                path.StartsWith("lava", StringComparison.Ordinal) ||
                path.StartsWith("ice", StringComparison.Ordinal) ||
                path.StartsWith("snow", StringComparison.Ordinal) ||
                path.StartsWith("peat", StringComparison.Ordinal) ||
                path.StartsWith("stalag", StringComparison.Ordinal) ||
                path.StartsWith("looseboulder", StringComparison.Ordinal) ||
                path.StartsWith("loosestone", StringComparison.Ordinal) ||
                path.StartsWith("looseflint", StringComparison.Ordinal) ||
                path.StartsWith("grass", StringComparison.Ordinal) ||
                path.StartsWith("tallgrass", StringComparison.Ordinal) ||
                path.StartsWith("fern", StringComparison.Ordinal) ||
                path.StartsWith("flower", StringComparison.Ordinal) ||
                path.StartsWith("mushroom", StringComparison.Ordinal) ||
                path.StartsWith("leaves", StringComparison.Ordinal) ||
                path.StartsWith("branch", StringComparison.Ordinal) ||
                path.StartsWith("root", StringComparison.Ordinal);
        }
    }
}
