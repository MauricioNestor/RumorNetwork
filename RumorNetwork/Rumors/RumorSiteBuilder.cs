using System;
using System.Collections.Generic;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors;

public static class RumorSiteBuilder
{
    public static List<RumorSite> Build(
        IEnumerable<GeneratedStructure> structures
    )
    {
        List<GeneratedStructure> source = new(structures);
        List<RumorSite> sites = new();
        HashSet<string> knownIds =
            new(StringComparer.Ordinal);

        AddGroupedVillages(
            source,
            sites,
            knownIds
        );

        AddSingleStructures(
            source,
            sites,
            knownIds
        );

        return sites;
    }

    private static void AddGroupedVillages(
        IEnumerable<GeneratedStructure> structures,
        ICollection<RumorSite> sites,
        ISet<string> knownIds
    )
    {
        const StructureKind kind =
            StructureKind.RuinedVillage;

        if (!RumorEligibilityPolicy.IsEligible(kind))
        {
            return;
        }

        List<GroupedStructure> villages =
            StructureGrouper.GroupVillageParts(
                structures
            );

        foreach (GroupedStructure village in villages)
        {
            Cuboidi location = village.Location;

            string id = CreateId(
                kind,
                village.Family,
                location
            );

            if (!knownIds.Add(id))
            {
                continue;
            }

            sites.Add(
                new RumorSite(
                    id,
                    kind,
                    village.Family,
                    village.Family,
                    location,
                    village.Parts.Count
                )
            );
        }
    }

    private static void AddSingleStructures(
        IEnumerable<GeneratedStructure> structures,
        ICollection<RumorSite> sites,
        ISet<string> knownIds
    )
    {
        foreach (GeneratedStructure structure in structures)
        {
            StructureKind kind =
                StructureClassifier.Classify(structure);

            if (!RumorEligibilityPolicy.IsEligible(kind))
            {
                continue;
            }

            string family =
                StructureGrouper.GetFamily(structure);

            Cuboidi location = structure.Location;

            string id = CreateId(
                kind,
                family,
                location
            );

            if (!knownIds.Add(id))
            {
                continue;
            }

            sites.Add(
                new RumorSite(
                    id,
                    kind,
                    family,
                    structure.Code ?? string.Empty,
                    location,
                    1
                )
            );
        }
    }

    private static string CreateId(
        StructureKind kind,
        string family,
        Cuboidi location
    )
    {
        return
            $"{kind}|" +
            $"{family}|" +
            $"{location.X1}|" +
            $"{location.Y1}|" +
            $"{location.Z1}|" +
            $"{location.X2}|" +
            $"{location.Y2}|" +
            $"{location.Z2}";
    }
}