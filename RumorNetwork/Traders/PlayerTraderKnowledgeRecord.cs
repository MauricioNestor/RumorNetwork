using System.Collections.Generic;
using ProtoBuf;

namespace RumorNetwork.Traders
{
    [ProtoContract]
    public sealed class PlayerTraderKnowledgeRecord
    {
        [ProtoMember(1)]
        public string PlayerUid { get; set; } =
            string.Empty;

        [ProtoMember(2)]
        public List<string> RevealedTraderIds
            { get; set; } = new();

        [ProtoMember(3)]
        public List<string> VisitedTraderIds
            { get; set; } = new();
    }
}
