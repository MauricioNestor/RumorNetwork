using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Structures;

public sealed class GroupedStructure
{
    public StructureKind Kind { get; }

    public string Family { get; }

    public IReadOnlyList<GeneratedStructure> Parts { get; }

    public Cuboidi Location { get; }

    public GroupedStructure(
        StructureKind kind,
        string family,
        List<GeneratedStructure> parts,
        Cuboidi location
    )
    {
        Kind = kind;
        Family = family;
        Parts = parts;
        Location = location;
    }
}