using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors;

public sealed class RumorSite
{
    public string Id { get; }

    public StructureKind Kind { get; }

    public string Family { get; }

    public string SourceCode { get; }

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
    )
    {
        Id = id;
        Kind = kind;
        Family = family;
        SourceCode = sourceCode;
        Location = location;
        PartCount = partCount;
    }
}