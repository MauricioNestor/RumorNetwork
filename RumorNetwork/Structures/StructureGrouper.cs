using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Structures;

public static class StructureGrouper
{
    public static List<GroupedStructure> GroupVillageParts(
        IEnumerable<GeneratedStructure> structures,
        int maximumDistance = 64
    )
    {
        List<GeneratedStructure> remaining = new();

        foreach (GeneratedStructure structure in structures)
        {
            if (
                StructureClassifier.Classify(structure)
                == StructureKind.VillagePart
            )
            {
                remaining.Add(structure);
            }
        }

        List<GroupedStructure> groups = new();

        while (remaining.Count > 0)
        {
            GeneratedStructure first = remaining[^1];
            remaining.RemoveAt(remaining.Count - 1);

            string family = GetFamily(first);

            List<GeneratedStructure> parts = new()
            {
                first
            };

            Queue<GeneratedStructure> pending = new();
            pending.Enqueue(first);

            while (pending.Count > 0)
            {
                GeneratedStructure current = pending.Dequeue();

                for (
                    int index = remaining.Count - 1;
                    index >= 0;
                    index--
                )
                {
                    GeneratedStructure candidate =
                        remaining[index];

                    if (!string.Equals(
                            GetFamily(candidate),
                            family,
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        continue;
                    }

                    if (!AreClose(
                            current,
                            candidate,
                            maximumDistance
                        ))
                    {
                        continue;
                    }

                    remaining.RemoveAt(index);
                    parts.Add(candidate);
                    pending.Enqueue(candidate);
                }
            }

            Cuboidi combinedLocation =
                CombineLocations(parts);

            GroupedStructure group = new(
                StructureKind.RuinedVillage,
                family,
                parts,
                combinedLocation
            );

            groups.Add(group);
        }

        return groups;
    }

    public static string GetFamily(
        GeneratedStructure structure
    )
    {
        string code = structure.Code ?? string.Empty;
        int separatorIndex = code.LastIndexOf('/');

        return separatorIndex >= 0
            ? code[(separatorIndex + 1)..]
            : code;
    }

    private static Cuboidi CombineLocations(
        IEnumerable<GeneratedStructure> structures
    )
    {
        int minimumX = int.MaxValue;
        int minimumY = int.MaxValue;
        int minimumZ = int.MaxValue;

        int maximumX = int.MinValue;
        int maximumY = int.MinValue;
        int maximumZ = int.MinValue;

        foreach (GeneratedStructure structure in structures)
        {
            Cuboidi box = structure.Location;

            minimumX = Math.Min(minimumX, box.X1);
            minimumY = Math.Min(minimumY, box.Y1);
            minimumZ = Math.Min(minimumZ, box.Z1);

            maximumX = Math.Max(maximumX, box.X2);
            maximumY = Math.Max(maximumY, box.Y2);
            maximumZ = Math.Max(maximumZ, box.Z2);
        }

        return new Cuboidi(
            minimumX,
            minimumY,
            minimumZ,
            maximumX,
            maximumY,
            maximumZ
        );
    }

    private static bool AreClose(
        GeneratedStructure first,
        GeneratedStructure second,
        int maximumDistance
    )
    {
        Vec3i firstCenter = first.Location.Center;
        Vec3i secondCenter = second.Location.Center;

        long deltaX =
            firstCenter.X - secondCenter.X;

        long deltaZ =
            firstCenter.Z - secondCenter.Z;

        long distanceSquared =
            deltaX * deltaX +
            deltaZ * deltaZ;

        long maximumDistanceSquared =
            (long)maximumDistance * maximumDistance;

        return distanceSquared <= maximumDistanceSquared;
    }
}