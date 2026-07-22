using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors
{
    public sealed class RumorTarget
    {
        public Vec3d Position { get; }

        public RumorTargetKind Kind { get; }

        public RumorTarget(
            Vec3d position,
            RumorTargetKind kind
        )
        {
            Position = position;
            Kind = kind;
        }
    }
}