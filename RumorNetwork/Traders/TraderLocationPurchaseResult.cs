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

        public int SellerPurchasesUsed { get; }

        public int SellerPurchaseLimit { get; }

        public int SellerPurchasesRemaining =>
            System.Math.Max(
                0,
                SellerPurchaseLimit -
                SellerPurchasesUsed
            );

        public TraderLocationPurchaseResult(
            RumorRecord seller,
            RumorRecord target,
            RumorPrice price,
            RumorWaypointHandle waypoint,
            double sellerDistance,
            double targetDistance,
            int sellerPurchasesUsed,
            int sellerPurchaseLimit
        )
        {
            Seller = seller;
            Target = target;
            Price = price;
            Waypoint = waypoint;
            SellerDistance = sellerDistance;
            TargetDistance = targetDistance;
            SellerPurchasesUsed = sellerPurchasesUsed;
            SellerPurchaseLimit = sellerPurchaseLimit;
        }
    }
}
