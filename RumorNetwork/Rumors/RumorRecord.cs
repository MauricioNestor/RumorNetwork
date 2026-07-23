using ProtoBuf;
using RumorNetwork.Structures;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors;

[ProtoContract]
public sealed class RumorRecord
{
    [ProtoMember(1)]
    public string Id { get; set; } = string.Empty;

    [ProtoMember(2)]
    public StructureKind Kind { get; set; }

    [ProtoMember(3)]
    public string Family { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string SourceCode { get; set; } = string.Empty;

    [ProtoMember(5)]
    public int PartCount { get; set; }

    [ProtoMember(6)]
    public RumorKnowledgeLevel Knowledge { get; set; }

    [ProtoMember(7)]
    public int X1 { get; set; }

    [ProtoMember(8)]
    public int Y1 { get; set; }

    [ProtoMember(9)]
    public int Z1 { get; set; }

    [ProtoMember(10)]
    public int X2 { get; set; }

    [ProtoMember(11)]
    public int Y2 { get; set; }

    [ProtoMember(12)]
    public int Z2 { get; set; }

    [ProtoMember(13)]
    public string SourceGroup { get; set; } = string.Empty;

    public Cuboidi CreateLocation()
    {
        return new Cuboidi(
            X1,
            Y1,
            Z1,
            X2,
            Y2,
            Z2
        );
    }

    public static RumorRecord FromSite(
        RumorSite site
    )
    {
        Cuboidi box = site.Location;

        return new RumorRecord
        {
            Id = site.Id,
            Kind = site.Kind,
            Family = site.Family,
            SourceCode = site.SourceCode,
            SourceGroup = site.SourceGroup,
            PartCount = site.PartCount,
            Knowledge = RumorKnowledgeLevel.NotSold,

            X1 = box.X1,
            Y1 = box.Y1,
            Z1 = box.Z1,

            X2 = box.X2,
            Y2 = box.Y2,
            Z2 = box.Z2
        };
    }
}
