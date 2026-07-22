using System.Collections.Generic;
using RumorNetwork.Rumors;

namespace RumorNetwork.Purchases
{
    public sealed class RumorPurchaseResult
    {
        public RumorRecord Record { get; }

        public RumorPrice Price { get; }

        public IReadOnlyList<RumorWaypointHandle>
            Waypoints { get; }

        public RumorPurchaseResult(
            RumorRecord record,
            RumorPrice price,
            IReadOnlyList<RumorWaypointHandle>
                waypoints
        )
        {
            Record = record;
            Price = price;
            Waypoints = waypoints;
        }
    }
}
