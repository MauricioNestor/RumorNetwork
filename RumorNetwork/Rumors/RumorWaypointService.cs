using System;
using System.Linq;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RumorNetwork.Rumors
{
    public static class RumorWaypointService
    {
        private const double ApproximateMinimumOffset =
            128;

        private const double ApproximateMaximumOffset =
            384;

        public static bool TryAddWaypoint(
            ICoreServerAPI api,
            IServerPlayer player,
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTarget target,
            Random random,
            out Vec3d waypointPosition,
            out string error
        )
        {
            waypointPosition =
                CreateWaypointPosition(
                    target.Position,
                    knowledge,
                    random
                );

            error = string.Empty;

            if (
                !api.World.Config.GetBool(
                    "allowMap",
                    true
                )
            )
            {
                error =
                    "O mapa está desativado neste mundo.";

                return false;
            }

            WorldMapManager mapManager =
                api.ModLoader
                    .GetModSystem<WorldMapManager>();

            WaypointMapLayer? waypointLayer =
                mapManager.MapLayers
                    .OfType<WaypointMapLayer>()
                    .FirstOrDefault();

            if (waypointLayer == null)
            {
                error =
                    "Não foi possível localizar " +
                    "a camada de waypoints.";

                return false;
            }

            Waypoint waypoint = new()
            {
                Color =
                    ColorUtil.Hex2Int("#E6B43C"),

                Icon = "circle",
                Pinned = true,
                Position = waypointPosition,
                OwningPlayerUid =
                    player.PlayerUID,

                Title = CreateTitle(
                    record,
                    knowledge
                ),

                Guid =
                    Guid.NewGuid().ToString()
            };

            waypointLayer.AddWaypoint(
                waypoint,
                player
            );

            return true;
        }

        private static Vec3d
            CreateWaypointPosition(
                Vec3d resolvedPosition,
                RumorKnowledgeLevel knowledge,
                Random random
            )
        {
            Vec3d position =
                resolvedPosition.Clone();

            if (
                knowledge
                != RumorKnowledgeLevel.Approximate
            )
            {
                return position;
            }

            double angle =
                random.NextDouble() *
                Math.PI *
                2;

            double distance =
                ApproximateMinimumOffset +
                random.NextDouble() *
                (
                    ApproximateMaximumOffset -
                    ApproximateMinimumOffset
                );

            position.X +=
                Math.Cos(angle) * distance;

            position.Z +=
                Math.Sin(angle) * distance;

            return position;
        }

        private static string CreateTitle(
            RumorRecord record,
            RumorKnowledgeLevel knowledge
        )
        {
            string locationName =
                record.Kind switch
                {
                    StructureKind.Trader =>
                        "comerciante",

                    StructureKind.Vug =>
                        record.Family,

                    StructureKind.RuinedVillage =>
                        "vila em ruínas",

                    StructureKind.SurfaceRuin =>
                        "ruínas na superfície",

                    StructureKind.UndergroundRuin =>
                        "ruínas subterrâneas",

                    StructureKind.BetterRuin =>
                        "ruínas antigas",

                    StructureKind.Translocator =>
                        "translocador",

                    _ =>
                        record.Kind.ToString()
                };

            string precision =
                knowledge
                == RumorKnowledgeLevel.Approximate
                    ? "local aproximado"
                    : "local exato";

            return
                $"Rumor: {locationName} — " +
                precision;
        }
    }
}