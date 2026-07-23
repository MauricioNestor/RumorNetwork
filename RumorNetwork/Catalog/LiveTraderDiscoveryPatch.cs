using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RumorNetwork.Dialogue;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RumorNetwork.Catalog
{
    public sealed class LiveTraderDiscoveryPatch : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.live-trader-discovery";

        private const int TraderStructureMatchRadius = 4;
        private const int ExistingTraderMatchRadius = 6;

        private static readonly FieldInfo? DialogueControllerField =
            AccessTools.Field(
                typeof(DialogueComponent),
                "controller"
            );

        private static ICoreServerAPI? serverApi;
        private static ILogger? logger;
        private static RumorRegistry? rumorRegistry;

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.94;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;
            logger = api.Logger;
            harmony = new Harmony(HarmonyId);

            MethodInfo? modStart = AccessTools.Method(
                typeof(RumorNetworkModSystem),
                nameof(RumorNetworkModSystem.StartServerSide)
            );

            MethodInfo? traderInitialize = AccessTools.Method(
                typeof(EntityTrader),
                "Initialize"
            );

            MethodInfo? availabilityExecute = AccessTools.Method(
                typeof(TraderAvailabilityDialogueComponent),
                nameof(TraderAvailabilityDialogueComponent.Execute)
            );

            if (modStart != null)
            {
                harmony.Patch(
                    modStart,
                    postfix: new HarmonyMethod(
                        typeof(LiveTraderDiscoveryPatch),
                        nameof(CaptureRegistry)
                    )
                );
            }

            if (traderInitialize != null)
            {
                harmony.Patch(
                    traderInitialize,
                    postfix: new HarmonyMethod(
                        typeof(LiveTraderDiscoveryPatch),
                        nameof(RegisterInitializedTrader)
                    )
                );
            }
            else
            {
                api.Logger.Warning(
                    "Rumor Network não encontrou EntityTrader.Initialize; " +
                    "traders vivos serão registrados apenas ao conversar."
                );
            }

            if (availabilityExecute != null)
            {
                harmony.Patch(
                    availabilityExecute,
                    prefix: new HarmonyMethod(
                        typeof(LiveTraderDiscoveryPatch),
                        nameof(RegisterInteractingTrader)
                    )
                );
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            rumorRegistry = null;
            serverApi = null;
            logger = null;
            base.Dispose();
        }

        private static void CaptureRegistry(
            RumorNetworkModSystem __instance
        )
        {
            rumorRegistry = Traverse.Create(__instance)
                .Field("rumorRegistry")
                .GetValue<RumorRegistry>();
        }

        private static void RegisterInitializedTrader(
            EntityTrader __instance
        )
        {
            Register(__instance);
        }

        private static void RegisterInteractingTrader(
            TraderAvailabilityDialogueComponent __instance
        )
        {
            DialogueController? controller =
                DialogueControllerField?.GetValue(__instance)
                    as DialogueController;

            if (controller?.NPCEntity is EntityTrader trader)
            {
                Register(trader);
            }
        }

        private static void Register(EntityTrader trader)
        {
            ICoreServerAPI? api = serverApi;
            RumorRegistry? registry = rumorRegistry;

            if (
                api == null ||
                registry == null ||
                trader == null ||
                !trader.Alive
            )
            {
                return;
            }

            GetSpawnPosition(
                trader,
                out int spawnX,
                out int spawnY,
                out int spawnZ
            );

            if (HasTraderNear(
                    registry,
                    spawnX,
                    spawnY,
                    spawnZ
                ))
            {
                return;
            }

            GeneratedStructure? structure = FindTraderStructure(
                api,
                spawnX,
                spawnY,
                spawnZ
            );

            string sourceCode =
                structure?.Code ??
                trader.Code?.ToString() ??
                "live-trader";

            string sourceGroup =
                structure?.Group ?? "live-trader";

            string family = structure != null
                ? StructureGrouper.GetFamily(structure)
                : trader.Code?.Path ?? "live-trader";

            Cuboidi sourceLocation = structure?.Location ?? new Cuboidi(
                spawnX,
                spawnY,
                spawnZ,
                spawnX,
                spawnY,
                spawnZ
            );

            Cuboidi targetLocation = new(
                spawnX,
                spawnY,
                spawnZ,
                spawnX,
                spawnY,
                spawnZ
            );

            string id =
                $"{StructureKind.Trader}|" +
                $"{family}|" +
                $"{sourceLocation.X1}|" +
                $"{sourceLocation.Y1}|" +
                $"{sourceLocation.Z1}|" +
                $"{sourceLocation.X2}|" +
                $"{sourceLocation.Y2}|" +
                $"{sourceLocation.Z2}|" +
                $"spawn={spawnX},{spawnY},{spawnZ}";

            int added = registry.Merge(
                new[]
                {
                    new RumorSite(
                        id,
                        StructureKind.Trader,
                        family,
                        sourceCode,
                        sourceGroup,
                        targetLocation,
                        1
                    )
                }
            );

            if (added > 0)
            {
                logger?.Notification(
                    "Rumor Network registrou imediatamente um " +
                    $"trader vivo em {spawnX},{spawnY},{spawnZ}."
                );
            }
        }

        private static void GetSpawnPosition(
            EntityTrader trader,
            out int x,
            out int y,
            out int z
        )
        {
            double spawnX = trader.Attributes.HasAttribute("spawnX")
                ? trader.Attributes.GetDouble("spawnX")
                : trader.Pos.X;

            double spawnY = trader.Attributes.HasAttribute("spawnY")
                ? trader.Attributes.GetDouble("spawnY")
                : trader.Pos.Y;

            double spawnZ = trader.Attributes.HasAttribute("spawnZ")
                ? trader.Attributes.GetDouble("spawnZ")
                : trader.Pos.Z;

            x = (int)Math.Floor(spawnX);
            y = (int)Math.Floor(spawnY);
            z = (int)Math.Floor(spawnZ);
        }

        private static bool HasTraderNear(
            RumorRegistry registry,
            int x,
            int y,
            int z
        )
        {
            long maximumDistanceSquared =
                ExistingTraderMatchRadius *
                ExistingTraderMatchRadius;

            foreach (RumorRecord record in registry.Records)
            {
                if (record.Kind != StructureKind.Trader)
                {
                    continue;
                }

                Vec3i center = record.CreateLocation().Center;
                long deltaX = center.X - x;
                long deltaY = center.Y - y;
                long deltaZ = center.Z - z;
                long distanceSquared =
                    deltaX * deltaX +
                    deltaY * deltaY +
                    deltaZ * deltaZ;

                if (distanceSquared <= maximumDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static GeneratedStructure? FindTraderStructure(
            ICoreServerAPI api,
            int x,
            int y,
            int z
        )
        {
            long regionIndex =
                api.WorldManager.MapRegionIndex2DByBlockPos(x, z);

            IMapRegion? region =
                api.WorldManager.GetMapRegion(regionIndex);

            List<GeneratedStructure>? structures =
                region?.GeneratedStructures;

            if (structures == null)
            {
                return null;
            }

            GeneratedStructure? nearest = null;
            long nearestDistance = long.MaxValue;

            foreach (GeneratedStructure candidate in structures)
            {
                if (
                    candidate?.Location == null ||
                    !string.Equals(
                        candidate.Group?.Trim(),
                        "trader",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    !ContainsExpanded(
                        candidate.Location,
                        x,
                        y,
                        z,
                        TraderStructureMatchRadius
                    )
                )
                {
                    continue;
                }

                Vec3i center = candidate.Location.Center;
                long deltaX = center.X - x;
                long deltaZ = center.Z - z;
                long distance =
                    deltaX * deltaX +
                    deltaZ * deltaZ;

                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static bool ContainsExpanded(
            Cuboidi location,
            int x,
            int y,
            int z,
            int radius
        )
        {
            return
                x >= location.X1 - radius &&
                x <= location.X2 + radius &&
                y >= location.Y1 - radius &&
                y <= location.Y2 + radius &&
                z >= location.Z1 - radius &&
                z <= location.Z2 + radius;
        }
    }
}
