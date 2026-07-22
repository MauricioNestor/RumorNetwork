using System.Collections.Generic;

namespace RumorNetwork.Rumors
{
    public sealed class RumorTargetResolution
    {
        private readonly List<RumorTarget> targets;

        public RumorTarget PrimaryTarget { get; }

        public RumorTarget? SecondaryTarget { get; }

        public IReadOnlyList<RumorTarget> Targets => targets;

        public RumorTargetResolution(
            RumorTarget primaryTarget,
            RumorTarget? secondaryTarget = null
        )
        {
            PrimaryTarget = primaryTarget;
            SecondaryTarget = secondaryTarget;

            targets = new List<RumorTarget>
            {
                primaryTarget
            };

            if (secondaryTarget != null)
            {
                targets.Add(secondaryTarget);
            }
        }
    }
}
