using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
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

        private static Harmony? sharedHarmony;
        private static bool installed;

        public override double ExecuteOrder()
        {
            return -0.77;
        }

        public override void Start(ICoreAPI api)
        {
            if (installed)
            {
                return;
            }

            installed = true;
            sharedHarmony = new Harmony(HarmonyId);

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

            sharedHarmony.Patch(execute, prefix: prefix);

            api.Logger.Notification(
                "Rumor Network instalou explicitamente a preservação " +
                "de opções externas de diálogo."
            );
        }

        public override void Dispose()
        {
            // This patch may be installed manually by the main mod system.
            // Leave it process-wide until shutdown.
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

            if (!snapshot.RestorePending)
            {
                // A normal root execution starts or refreshes the dialogue
                // session. Store fresh independent copies so conditions and
                // quest state from other mods are not kept stale forever.
                snapshot.Replace(externalChoices);
                return;
            }

            List<DialogeTextElement> restored =
                snapshot.CreateCopies();

            restored.AddRange(
                current
                    .Where(IsRumorNetworkChoice)
                    .Select(CloneChoice)
            );

            snapshot.RestorePending = false;
            __instance.Text = restored.ToArray();

            controller.NPCEntity.Api.Logger.Notification(
                "Rumor Network reconstruiu o menu raiz com " +
                $"{snapshot.Count} opção(ões) externas independentes " +
                $"para {controller.NPCEntity.Code}."
            );
        }

        private static void MarkRestorePending(
            DialogueController controller
        )
        {
            string entityPrefix = CreateEntityPrefix(controller);

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
                CreateEntityPrefix(controller) +
                (rootCode ?? string.Empty);
        }

        private static string CreateEntityPrefix(
            DialogueController controller
        )
        {
            return
                controller.NPCEntity.Api.Side + "|" +
                controller.NPCEntity.EntityId + "|";
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

        private static DialogeTextElement CloneChoice(
            DialogeTextElement choice
        )
        {
            try
            {
                string json = JsonConvert.SerializeObject(choice);

                return JsonConvert.DeserializeObject<DialogeTextElement>(
                    json
                ) ?? choice;
            }
            catch
            {
                // A shallow fallback is still preferable to deleting an
                // option. Normal BetterRuins dialogue elements serialize.
                return choice;
            }
        }

        private sealed class RootSnapshot
        {
            private readonly List<DialogeTextElement> choices = new();

            public int Count => choices.Count;

            public bool RestorePending { get; set; }

            public RootSnapshot(
                IEnumerable<DialogeTextElement> source
            )
            {
                Replace(source);
            }

            public void Replace(
                IEnumerable<DialogeTextElement> source
            )
            {
                choices.Clear();
                choices.AddRange(source.Select(CloneChoice));
            }

            public List<DialogeTextElement> CreateCopies()
            {
                return choices.Select(CloneChoice).ToList();
            }
        }
    }
}
