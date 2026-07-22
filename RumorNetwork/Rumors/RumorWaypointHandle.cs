using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors
{
    public sealed class RumorWaypointHandle
    {
        public string Guid { get; }

        public Vec3d Position { get; }

        public RumorWaypointHandle(
            string guid,
            Vec3d position
        )
        {
            Guid = guid;
            Position = position;
        }
    }
}
