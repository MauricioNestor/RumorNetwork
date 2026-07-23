using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public sealed class TraderDialogueControllerHook : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.trader-dialogue-controller-hook";

        private const string TraderRootCode =
            "rumornetwork-root-traders";

        private const string RumorRootCode =
            "rumornetwork-root-rumors";

        private static readonly FieldInfo? ComponentControllerField =
            AccessTools.Field(
                typeof(DialogueComponent),
                "controller"
            );

        private static readonly FieldInfo? ComponentDialogField =
            AccessTools.Field(
                typeof(DialogueComponent),
                "dialog"
            );

        private static readonly FieldInfo? ControllerDialogueField =
            AccessTools.Field(
                typeof(DialogueController),
                "dialogue"
            );

        private static readonly MethodInfo? CreateTraderBranchMethod =
            AccessTools.Method(
                typeof(TraderDialoguePatch),
                "CreateTraderBranch"
            );

        private static readonly MethodInfo? CreateRumorBranchMethod =
            AccessTools.Method(
                typeof(TraderDialoguePatch),
                "CreateRumorBranch"
            );

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.79;
        }

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);

            MethodInfo? execute = AccessTools.Method(
                typeof(DlgTalkComponent),
                nameof(DlgTalkComponent.Execute)
            );

            if (execute == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "DlgTalkComponent.Execute; as opções de rumores " +
                    "não poderão ser anexadas ao menu exibido."
                );

                return;
            }

            harmony.Patch(
                execute,
                prefix: new HarmonyMethod(
                    typeof(TraderDialogueControllerHook),
                    nameof(BeforeTalkComponentExecutes)
                )
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private static void BeforeTalkComponentExecutes(
            DlgTalkComponent __instance
        )
        {
            if (!string.Equals(
                    __instance.Owner,
                    "player",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return;
            }

            if (
                ComponentControllerField == null ||
                ControllerDialogueField == null
            )
            {
                return;
            }

            DialogueController? controller =
                ComponentControllerField.GetValue(__instance)
                    as DialogueController;

            if (
                controller?.NPCEntity is not EntityTradingHumanoid ||
                ControllerDialogueField.GetValue(controller)
                    is not DialogueComponent[] existing ||
                existing.Length == 0
            )
            {
                return;
            }

            bool menuAlreadyLinked =
                HasRumorChoices(__instance);

            bool traderBranchExists = existing.Any(component =>
                string.Equals(
                    component.Code,
                    TraderRootCode,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            bool rumorBranchExists = existing.Any(component =>
                string.Equals(
                    component.Code,
                    RumorRootCode,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (
                menuAlreadyLinked &&
                traderBranchExists &&
                rumorBranchExists
            )
            {
                return;
            }

            int nextId = FindNextAnswerId(existing);

            if (!menuAlreadyLinked)
            {
                AppendRootChoices(
                    __instance,
                    ref nextId
                );
            }

            List<DialogueComponent> additions = new();

            if (!traderBranchExists)
            {
                if (!TryCreateBranch(
                        CreateTraderBranchMethod,
                        __instance.Code,
                        additions
                    ))
                {
                    controller.NPCEntity.Api.Logger.Warning(
                        "Rumor Network não conseguiu criar o submenu " +
                        "de localização de comerciantes."
                    );

                    return;
                }
            }

            if (!rumorBranchExists)
            {
                if (!TryCreateBranch(
                        CreateRumorBranchMethod,
                        __instance.Code,
                        additions
                    ))
                {
                    controller.NPCEntity.Api.Logger.Warning(
                        "Rumor Network não conseguiu criar o submenu " +
                        "de rumores gerais."
                    );

                    return;
                }
            }

            foreach (DialogueComponent component in additions)
            {
                component.Init(ref nextId);
            }

            DialogueComponent[] combined = additions.Count == 0
                ? existing
                : existing.Concat(additions).ToArray();

            ControllerDialogueField.SetValue(
                controller,
                combined
            );

            GuiDialogueDialog? dialog =
                ComponentDialogField?.GetValue(__instance)
                    as GuiDialogueDialog;

            foreach (DialogueComponent component in additions)
            {
                component.SetReferences(
                    controller,
                    dialog
                );
            }

            controller.NPCEntity.Api.Logger.Notification(
                "Rumor Network anexou opções ao menu exibido " +
                $"{__instance.Code} de {controller.NPCEntity.Code}. " +
                $"Respostas={__instance.Text?.Length ?? 0}."
            );
        }

        private static bool HasRumorChoices(
            DlgTalkComponent component
        )
        {
            return component.Text?.Any(answer =>
                string.Equals(
                    answer.JumpTo,
                    TraderRootCode,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                string.Equals(
                    answer.JumpTo,
                    RumorRootCode,
                    StringComparison.OrdinalIgnoreCase
                )
            ) == true;
        }

        private static int FindNextAnswerId(
            IEnumerable<DialogueComponent> components
        )
        {
            return components
                .OfType<DlgTalkComponent>()
                .SelectMany(component =>
                    component.Text ??
                    Array.Empty<DialogeTextElement>()
                )
                .Select(element => element.Id)
                .DefaultIfEmpty(-1)
                .Max() + 1;
        }

        private static void AppendRootChoices(
            DlgTalkComponent root,
            ref int nextId
        )
        {
            List<DialogeTextElement> answers = new(
                root.Text ??
                Array.Empty<DialogeTextElement>()
            );

            answers.Add(new DialogeTextElement
            {
                Id = nextId++,
                Value =
                    "Do you know any other traders around?",
                JumpTo = TraderRootCode
            });

            answers.Add(new DialogeTextElement
            {
                Id = nextId++,
                Value =
                    "Have you heard any rumors lately?",
                JumpTo = RumorRootCode
            });

            root.Text = answers.ToArray();
        }

        private static bool TryCreateBranch(
            MethodInfo? factory,
            string rootCode,
            ICollection<DialogueComponent> destination
        )
        {
            if (factory == null)
            {
                return false;
            }

            object? result = factory.Invoke(
                null,
                new object[] { rootCode }
            );

            if (
                result is not
                    IEnumerable<DialogueComponent> components
            )
            {
                return false;
            }

            foreach (DialogueComponent component in components)
            {
                destination.Add(component);
            }

            return true;
        }
    }
}
