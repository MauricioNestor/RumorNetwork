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

        private static readonly FieldInfo? DialogueField =
            AccessTools.Field(
                typeof(DialogueController),
                "dialogue"
            );

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.8;
        }

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);

            MethodInfo? getController = AccessTools.Method(
                typeof(EntityBehaviorConversable),
                nameof(EntityBehaviorConversable.GetOrCreateController)
            );

            MethodInfo? receiveServerPacket = AccessTools.Method(
                typeof(EntityBehaviorConversable),
                nameof(EntityBehaviorConversable.OnReceivedServerPacket)
            );

            if (getController == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "EntityBehaviorConversable.GetOrCreateController; " +
                    "o diálogo de rumores não será injetado."
                );
            }
            else
            {
                harmony.Patch(
                    getController,
                    postfix: new HarmonyMethod(
                        typeof(TraderDialoguePatch),
                        nameof(OnControllerCreated)
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

        private static void OnControllerCreated(
            EntityBehaviorConversable __instance,
            DialogueController __result
        )
        {
            if (
                __result == null ||
                __result.NPCEntity is not EntityTrader ||
                DialogueField == null
            )
            {
                return;
            }

            DialogueComponent[]? existing =
                DialogueField.GetValue(__result)
                    as DialogueComponent[];

            if (
                existing == null ||
                existing.Any(component =>
                    component.Code ==
                    "rumornetwork-root-traders"
                )
            )
            {
                return;
            }

            DlgTalkComponent? root = FindRoot(existing);
            if (root == null)
            {
                __result.NPCEntity.Api.Logger.Warning(
                    "Rumor Network não encontrou o menu principal " +
                    $"do diálogo de {__result.NPCEntity.Code}."
                );

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

            additions.AddRange(CreateTraderBranch(
                root.Code,
                ref nextId
            ));

            additions.AddRange(CreateRumorBranch(
                root.Code,
                ref nextId
            ));

            foreach (DialogueComponent component in additions)
            {
                component.SetReferences(
                    __result,
                    __instance.Dialog
                );
            }

            DialogueField.SetValue(
                __result,
                existing.Concat(additions).ToArray()
            );

            __result.NPCEntity.Api.Logger.VerboseDebug(
                "Rumor Network injetou opções de rumores no " +
                $"diálogo de {__result.NPCEntity.Code}."
            );
        }

        private static DlgTalkComponent? FindRoot(
            IEnumerable<DialogueComponent> components
        )
        {
            DlgTalkComponent[] playerTalk = components
                .OfType<DlgTalkComponent>()
                .Where(component =>
                    component.Owner == "player"
                )
                .ToArray();

            return playerTalk.FirstOrDefault(component =>
                    component.Text?.Any(answer =>
                        answer.JumpTo == "opentrade"
                    ) == true
                )
                ?? playerTalk.FirstOrDefault();
        }

        private static IEnumerable<DialogueComponent>
            CreateTraderBranch(
                string rootCode,
                ref int nextId
            )
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
                    ref nextId,
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
            CreateRumorBranch(
                string rootCode,
                ref int nextId
            )
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
                    ref nextId,
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
            ref int nextId,
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
                    Id = nextId++,
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
