using System.Collections.Generic;
using ProtoBuf;

namespace RumorNetwork.Traders
{
    [ProtoContract]
    public sealed class TraderKnowledgeSaveData
    {
        [ProtoMember(1)]
        public int Version { get; set; } = 1;

        [ProtoMember(2)]
        public List<PlayerTraderKnowledgeRecord> Players
            { get; set; } = new();
    }
}
