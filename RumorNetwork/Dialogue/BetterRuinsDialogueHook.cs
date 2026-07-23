using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RumorNetwork.Configuration;
using RumorNetwork.Rumors;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public sealed class BetterRuinsDialogueHook : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.betterruins-dialogue-hook";

        private const string RumorRootCode =
            "rumornetwork-root-rumors";

        private const string RumorOptionsCode =
            "rumornetwork-rumor-options";

        private const string BuyCode =
            "rumornetwork-buybetterruins";

        internal const string SuccessCode =
            "rumornetwork-rumor-betterruins";

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

        private static bool betterRuinsModPresent;

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.78;
        }

        public override void Start(ICoreAPI api)
        {
            betterRuinsModPresent =
                api.ModLoader.IsModEnabled(
                    BetterRuinsRumorPolicy.ModId
                );

            harmony = new Harmony(HarmonyId);

            MethodInfo? execute = AccessTools.Method(
                typeof(DlgTalkComponent),
                nameof(DlgTalkComponent.Execute)
            );

            if (execute == null)
            {
                api.Logger.Error(
                    "Rumor Network could not attach the " +
                    "BetterRuins dialogue option."
                );
                return;
            }

            harmony.Patch(
                execute,
                prefix: new HarmonyMethod(
                    typeof(BetterRuinsDialogueHook),
                    nameof(BeforeTalkComponentExecutes)
                )
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            betterRuinsModPresent = false;
            base.Dispose();
        }

        private static void BeforeTalkComponentExecutes(
            DlgTalkComponent __instance
        )
        {
            if (
                !betterRuinsModPresent ||
                !RumorRuntimeSettings.BetterRuins.Enabled
            )
            {
                return;
            }

            if (TraderDialoguePatch.IsTradingRoot(__instance))
            {
                EnsureRootRumorChoice(__instance);
                return;
            }

            if (!string.Equals(
                    __instance.Code,
                    RumorOptionsCode,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return;
            }

            EnsureBetterRuinsChoice(__instance);
            EnsureBetterRuinsComponents(__instance);
        }

        private static void EnsureRootRumorChoice(
            DlgTalkComponent root
        )
        {
            if (HasChoice(root, RumorRootCode))
            {
                return;
            }

            List<DialogeTextElement> answers = new(
                root.Text ??
                Array.Empty<DialogeTextElement>()
            );

            int nextId = answers
                .Select(answer => answer.Id)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            answers.Add(
                new DialogeTextElement
                {
                    Id = nextId,
                    Value = RumorText.Get(
                        "dialogue-root-rumor-choice"
                    ),
                    JumpTo = RumorRootCode
                }
            );

            root.Text = answers.ToArray();
        }

        private static void EnsureBetterRuinsChoice(
            DlgTalkComponent options
        )
        {
            if (HasChoice(options, BuyCode))
            {
                return;
            }

            List<DialogeTextElement> answers = new(
                options.Text ??
                Array.Empty<DialogeTextElement>()
            );

            int nextId = answers
                .Select(answer => answer.Id)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            int cancelIndex = answers.FindIndex(answer =>
                string.Equals(
                    answer.Value,
                    RumorText.Get("dialogue-rumor-cancel"),
                    StringComparison.Ordinal
                )
            );

            DialogeTextElement choice = new()
            {
                Id = nextId,
                Value = RumorText.Get(
                    "dialogue-rumor-betterruins-pay"
                ),
                JumpTo = BuyCode
            };

            if (cancelIndex >= 0)
            {
                answers.Insert(cancelIndex, choice);
            }
            else
            {
                answers.Add(choice);
            }

            options.Text = answers.ToArray();
        }

        private static void EnsureBetterRuinsComponents(
            DlgTalkComponent options
        )
        {
            if (
                ComponentControllerField == null ||
                ControllerDialogueField == null
            )
            {
                return;
            }

            DialogueController? controller =
                ComponentControllerField.GetValue(options)
                    as DialogueController;

            if (
                controller == null ||
                ControllerDialogueField.GetValue(controller)
                    is not DialogueComponent[] existing
            )
            {
                return;
            }

            List<DialogueComponent> additions = new();

            if (!HasComponent(existing, BuyCode))
            {
                additions.Add(
                    new BetterRuinsActionDialogueComponent
                    {
                        Code = BuyCode,
                        Type = "rumornetwork-betterruins-action"
                    }
                );
            }

            if (!HasComponent(existing, SuccessCode))
            {
                additions.Add(
                    new DlgTalkComponent
                    {
                        Code = SuccessCode,
                        Owner = "npc",
                        Type = "talk",
                        Text = new[]
                        {
                            new DialogeTextElement
                            {
                                Value = RumorText.Get(
                                    "dialogue-rumor-betterruins-success"
                                )
                            }
                        },
                        JumpTo = FindReturnCode(existing)
                    }
                );
            }

            if (additions.Count == 0)
            {
                return;
            }

            int nextId = existing
                .OfType<DlgTalkComponent>()
                .SelectMany(component =>
                    component.Text ??
                    Array.Empty<DialogeTextElement>()
                )
                .Select(element => element.Id)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            GuiDialogueDialog? dialog =
                ComponentDialogField?.GetValue(options)
                    as GuiDialogueDialog;

            foreach (DialogueComponent component in additions)
            {
                component.Init(ref nextId);
                component.SetReferences(
                    controller,
                    dialog
                );
            }

            ControllerDialogueField.SetValue(
                controller,
                existing.Concat(additions).ToArray()
            );
        }

        private static string FindReturnCode(
            IEnumerable<DialogueComponent> components
        )
        {
            DlgTalkComponent? existingResult = components
                .OfType<DlgTalkComponent>()
                .FirstOrDefault(component =>
                    string.Equals(
                        component.Code,
                        "rumornetwork-rumor-exact",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            return existingResult?.JumpTo ?? "opentrade";
        }

        private static bool HasChoice(
            DlgTalkComponent component,
            string jumpTo
        )
        {
            return component.Text?.Any(answer =>
                string.Equals(
                    answer.JumpTo,
                    jumpTo,
                    StringComparison.OrdinalIgnoreCase
                )
            ) == true;
        }

        private static bool HasComponent(
            IEnumerable<DialogueComponent> components,
            string code
        )
        {
            return components.Any(component =>
                string.Equals(
                    component.Code,
                    code,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }
    }

    internal sealed class BetterRuinsActionDialogueComponent :
        DialogueComponent
    {
        public override string Execute()
        {
            if (
                controller.NPCEntity.Api is not ICoreServerAPI sapi ||
                controller.PlayerEntity.Player is not IServerPlayer player
            )
            {
                return null;
            }

            string result = RumorDialogueRuntime.Execute(
                player,
                "buybetterruins"
            );

            string responseCode = result switch
            {
                "betterruins" =>
                    BetterRuinsDialogueHook.SuccessCode,

                "searching" =>
                    "rumornetwork-rumor-searching",

                "nofunds" =>
                    "rumornetwork-rumor-nofunds",

                _ =>
                    "rumornetwork-rumor-failed"
            };

            sapi.Network.SendEntityPacket(
                player,
                controller.NPCEntity.EntityId,
                TraderDialoguePatch.ResultPacketId,
                SerializerUtil.Serialize(responseCode)
            );

            return responseCode;
        }
    }
}
