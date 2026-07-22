using RumorNetwork.Purchases;
using RumorNetwork.Rumors;

namespace RumorNetwork.Traders
{
    public sealed class TraderLocationPurchaseResult
    {
        public RumorRecord Seller { get; }

        public RumorRecord Target { get; }

        public RumorPrice Price { get; }

        public RumorWaypointHandle Waypoint { get; }

        public double SellerDistance { get; }

        public double TargetDistance { get; }

        public TraderLocationPurchaseResult(
            RumorRecord seller,
            RumorRecord target,
            RumorPrice price,
            RumorWaypointHandle waypoint,
            double sellerDistance,
            double targetDistance
        )
        {
            Seller = seller;
            Target = target;
            Price = price;
            Waypoint = waypoint;
            SellerDistance = sellerDistance;
            TargetDistance = targetDistance;
        }
    }
}
