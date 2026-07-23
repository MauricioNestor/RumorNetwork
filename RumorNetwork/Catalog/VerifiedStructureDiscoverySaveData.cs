using System.Collections.Generic;
using ProtoBuf;

namespace RumorNetwork.Catalog
{
    [ProtoContract]
    public sealed class VerifiedStructureDiscoverySaveData
    {
        [ProtoMember(1)]
        public int Version { get; set; }

        [ProtoMember(2)]
        public List<long> InspectedChunkIndices
            { get; set; } = new();
    }
}
