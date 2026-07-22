using System.Collections.Generic;
using ProtoBuf;

namespace RumorNetwork.Catalog
{
    [ProtoContract]
    public sealed class SelectiveStructureCatalogSaveData
    {
        [ProtoMember(1)]
        public int Version { get; set; } = 1;

        [ProtoMember(2)]
        public List<long> ScannedRegionIndices
            { get; set; } = new();
    }
}
