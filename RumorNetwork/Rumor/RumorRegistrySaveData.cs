using System.Collections.Generic;
using ProtoBuf;

namespace RumorNetwork.Rumors;

[ProtoContract]
public sealed class RumorRegistrySaveData
{
    [ProtoMember(1)]
    public int Version { get; set; } = 1;

    [ProtoMember(2)]
    public List<RumorRecord> Records { get; set; } = new();
}