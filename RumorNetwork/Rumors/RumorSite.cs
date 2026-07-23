using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors;

public sealed class RumorSite
{
    public string Id { get; }

    public StructureKind Kind { get; }

    public string Family { get; }

    public string SourceCode { get; }

    public string SourceGroup { get; }

    public Cuboidi Location { get; }

    public int PartCount { get; }

    public Vec3i Center => Location.Center;

    public RumorSite(
        string id,
        StructureKind kind,
        string family,
        string sourceCode,
        Cuboidi location,
        int partCount
    ) : this(
        id,
        kind,
        family,
        sourceCode,
        string.Empty,
        location,
        partCount
    )
    {
    }

    public RumorSite(
        string id,
        StructureKind kind,
        string family,
        string sourceCode,
        string sourceGroup,
        Cuboidi location,
        int partCount
    )
    {
        Id = id;
        Kind = kind;
        Family = family;
        SourceCode = sourceCode;
        SourceGroup = sourceGroup;
        Location = location;
        PartCount = partCount;
    }
}
