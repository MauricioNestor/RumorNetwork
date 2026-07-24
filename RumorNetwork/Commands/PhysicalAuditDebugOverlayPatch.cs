using System.Reflection;
using HarmonyLib;
using RumorNetwork.Rumors;
using RumorNetwork.Structures;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class PhysicalAuditDebugOverlayPatch : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.physical-audit-debug-overlay";

        private static ICoreServerAPI? serverApi;
        private static ILogger? logger;

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.90;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;
            logger = api.Logger;
            harmony = new Harmony(HarmonyId);

            MethodInfo? method = AccessTools.Method(
                typeof(CaveDebugCommands),
                "ShowDebugDeliveryOverlay"
            );

            if (method == null)
            {
                api.Logger.Warning(
                    "Rumor Network não encontrou o comando de overlay " +
                    "para anexar a auditoria física."
                );
                return;
            }

            harmony.Patch(
                method,
                postfix: new HarmonyMethod(
                    typeof(PhysicalAuditDebugOverlayPatch),
                    nameof(LogPhysicalAudit)
                )
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            serverApi = null;
            logger = null;
            base.Dispose();
        }

        private static void LogPhysicalAudit(
            IServerPlayer player,
            int debugToken
        )
        {
            ICoreServerAPI? api = serverApi;

            if (
                api == null ||
                !RumorDebugDeliveryRegistry.TryGet(
                    debugToken,
                    player.PlayerUID,
                    out RumorDebugDeliverySnapshot? snapshot
                ) ||
                snapshot == null
            )
            {
                return;
            }

            StructurePhysicalAuditResult audit =
                StructurePhysicalAuditor.Audit(
                    api,
                    snapshot.Record
                );

            logger?.Notification(
                "=== Rumor Network: physical structure audit ==="
            );

            logger?.Notification(
                $"DebugToken=d{debugToken} | " +
                $"ChunksLoaded={audit.ChunksLoaded} | " +
                $"DeclaredVolume={audit.DeclaredVolume} | " +
                $"Stride={audit.SampleStride} | " +
                $"Sampled={audit.SampledBlocks}"
            );

            logger?.Notification(
                $"Air={audit.AirBlocks} | " +
                $"Natural={audit.NaturalBlocks} | " +
                $"Artificial={audit.ArtificialBlocks} | " +
                $"Meta={audit.MetaBlocks} | " +
                $"BlockEntities={audit.BlockEntities} | " +
                $"HasStructuralEvidence={audit.HasStructuralEvidence}"
            );

            logger?.Notification(
                $"TopBlocks={audit.FormatTopBlocks()}"
            );
        }
    }
}
