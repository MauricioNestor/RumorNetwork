using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Traders
{
    public sealed class TraderLocationSelector
    {
        private const double MinimumDifferentTraderDistance = 8d;

        private readonly RumorRegistry rumorRegistry;

        public TraderLocationSelector(
            RumorRegistry rumorRegistry
        )
        {
            this.rumorRegistry = rumorRegistry;
        }

        public int CountIndexedTraders()
        {
            return rumorRegistry.CountByKind(
                StructureKind.Trader
            );
        }

        public int CountKnownIndexedTraders(
            PlayerTraderKnowledge knowledge
        )
        {
            int count = 0;

            foreach (
                RumorRecord candidate
                in rumorRegistry.Records
            )
            {
                if (
                    candidate.Kind == StructureKind.Trader &&
                    knowledge.IsKnown(candidate.Id)
                )
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryFindSeller(
            Vec3d playerPosition,
            double maximumDistance,
            out RumorRecord? seller,
            out double distance
        )
        {
            seller = null;
            distance = 0;

            double maximumDistanceSquared =
                maximumDistance * maximumDistance;

            double bestDistanceSquared =
                double.MaxValue;

            foreach (
                RumorRecord candidate
                in rumorRegistry.Records
            )
            {
                if (candidate.Kind != StructureKind.Trader)
                {
                    continue;
                }

                Vec3d candidateCenter =
                    CreateCenter(candidate);

                double candidateDistanceSquared =
                    HorizontalDistanceSquared(
                        playerPosition,
                        candidateCenter
                    );

                if (
                    candidateDistanceSquared >
                        maximumDistanceSquared ||
                    candidateDistanceSquared >=
                        bestDistanceSquared
                )
                {
                    continue;
                }

                seller = candidate;
                bestDistanceSquared =
                    candidateDistanceSquared;
            }

            if (seller == null)
            {
                return false;
            }

            distance =
                System.Math.Sqrt(bestDistanceSquared);

            return true;
        }

        public bool TryFindNearestUnknown(
            RumorRecord seller,
            PlayerTraderKnowledge knowledge,
            out RumorRecord? target,
            out double distance
        )
        {
            target = null;
            distance = 0;

            Vec3d sellerCenter =
                CreateCenter(seller);

            double minimumDistanceSquared =
                MinimumDifferentTraderDistance *
                MinimumDifferentTraderDistance;

            double bestDistanceSquared =
                double.MaxValue;

            foreach (
                RumorRecord candidate
                in rumorRegistry.Records
            )
            {
                if (
                    candidate.Kind != StructureKind.Trader ||
                    candidate.Id == seller.Id ||
                    knowledge.IsKnown(candidate.Id)
                )
                {
                    continue;
                }

                Vec3d candidateCenter =
                    CreateCenter(candidate);

                double candidateDistanceSquared =
                    HorizontalDistanceSquared(
                        sellerCenter,
                        candidateCenter
                    );

                // A live seller can have a different synthetic id from the
                // same trader's catalog record. Distance is the reliable way
                // to keep the seller from revealing itself.
                if (
                    candidateDistanceSquared <=
                        minimumDistanceSquared ||
                    candidateDistanceSquared >=
                        bestDistanceSquared
                )
                {
                    continue;
                }

                target = candidate;
                bestDistanceSquared =
                    candidateDistanceSquared;
            }

            if (target == null)
            {
                return false;
            }

            distance =
                System.Math.Sqrt(bestDistanceSquared);

            return true;
        }

        private static Vec3d CreateCenter(
            RumorRecord record
        )
        {
            Vec3i center =
                record.CreateLocation().Center;

            return new Vec3d(
                center.X + 0.5,
                center.Y + 0.5,
                center.Z + 0.5
            );
        }

        private static double HorizontalDistanceSquared(
            Vec3d first,
            Vec3d second
        )
        {
            double deltaX = first.X - second.X;
            double deltaZ = first.Z - second.Z;

            return
                deltaX * deltaX +
                deltaZ * deltaZ;
        }
    }
}
