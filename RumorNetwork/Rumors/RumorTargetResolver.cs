using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors
{
    public sealed class RumorTargetResolver
    {
        public bool TryResolve(
            RumorRecord record,
            out RumorTarget? target,
            out string error
        )
        {
            Cuboidi location =
                record.CreateLocation();

            Vec3i center =
                location.Center;

            Vec3d position = new(
                center.X + 0.5,
                center.Y + 0.5,
                center.Z + 0.5
            );

            target = new RumorTarget(
                position,
                RumorTargetKind.StructureCenter
            );

            error = string.Empty;
            return true;
        }
    }
}