using HarmonyLib;
using RumorNetwork.Dialogue;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RumorNetwork.Catalog
{
    public sealed class RepairedTranslocatorPatch : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.repaired-translocators";

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.75;
        }

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);

            var doRepair = AccessTools.Method(
                typeof(BlockEntityStaticTranslocator),
                nameof(BlockEntityStaticTranslocator.DoRepair)
            );

            var initialize = AccessTools.Method(
                typeof(BlockEntityStaticTranslocator),
                nameof(BlockEntityStaticTranslocator.Initialize)
            );

            if (doRepair != null)
            {
                harmony.Patch(
                    doRepair,
                    postfix: new HarmonyMethod(
                        typeof(RepairedTranslocatorPatch),
                        nameof(RemoveAfterRepair)
                    )
                );
            }

            if (initialize != null)
            {
                harmony.Patch(
                    initialize,
                    postfix: new HarmonyMethod(
                        typeof(RepairedTranslocatorPatch),
                        nameof(RemoveLoadedRepaired)
                    )
                );
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private static void RemoveAfterRepair(
            BlockEntityStaticTranslocator __instance
        )
        {
            RemoveIfRepaired(__instance);
        }

        private static void RemoveLoadedRepaired(
            BlockEntityStaticTranslocator __instance
        )
        {
            RemoveIfRepaired(__instance);
        }

        private static void RemoveIfRepaired(
            BlockEntityStaticTranslocator translocator
        )
        {
            if (
                translocator?.Api?.Side != EnumAppSide.Server ||
                !translocator.FullyRepaired ||
                translocator.Pos == null
            )
            {
                return;
            }

            RumorDialogueRuntime.MarkTranslocatorRepaired(
                translocator.Pos
            );
        }
    }
}
