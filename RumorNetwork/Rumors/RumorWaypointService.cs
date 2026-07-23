using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RumorNetwork.Configuration;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RumorNetwork.Rumors
{
    public static class RumorWaypointService
    {
        private const string WaypointGuidPrefix = "rumornetwork:";
        private const string WaypointTitlePrefix = "Rumor:";

        private static readonly MethodInfo? ResendWaypointsMethod =
            typeof(WaypointMapLayer).GetMethod(
                "ResendWaypoints",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public
            );

        private static readonly MethodInfo? RebuildMapComponentsMethod =
            typeof(WaypointMapLayer).GetMethod(
                "RebuildMapComponents",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public
            );

        public static bool TryAddWaypoints(
            ICoreServerAPI api,
            IServerPlayer player,
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTargetResolution resolution,
            Random random,
            out IReadOnlyList<Vec3d> waypointPositions,
            out string error
        )
        {
            bool added = TryAddWaypointsTracked(
                api,
                player,
                record,
                knowledge,
                resolution,
                random,
                out IReadOnlyList<RumorWaypointHandle> waypointHandles,
                out error
            );

            waypointPositions = waypointHandles
                .Select(handle => handle.Position)
                .ToList()
                .AsReadOnly();

            return added;
        }

        public static bool TryAddWaypointsTracked(
            ICoreServerAPI api,
            IServerPlayer player,
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTargetResolution resolution,
            Random random,
            out IReadOnlyList<RumorWaypointHandle> waypointHandles,
            out string error
        )
        {
            List<RumorWaypointHandle> addedHandles = new();
            waypointHandles = addedHandles.AsReadOnly();
            error = string.Empty;

            if (
                !TryGetWaypointLayer(
                    api,
                    out WaypointMapLayer? waypointLayer,
                    out error
                ) ||
                waypointLayer == null
            )
            {
                return false;
            }

            IReadOnlyList<RumorTarget> targets = SelectTargets(
                resolution,
                knowledge
            );

            try
            {
                foreach (RumorTarget target in targets)
                {
                    Vec3d waypointPosition = CreateWaypointPosition(
                        target.Position,
                        knowledge,
                        random
                    );

                    string waypointGuid =
                        WaypointGuidPrefix +
                        Guid.NewGuid().ToString("N");

                    Waypoint waypoint = new()
                    {
                        Color = ColorUtil.Hex2Int(
                            CreateColor(record, knowledge)
                        ),
                        Icon = CreateIcon(record),
                        Pinned = true,
                        Position = waypointPosition,
                        OwningPlayerUid = player.PlayerUID,
                        Title = CreateTitle(record, knowledge),
                        Guid = waypointGuid
                    };

                    waypointLayer.AddWaypoint(
                        waypoint,
                        player
                    );

                    addedHandles.Add(
                        new RumorWaypointHandle(
                            waypointGuid,
                            waypointPosition
                        )
                    );
                }
            }
            catch (Exception exception)
            {
                if (addedHandles.Count > 0)
                {
                    TryRemoveWaypoints(
                        api,
                        player,
                        addedHandles.Select(handle => handle.Guid),
                        out _,
                        out _
                    );
                }

                error =
                    "Não foi possível adicionar os " +
                    "waypoints do rumor: " +
                    exception.GetBaseException().Message;

                return false;
            }

            waypointHandles = addedHandles.AsReadOnly();
            return true;
        }

        public static bool TryRemoveWaypoints(
            ICoreServerAPI api,
            IServerPlayer player,
            IEnumerable<string> waypointGuids,
            out int removedCount,
            out string error
        )
        {
            removedCount = 0;
            error = string.Empty;

            if (
                !TryGetWaypointLayer(
                    api,
                    out WaypointMapLayer? waypointLayer,
                    out error
                ) ||
                waypointLayer == null
            )
            {
                return false;
            }

            HashSet<string> requestedGuids = new(
                waypointGuids,
                StringComparer.Ordinal
            );

            removedCount = waypointLayer.Waypoints.RemoveAll(
                waypoint =>
                    waypoint.OwningPlayerUid == player.PlayerUID &&
                    waypoint.Guid != null &&
                    requestedGuids.Contains(waypoint.Guid)
            );

            return TrySynchronizeWaypointLayer(
                waypointLayer,
                player,
                removedCount,
                out error
            );
        }

        public static bool TryClearWaypoints(
            ICoreServerAPI api,
            IServerPlayer player,
            out int removedCount,
            out string error
        )
        {
            removedCount = 0;
            error = string.Empty;

            if (
                !TryGetWaypointLayer(
                    api,
                    out WaypointMapLayer? waypointLayer,
                    out error
                ) ||
                waypointLayer == null
            )
            {
                return false;
            }

            removedCount = waypointLayer.Waypoints.RemoveAll(
                waypoint =>
                    waypoint.OwningPlayerUid == player.PlayerUID &&
                    IsRumorWaypoint(waypoint)
            );

            return TrySynchronizeWaypointLayer(
                waypointLayer,
                player,
                removedCount,
                out error
            );
        }

        private static bool TrySynchronizeWaypointLayer(
            WaypointMapLayer waypointLayer,
            IServerPlayer player,
            int changedCount,
            out string error
        )
        {
            error = string.Empty;

            if (changedCount == 0)
            {
                return true;
            }

            if (ResendWaypointsMethod == null)
            {
                error =
                    "A versão atual do jogo não expõe a " +
                    "rotina necessária para sincronizar " +
                    "a remoção dos waypoints.";

                return false;
            }

            try
            {
                ResendWaypointsMethod.Invoke(
                    waypointLayer,
                    new object[]
                    {
                        player
                    }
                );

                RebuildMapComponentsMethod?.Invoke(
                    waypointLayer,
                    null
                );

                return true;
            }
            catch (Exception exception)
            {
                error =
                    "A camada do mapa não pôde ser " +
                    "atualizada: " +
                    exception.GetBaseException().Message;

                return false;
            }
        }

        private static bool TryGetWaypointLayer(
            ICoreServerAPI api,
            out WaypointMapLayer? waypointLayer,
            out string error
        )
        {
            waypointLayer = null;
            error = string.Empty;

            if (!api.World.Config.GetBool("allowMap", true))
            {
                error = "O mapa está desativado neste mundo.";
                return false;
            }

            WorldMapManager mapManager = api.ModLoader
                .GetModSystem<WorldMapManager>();

            waypointLayer = mapManager.MapLayers
                .OfType<WaypointMapLayer>()
                .FirstOrDefault();

            if (waypointLayer == null)
            {
                error =
                    "Não foi possível localizar " +
                    "a camada de waypoints.";

                return false;
            }

            return true;
        }

        private static bool IsRumorWaypoint(
            Waypoint waypoint
        )
        {
            bool taggedGuid = waypoint.Guid?.StartsWith(
                WaypointGuidPrefix,
                StringComparison.Ordinal
            ) == true;

            bool legacyTitle = waypoint.Title?.StartsWith(
                WaypointTitlePrefix,
                StringComparison.Ordinal
            ) == true;

            return taggedGuid || legacyTitle;
        }

        private static IReadOnlyList<RumorTarget> SelectTargets(
            RumorTargetResolution resolution,
            RumorKnowledgeLevel knowledge
        )
        {
            if (knowledge == RumorKnowledgeLevel.Approximate)
            {
                return new[]
                {
                    resolution.PrimaryTarget
                };
            }

            // Exact underground-ruin rumors retain both the cave entrance
            // and the structure entrance supplied by RumorTargetResolver.
            return resolution.Targets;
        }

        private static Vec3d CreateWaypointPosition(
            Vec3d resolvedPosition,
            RumorKnowledgeLevel knowledge,
            Random random
        )
        {
            Vec3d position = resolvedPosition.Clone();

            if (knowledge != RumorKnowledgeLevel.Approximate)
            {
                return position;
            }

            RumorWaypointConfig config =
                RumorRuntimeSettings.Waypoints;

            double angle = random.NextDouble() * Math.PI * 2;
            double distance =
                config.ApproximateMinimumOffset +
                random.NextDouble() *
                (
                    config.ApproximateMaximumOffset -
                    config.ApproximateMinimumOffset
                );

            position.X += Math.Cos(angle) * distance;
            position.Z += Math.Sin(angle) * distance;

            return position;
        }

        private static string CreateColor(
            RumorRecord record,
            RumorKnowledgeLevel knowledge
        )
        {
            RumorWaypointConfig config =
                RumorRuntimeSettings.Waypoints;

            return record.Kind switch
            {
                StructureKind.Trader =>
                    config.TraderColor,

                StructureKind.Translocator =>
                    config.TranslocatorColor,

                _ when knowledge ==
                    RumorKnowledgeLevel.Approximate =>
                        config.ApproximateRuinColor,

                _ => config.ExactRuinColor
            };
        }

        private static string CreateIcon(
            RumorRecord record
        )
        {
            RumorWaypointConfig config =
                RumorRuntimeSettings.Waypoints;

            return record.Kind switch
            {
                StructureKind.Trader =>
                    config.TraderIcon,

                StructureKind.Translocator =>
                    config.TranslocatorIcon,

                StructureKind.UndergroundRuin or
                StructureKind.BetterRuin or
                StructureKind.SurfaceRuin or
                StructureKind.RuinedVillage =>
                    config.RuinIcon,

                _ => config.FallbackIcon
            };
        }

        private static string CreateTitle(
            RumorRecord record,
            RumorKnowledgeLevel knowledge
        )
        {
            RumorWaypointConfig config =
                RumorRuntimeSettings.Waypoints;

            if (record.Kind == StructureKind.Trader)
            {
                return Lang.Get(config.TraderTitleKey);
            }

            if (record.Kind == StructureKind.Translocator)
            {
                return Lang.Get(config.TranslocatorTitleKey);
            }

            if (IsRuin(record.Kind))
            {
                string ruinKey = knowledge ==
                    RumorKnowledgeLevel.Approximate
                        ? config.ApproximateRuinTitleKey
                        : config.ExactRuinTitleKey;

                return Lang.Get(ruinKey);
            }

            string genericKey = knowledge ==
                RumorKnowledgeLevel.Approximate
                    ? "rumornetwork:waypoint-generic-approximate"
                    : "rumornetwork:waypoint-generic-exact";

            return Lang.Get(
                genericKey,
                record.Kind.ToString()
            );
        }

        private static bool IsRuin(
            StructureKind kind
        )
        {
            return kind is
                StructureKind.UndergroundRuin or
                StructureKind.BetterRuin or
                StructureKind.SurfaceRuin or
                StructureKind.RuinedVillage;
        }
    }
}
