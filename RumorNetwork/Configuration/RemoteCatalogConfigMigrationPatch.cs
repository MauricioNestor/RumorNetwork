using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Configuration
{
    public sealed class RemoteCatalogConfigMigrationPatch
        : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.remote-catalog-config-v5";

        private const int ConfigVersion = 5;
        private const int PreviousDefaultIntervalMs = 250;
        private const int NewDefaultIntervalMs = 100;

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.95;
        }

        public override void StartServerSide(
            ICoreServerAPI api
        )
        {
            harmony = new Harmony(HarmonyId);

            var normalize = AccessTools.Method(
                typeof(RumorNetworkConfig),
                nameof(RumorNetworkConfig.Normalize)
            );

            if (normalize == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou Normalize para " +
                    "migrar o intervalo do catálogo remoto."
                );

                return;
            }

            harmony.Patch(
                normalize,
                prefix: new HarmonyMethod(
                    typeof(RemoteCatalogConfigMigrationPatch),
                    nameof(CapturePreviousVersion)
                ),
                postfix: new HarmonyMethod(
                    typeof(RemoteCatalogConfigMigrationPatch),
                    nameof(ApplyMigration)
                )
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private static void CapturePreviousVersion(
            RumorNetworkConfig __instance,
            out int __state
        )
        {
            __state = __instance.Version;
        }

        private static void ApplyMigration(
            RumorNetworkConfig __instance,
            int __state
        )
        {
            if (
                __state < ConfigVersion &&
                __instance.RemoteCatalog != null &&
                __instance.RemoteCatalog.PeekIntervalMs ==
                    PreviousDefaultIntervalMs
            )
            {
                __instance.RemoteCatalog.PeekIntervalMs =
                    NewDefaultIntervalMs;
            }

            if (__instance.Version < ConfigVersion)
            {
                __instance.Version = ConfigVersion;
            }
        }
    }
}
