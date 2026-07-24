using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public sealed class CanonicalTraderDialogueRootPatch : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.canonical-trader-dialogue-root";

        private static readonly FieldInfo? ComponentControllerField =
            AccessTools.Field(
                typeof(DialogueComponent),
                "controller"
            );

        private static readonly FieldInfo? ControllerDialogueField =
            AccessTools.Field(
                typeof(DialogueController),
                "dialogue"
            );

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            // Install before the existing dialogue integration systems.
            return -0.82;
        }

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);

            PatchRootSelector(api);
            PatchRuntimeHookGuard(
                api,
                typeof(TraderDialogueControllerHook),
                "BeforeTalkComponentExecutes"
            );
            PatchRuntimeHookGuard(
                api,
                typeof(BetterRuinsDialogueHook),
                "BeforeTalkComponentExecutes"
            );

            api.Logger.Notification(
                "Rumor Network instalou a seleção canônica da raiz " +
                "dos diálogos de comerciantes."
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private void PatchRootSelector(ICoreAPI api)
        {
            MethodInfo? selector = AccessTools.Method(
                typeof(TraderDialoguePatch),
                "FindTradingRoot"
            );

            if (selector == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou FindTradingRoot para " +
                    "corrigir o retorno dos submenus."
                );
                return;
            }

            harmony!.Patch(
                selector,
                postfix: new HarmonyMethod(
                    typeof(CanonicalTraderDialogueRootPatch),
                    nameof(SelectCanonicalRoot)
                )
            );
        }

        private void PatchRuntimeHookGuard(
            ICoreAPI api,
            Type hookType,
            string methodName
        )
        {
            MethodInfo? hook = AccessTools.Method(
                hookType,
                methodName
            );

            if (hook == null)
            {
                api.Logger.Warning(
                    "Rumor Network não encontrou " +
                    $"{hookType.Name}.{methodName} para limitar a " +
                    "injeção à raiz canônica."
                );
                return;
            }

            harmony!.Patch(
                hook,
                prefix: new HarmonyMethod(
                    typeof(CanonicalTraderDialogueRootPatch),
                    nameof(AllowCanonicalRootOnly)
                )
            );
        }

        private static void SelectCanonicalRoot(
            IEnumerable<DialogueComponent> __0,
            ref DlgTalkComponent? __result
        )
        {
            __result = FindCanonicalRoot(__0);
        }

        private static bool AllowCanonicalRootOnly(
            DlgTalkComponent __0
        )
        {
            if (!IsTradingRootCandidate(__0))
            {
                // Internal Rumor Network branches and unrelated components
                // still need the original hook behavior.
                return true;
            }

            DialogueController? controller =
                ComponentControllerField?.GetValue(__0)
                    as DialogueController;

            if (
                controller == null ||
                ControllerDialogueField?.GetValue(controller)
                    is not DialogueComponent[] components
            )
            {
                // Failing open is safer than deleting all integration when a
                // future game version changes a private field name.
                return true;
            }

            DlgTalkComponent? canonical =
                FindCanonicalRoot(components);

            if (canonical == null)
            {
                return true;
            }

            return
                ReferenceEquals(canonical, __0) ||
                string.Equals(
                    canonical.Code,
                    __0.Code,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        private static DlgTalkComponent? FindCanonicalRoot(
            IEnumerable<DialogueComponent> components
        )
        {
            List<DlgTalkComponent> candidates = components
                .OfType<DlgTalkComponent>()
                .Where(IsTradingRootCandidate)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            DlgTalkComponent? namedMain = candidates
                .FirstOrDefault(component =>
                    string.Equals(
                        component.Code,
                        "main",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            if (namedMain != null)
            {
                return namedMain;
            }

            // Compatibility fallback for dialogue packs that use another code
            // for the persistent menu. Introductory responses usually contain
            // only a few choices, while the real trading root is the broadest
            // player menu.
            return candidates
                .OrderByDescending(component =>
                    component.Text?.Length ?? 0
                )
                .ThenBy(component =>
                    component.Code ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase
                )
                .First();
        }

        private static bool IsTradingRootCandidate(
            DlgTalkComponent component
        )
        {
            return
                string.Equals(
                    component.Owner,
                    "player",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                component.Text?.Any(answer =>
                    string.Equals(
                        answer.JumpTo,
                        "opentrade",
                        StringComparison.OrdinalIgnoreCase
                    )
                ) == true;
        }
    }
}
