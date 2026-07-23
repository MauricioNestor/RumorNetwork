using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
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

            if (existing.Any(component =>
                    component.Code ==
                    "rumornetwork-root-traders"
                ))
            {
                return;
            }

            DlgTalkComponent? root =
                FindTradingRoot(existing);

            if (root == null)
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

            List<DialogeTextElement> rootAnswers =
                new(
                    root.Text ??
                    Array.Empty<DialogeTextElement>()
                );

            rootAnswers.Add(new DialogeTextElement
            {
                Id = nextId++,
                Value =
                    "Do you know any other traders around?",
                JumpTo = "rumornetwork-root-traders"
            });

            rootAnswers.Add(new DialogeTextElement
            {
                Id = nextId++,
                Value =
                    "Have you heard any rumors lately?",
                JumpTo = "rumornetwork-root-rumors"
            });

            root.Text = rootAnswers.ToArray();

            List<DialogueComponent> additions = new();

            additions.AddRange(
                CreateTraderBranch(root.Code)
            );

            additions.AddRange(
                CreateRumorBranch(root.Code)
            );

            foreach (DialogueComponent component in additions)
            {
                component.Init(ref nextId);
            }

            __result.components = existing
                .Concat(additions)
                .ToArray();

            Console.WriteLine(
                "Rumor Network injetou opções de rumores no " +
                $"diálogo {loc}."
            );
        }

        private static DlgTalkComponent? FindTradingRoot(
            IEnumerable<DialogueComponent> components
        )
        {
            return components
                .OfType<DlgTalkComponent>()
                .FirstOrDefault(component =>
                    component.Owner == "player" &&
                    component.Text?.Any(answer =>
                        answer.JumpTo == "opentrade"
                    ) == true
                );
        }

        private static IEnumerable<DialogueComponent>
            CreateTraderBranch(string rootCode)
        {
            return new DialogueComponent[]
            {
                NpcTalk(
                    "rumornetwork-root-traders",
                    "One of my colleagues may be stationed nearby. I can mark their location on your map for four rusty gears.",
                    "rumornetwork-trader-options"
                ),
                PlayerTalk(
                    "rumornetwork-trader-options",
                    (
                        "I'll pay four rusty gears.",
                        "rumornetwork-buytrader"
                    ),
                    (
                        "I don't have that many gears.",
                        rootCode
                    )
                ),
                Action(
                    "rumornetwork-buytrader",
                    "buytrader"
                ),
                NpcTalk(
                    "rumornetwork-trader-success",
                    "There. I've marked my colleague's location on your map.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-searching",
                    "Give me a moment. I need to think about who might still be operating nearby. Ask me again shortly.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-quota",
                    "That's everyone I know how to reach.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-nofunds",
                    "Come back when you have four rusty gears.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-trader-failed",
                    "I don't know of any other traders I can point you toward. Sorry.",
                    rootCode
                )
            };
        }

        private static IEnumerable<DialogueComponent>
            CreateRumorBranch(string rootCode)
        {
            return new DialogueComponent[]
            {
                NpcTalk(
                    "rumornetwork-root-rumors",
                    "Certainly. But useful information has a price.",
                    "rumornetwork-rumor-options"
                ),
                PlayerTalk(
                    "rumornetwork-rumor-options",
                    (
                        "I'll pay one rusty gear for whatever you've heard.",
                        "rumornetwork-buyapproximate"
                    ),
                    (
                        "Here are three rusty gears. I want reliable information.",
                        "rumornetwork-buyexact"
                    ),
                    (
                        "I have a temporal gear. I want exceptional gossip.",
                        "rumornetwork-buytranslocator"
                    ),
                    (
                        "Never mind. I'm not interested.",
                        rootCode
                    )
                ),
                Action(
                    "rumornetwork-buyapproximate",
                    "buyapproximate"
                ),
                Action(
                    "rumornetwork-buyexact",
                    "buyexact"
                ),
                Action(
                    "rumornetwork-buytranslocator",
                    "buytranslocator"
                ),
                NpcTalk(
                    "rumornetwork-rumor-approximate",
                    "I've marked the general area on your map. You'll have to do some searching yourself.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-rumor-exact",
                    "I've marked the exact location. Try not to get yourself killed.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-rumor-translocator",
                    "I know the location of an unrepaired ancient translocator. I've marked it precisely on your map.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-rumor-searching",
                    "That kind of information takes time. Ask me again in a moment.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-rumor-nofunds",
                    "Useful information is not free. Come back when you can pay.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-rumor-failed",
                    "I don't have anything useful for you right now.",
                    rootCode
                )
            };
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

            return null;
        }
    }
}
