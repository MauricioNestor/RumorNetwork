using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public sealed class ExternalDialogueOptionPreservationPatch :
        ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.external-dialogue-option-preservation";

        private const string RumorNetworkPrefix = "rumornetwork-";

        private static readonly FieldInfo? ComponentControllerField =
            AccessTools.Field(
                typeof(DialogueComponent),
                "controller"
            );

        private static readonly Dictionary<string, RootSnapshot>
            Snapshots = new(StringComparer.Ordinal);

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.77;
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
                    "Rumor Network não encontrou DlgTalkComponent.Execute " +
                    "para preservar opções de diálogo de outros mods."
                );

                return;
            }

            HarmonyMethod prefix = new(
                typeof(ExternalDialogueOptionPreservationPatch),
                nameof(BeforeTalkComponentExecutes)
            )
            {
                priority = Priority.Last
            };

            harmony.Patch(execute, prefix: prefix);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            Snapshots.Clear();
            base.Dispose();
        }

        private static void BeforeTalkComponentExecutes(
            DlgTalkComponent __instance
        )
        {
            DialogueController? controller =
                ComponentControllerField?.GetValue(__instance)
                    as DialogueController;

            if (controller?.NPCEntity == null)
            {
                return;
            }

            if (IsRumorNetworkBranch(__instance.Code))
            {
                MarkRestorePending(controller);
                return;
            }

            if (!TraderDialoguePatch.IsTradingRoot(__instance))
            {
                return;
            }

            string key = CreateKey(controller, __instance.Code);
            List<DialogeTextElement> current = new(
                __instance.Text ??
                Array.Empty<DialogeTextElement>()
            );

            List<DialogeTextElement> externalChoices = current
                .Where(answer => !IsRumorNetworkChoice(answer))
                .ToList();

            if (!Snapshots.TryGetValue(key, out RootSnapshot? snapshot))
            {
                Snapshots[key] = new RootSnapshot(externalChoices);
                return;
            }

            snapshot.Remember(externalChoices);

            if (!snapshot.RestorePending)
            {
                return;
            }

            List<DialogeTextElement> missing = snapshot.Choices
                .Where(saved => !current.Any(candidate =>
                    SameChoice(saved, candidate)
                ))
                .ToList();

            snapshot.RestorePending = false;

            if (missing.Count == 0)
            {
                return;
            }

            int firstRumorNetworkChoice = current.FindIndex(
                IsRumorNetworkChoice
            );

            if (firstRumorNetworkChoice < 0)
            {
                current.AddRange(missing);
            }
            else
            {
                current.InsertRange(
                    firstRumorNetworkChoice,
                    missing
                );
            }

            __instance.Text = current.ToArray();

            controller.NPCEntity.Api.Logger.Notification(
                "Rumor Network restaurou " +
                $"{missing.Count} opção(ões) externas do diálogo de " +
                $"{controller.NPCEntity.Code}."
            );
        }

        private static void MarkRestorePending(
            DialogueController controller
        )
        {
            string entityPrefix =
                controller.NPCEntity.EntityId + "|";

            foreach (
                KeyValuePair<string, RootSnapshot> pair
                in Snapshots
            )
            {
                if (pair.Key.StartsWith(
                        entityPrefix,
                        StringComparison.Ordinal
                    ))
                {
                    pair.Value.RestorePending = true;
                }
            }
        }

        private static string CreateKey(
            DialogueController controller,
            string rootCode
        )
        {
            return
                controller.NPCEntity.EntityId + "|" +
                (rootCode ?? string.Empty);
        }

        private static bool IsRumorNetworkBranch(string? code)
        {
            return code?.StartsWith(
                RumorNetworkPrefix,
                StringComparison.OrdinalIgnoreCase
            ) == true;
        }

        private static bool IsRumorNetworkChoice(
            DialogeTextElement answer
        )
        {
            return answer?.JumpTo?.StartsWith(
                RumorNetworkPrefix,
                StringComparison.OrdinalIgnoreCase
            ) == true;
        }

        private static bool SameChoice(
            DialogeTextElement first,
            DialogeTextElement second
        )
        {
            if (
                !string.IsNullOrEmpty(first.JumpTo) ||
                !string.IsNullOrEmpty(second.JumpTo)
            )
            {
                return string.Equals(
                    first.JumpTo,
                    second.JumpTo,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            return string.Equals(
                first.Value,
                second.Value,
                StringComparison.Ordinal
            );
        }

        private sealed class RootSnapshot
        {
            public List<DialogeTextElement> Choices { get; } = new();

            public bool RestorePending { get; set; }

            public RootSnapshot(
                IEnumerable<DialogeTextElement> choices
            )
            {
                Remember(choices);
            }

            public void Remember(
                IEnumerable<DialogeTextElement> choices
            )
            {
                foreach (DialogeTextElement choice in choices)
                {
                    if (!Choices.Any(saved =>
                            SameChoice(saved, choice)
                        ))
                    {
                        Choices.Add(choice);
                    }
                }
            }
        }
    }
}
