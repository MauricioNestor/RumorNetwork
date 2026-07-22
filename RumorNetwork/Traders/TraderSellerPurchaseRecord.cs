using ProtoBuf;

namespace RumorNetwork.Traders
{
    [ProtoContract]
    public sealed class TraderSellerPurchaseRecord
    {
        [ProtoMember(1)]
        public string SellerTraderId { get; set; } =
            string.Empty;

        [ProtoMember(2)]
        public int PurchaseCount { get; set; }
    }
}
