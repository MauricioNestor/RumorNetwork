using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RumorNetwork.Configuration;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public sealed class TraderDialoguePatch : ModSystem
    {
        internal const int ResultPacketId = 22137;

        private const string HarmonyId =
            "rumornetwork.trader-dialogue";

        private const string TraderRootCode =
            "rumornetwork-root-traders";

        private const string RumorRootCode =
            "rumornetwork-root-rumors";

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.8;
        }

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);

            MethodInfo? loadDialogue =
                AccessTools.DeclaredMethod(
                    typeof(EntityBehaviorConversable),
                    "loadDialogue",
                    new[]
                    {
                        typeof(AssetLocation),
                        typeof(EntityPlayer)
                    }
                );

            MethodInfo? receiveServerPacket =
                AccessTools.Method(
                    typeof(EntityBehaviorConversable),
                    nameof(
                        EntityBehaviorConversable
                            .OnReceivedServerPacket
                    )
                );

            if (loadDialogue == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "EntityBehaviorConversable.loadDialogue; " +
                    "o diálogo de rumores não será injetado."
                );
            }
            else
            {
                harmony.Patch(
                    loadDialogue,
                    postfix: new HarmonyMethod(
                        typeof(TraderDialoguePatch),
                        nameof(InjectRumorDialogue)
                    )
                );
            }

            if (receiveServerPacket == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "EntityBehaviorConversable.OnReceivedServerPacket; " +
                    "respostas do diálogo não poderão voltar ao cliente."
                );
            }
            else
            {
                harmony.Patch(
                    receiveServerPacket,
                    prefix: new HarmonyMethod(
                        typeof(TraderDialoguePatch),
                        nameof(ReceiveRumorResult)
                    )
                );
            }

            api.Logger.Notification(
                "Rumor Network registrou a integração de diálogo dos comerciantes."
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private static void InjectRumorDialogue(
            EntityBehaviorConversable __instance,
            AssetLocation loc,
            ref DialogueConfig __result
        )
        {
            if (__result?.components == null)
            {
                return;
            }

            DialogueComponent[] existing =
                __result.components;

            DlgTalkComponent? root =
                FindTradingRoot(existing);

            if (root == null)
            {
                return;
            }

            bool traderChoiceExists =
                HasChoice(root, TraderRootCode);

            bool rumorChoiceExists =
                HasChoice(root, RumorRootCode);

            bool traderBranchExists =
                HasComponent(existing, TraderRootCode);

            bool rumorBranchExists =
                HasComponent(existing, RumorRootCode);

            if (
                traderChoiceExists &&
                rumorChoiceExists &&
                traderBranchExists &&
                rumorBranchExists
            )
            {
                return;
            }

            int nextId = FindNextAnswerId(existing);

            AppendRootChoices(
                root,
                ref nextId,
                traderChoiceExists,
                rumorChoiceExists
            );

            List<DialogueComponent> additions = new();

            if (!traderBranchExists)
            {
                additions.AddRange(
                    CreateTraderBranch(root.Code)
                );
            }

            if (!rumorBranchExists)
            {
                additions.AddRange(
                    CreateRumorBranch(root.Code)
                );
            }

            foreach (DialogueComponent component in additions)
            {
                component.Init(ref nextId);
            }

            if (additions.Count > 0)
            {
                __result.components = existing
                    .Concat(additions)
                    .ToArray();
            }

            Console.WriteLine(
                "Rumor Network injetou opções de rumores no " +
                $"diálogo {loc}."
            );
        }

        internal static bool IsTradingRoot(
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

        private static DlgTalkComponent? FindTradingRoot(
            IEnumerable<DialogueComponent> components
        )
        {
            return components
                .OfType<DlgTalkComponent>()
                .FirstOrDefault(IsTradingRoot);
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
            ref int nextId,
            bool traderChoiceExists,
            bool rumorChoiceExists
        )
        {
            bool traderEnabled =
                RumorRuntimeSettings
                    .Current
                    .TraderLocations
                    .Enabled;

            GeneralRumorConfig general =
                RumorRuntimeSettings.GeneralRumors;

            bool rumorEnabled =
                general.Enabled &&
                (
                    general.ApproximateEnabled ||
                    general.ExactEnabled ||
                    general.TranslocatorEnabled
                );

            List<DialogeTextElement> answers = new(
                root.Text ??
                Array.Empty<DialogeTextElement>()
            );

            if (!traderChoiceExists && traderEnabled)
            {
                answers.Add(new DialogeTextElement
                {
                    Id = nextId++,
                    Value = RumorText.Get(
                        "dialogue-root-trader-choice"
                    ),
                    JumpTo = TraderRootCode
                });
            }

            if (!rumorChoiceExists && rumorEnabled)
            {
                answers.Add(new DialogeTextElement
                {
                    Id = nextId++,
                    Value = RumorText.Get(
                        "dialogue-root-rumor-choice"
                    ),
                    JumpTo = RumorRootCode
                });
            }

            root.Text = answers.ToArray();
        }

        private static IEnumerable<DialogueComponent>
            CreateTraderBranch(string rootCode)
        {
            return new DialogueComponent[]
            {
                NpcTalk(
                    TraderRootCode,
                    RumorText.Get("dialogue-trader-intro"),
                    "rumornetwork-trader-options"
                ),
                PlayerTalk(
                    "rumornetwork-trader-options",
                    (
                        RumorText.Get("dialogue-trader-pay"),
                        "rumornetwork-buytrader"
                    ),
                    (
                        RumorText.Get("dialogue-trader-cancel"),
                        rootCode
                    )
                ),
                Action(
                    "rumornetwork-buytrader",
                    "buytrader"
                ),
                NpcTalk(
                    "rumornetwork-trader-success",
                    RumorText.Get("dialogue-trader-success"),
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-searching",
                    RumorText.Get("dialogue-trader-searching"),
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-quota",
                    RumorText.Get("dialogue-trader-quota"),
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-nofunds",
                    RumorText.Get("dialogue-trader-nofunds"),
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-failed",
                    RumorText.Get("dialogue-trader-failed"),
                    rootCode
                )
            };
        }

        private static IEnumerable<DialogueComponent>
            CreateRumorBranch(string rootCode)
        {
            GeneralRumorConfig config =
                RumorRuntimeSettings.GeneralRumors;

            List<(string Text, string JumpTo)> answers = new();
            List<DialogueComponent> components = new();

            components.Add(
                NpcTalk(
                    RumorRootCode,
                    RumorText.Get("dialogue-rumor-intro"),
                    "rumornetwork-rumor-options"
                )
            );

            if (config.Enabled && config.ApproximateEnabled)
            {
                answers.Add(
                    (
                        RumorText.Get(
                            "dialogue-rumor-approximate-pay"
                        ),
                        "rumornetwork-buyapproximate"
                    )
                );
            }

            if (config.Enabled && config.ExactEnabled)
            {
                answers.Add(
                    (
                        RumorText.Get(
                            "dialogue-rumor-exact-pay"
                        ),
                        "rumornetwork-buyexact"
                    )
                );
            }

            if (config.Enabled && config.TranslocatorEnabled)
            {
                answers.Add(
                    (
                        RumorText.Get(
                            "dialogue-rumor-translocator-pay"
                        ),
                        "rumornetwork-buytranslocator"
                    )
                );
            }

            answers.Add(
                (
                    RumorText.Get("dialogue-rumor-cancel"),
                    rootCode
                )
            );

            components.Add(
                PlayerTalk(
                    "rumornetwork-rumor-options",
                    answers.ToArray()
                )
            );

            if (config.Enabled && config.ApproximateEnabled)
            {
                components.Add(
                    Action(
                        "rumornetwork-buyapproximate",
                        "buyapproximate"
                    )
                );
            }

            if (config.Enabled && config.ExactEnabled)
            {
                components.Add(
                    Action(
                        "rumornetwork-buyexact",
                        "buyexact"
                    )
                );
            }

            if (config.Enabled && config.TranslocatorEnabled)
            {
                components.Add(
                    Action(
                        "rumornetwork-buytranslocator",
                        "buytranslocator"
                    )
                );
            }

            components.AddRange(
                new DialogueComponent[]
                {
                    NpcTalk(
                        "rumornetwork-rumor-approximate",
                        RumorText.Get(
                            "dialogue-rumor-approximate-success"
                        ),
                        rootCode
                    ),
                    NpcTalk(
                        "rumornetwork-rumor-exact",
                        RumorText.Get(
                            "dialogue-rumor-exact-success"
                        ),
                        rootCode
                    ),
                    NpcTalk(
                        "rumornetwork-rumor-translocator",
                        RumorText.Get(
                            "dialogue-rumor-translocator-success"
                        ),
                        rootCode
                    ),
                    NpcTalk(
                        "rumornetwork-rumor-searching",
                        RumorText.Get(
                            "dialogue-rumor-searching"
                        ),
                        rootCode
                    ),
                    NpcTalk(
                        "rumornetwork-rumor-nofunds",
                        RumorText.Get(
                            "dialogue-rumor-nofunds"
                        ),
                        rootCode
                    ),
                    NpcTalk(
                        "rumornetwork-rumor-failed",
                        RumorText.Get(
                            "dialogue-rumor-failed"
                        ),
                        rootCode
                    )
                }
            );

            return components;
        }

        private static DlgTalkComponent NpcTalk(
            string code,
            string text,
            string jumpTo
        )
        {
            return new DlgTalkComponent
            {
                Code = code,
                Owner = "npc",
                Type = "talk",
                Text = new[]
                {
                    new DialogeTextElement
                    {
                        Value = text
                    }
                },
                JumpTo = jumpTo
            };
        }

        private static DlgTalkComponent PlayerTalk(
            string code,
            params (string Text, string JumpTo)[] answers
        )
        {
            DialogeTextElement[] elements =
                new DialogeTextElement[answers.Length];

            for (
                int index = 0;
                index < answers.Length;
                index++
            )
            {
                elements[index] = new DialogeTextElement
                {
                    Value = answers[index].Text,
                    JumpTo = answers[index].JumpTo
                };
            }

            return new DlgTalkComponent
            {
                Code = code,
                Owner = "player",
                Type = "talk",
                Text = elements
            };
        }

        private static RumorActionDialogueComponent Action(
            string code,
            string action
        )
        {
            return new RumorActionDialogueComponent
            {
                Code = code,
                Type = "rumornetwork-action",
                Action = action
            };
        }

        internal static string ResolveResponseCode(
            string action,
            string result
        )
        {
            if (action == "buytrader")
            {
                return result switch
                {
                    "success" =>
                        "rumornetwork-trader-success",
                    "searching" =>
                        "rumornetwork-trader-searching",
                    "quota" =>
                        "rumornetwork-trader-quota",
                    "nofunds" =>
                        "rumornetwork-trader-nofunds",
                    _ =>
                        "rumornetwork-trader-failed"
                };
            }

            return result switch
            {
                "approximate" =>
                    "rumornetwork-rumor-approximate",
                "exact" =>
                    "rumornetwork-rumor-exact",
                "translocator" =>
                    "rumornetwork-rumor-translocator",
                "searching" =>
                    "rumornetwork-rumor-searching",
                "nofunds" =>
                    "rumornetwork-rumor-nofunds",
                _ =>
                    "rumornetwork-rumor-failed"
            };
        }

        private static bool ReceiveRumorResult(
            EntityBehaviorConversable __instance,
            int packetid,
            byte[] data,
            ref EnumHandling handled
        )
        {
            if (packetid != ResultPacketId)
            {
                return true;
            }

            handled = EnumHandling.PreventDefault;

            string responseCode =
                SerializerUtil.Deserialize<string>(data);

            DialogueController? controller =
                __instance.ControllerByPlayer
                    .Values
                    .FirstOrDefault();

            controller?.JumpTo(responseCode);
            return false;
        }
    }

    internal sealed class RumorActionDialogueComponent :
        DialogueComponent
    {
        public string Action { get; set; } = string.Empty;

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
                Action
            );

            string responseCode =
                TraderDialoguePatch.ResolveResponseCode(
                    Action,
                    result
                );

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
