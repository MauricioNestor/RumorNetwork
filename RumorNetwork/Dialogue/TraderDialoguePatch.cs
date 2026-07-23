using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public sealed class TraderDialoguePatch : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.trader-dialogue";

        private static readonly ConditionalWeakTable<
            DialogueController,
            object
        > AttachedControllers = new();

        private Harmony? harmony;

        public override double ExecuteOrder()
        {
            return -0.8;
        }

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(HarmonyId);

            var loadDialogue = AccessTools.Method(
                typeof(EntityBehaviorConversable),
                "loadDialogue"
            );

            var getController = AccessTools.Method(
                typeof(EntityBehaviorConversable),
                nameof(EntityBehaviorConversable.GetOrCreateController)
            );

            if (loadDialogue != null)
            {
                harmony.Patch(
                    loadDialogue,
                    postfix: new HarmonyMethod(
                        typeof(TraderDialoguePatch),
                        nameof(AppendRumorDialogue)
                    )
                );
            }

            if (getController != null)
            {
                harmony.Patch(
                    getController,
                    postfix: new HarmonyMethod(
                        typeof(TraderDialoguePatch),
                        nameof(AttachTriggerHandler)
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

        private static void AppendRumorDialogue(
            EntityBehaviorConversable __instance,
            ref DialogueConfig __result
        )
        {
            if (__result?.components == null)
            {
                return;
            }

            Entity? entity = Traverse.Create(__instance)
                .Field("entity")
                .GetValue<Entity>();

            if (entity is not EntityTrader)
            {
                return;
            }

            if (__result.components.Any(
                    component =>
                        component.Code == "rumornetwork-root-traders"
                ))
            {
                return;
            }

            DlgTalkComponent? root = __result.components
                .OfType<DlgTalkComponent>()
                .FirstOrDefault(component =>
                    component.Owner == "player" &&
                    component.Text?.Any(answer =>
                        answer.JumpTo == "opentrade"
                    ) == true
                )
                ?? __result.components
                    .OfType<DlgTalkComponent>()
                    .FirstOrDefault(component =>
                        component.Owner == "player"
                    );

            if (root == null)
            {
                return;
            }

            int nextId = __result.components
                .OfType<DlgTalkComponent>()
                .SelectMany(component =>
                    component.Text ?? Array.Empty<DialogeTextElement>()
                )
                .Select(element => element.Id)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            List<DialogeTextElement> rootAnswers =
                new(root.Text ?? Array.Empty<DialogeTextElement>());

            rootAnswers.Add(new DialogeTextElement
            {
                Id = nextId++,
                Value = "Do you know any other traders around?",
                JumpTo = "rumornetwork-root-traders"
            });

            rootAnswers.Add(new DialogeTextElement
            {
                Id = nextId++,
                Value = "Have you heard any rumors lately?",
                JumpTo = "rumornetwork-root-rumors"
            });

            root.Text = rootAnswers.ToArray();

            List<DialogueComponent> components =
                new(__result.components);

            components.AddRange(CreateTraderBranch(
                root.Code,
                ref nextId
            ));

            components.AddRange(CreateRumorBranch(
                root.Code,
                ref nextId
            ));

            __result.components = components.ToArray();
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
                    ("I'll pay four rusty gears.", "rumornetwork-buytrader"),
                    ("I don't have that many gears.", rootCode)
                ),
                Trigger(
                    "rumornetwork-buytrader",
                    "buytrader",
                    "rumornetwork-trader-success-check"
                ),
                Condition(
                    "rumornetwork-trader-success-check",
                    "success",
                    "rumornetwork-trader-success",
                    "rumornetwork-trader-search-check"
                ),
                Condition(
                    "rumornetwork-trader-search-check",
                    "searching",
                    "rumornetwork-trader-searching",
                    "rumornetwork-trader-quota-check"
                ),
                Condition(
                    "rumornetwork-trader-quota-check",
                    "quota",
                    "rumornetwork-trader-quota",
                    "rumornetwork-trader-failed"
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
                    "rumornetwork-trader-failed",
                    "I can't arrange that trade right now.",
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
                    ("I'll pay one rusty gear for whatever you've heard.", "rumornetwork-buyapproximate"),
                    ("Here are three rusty gears. I want reliable information.", "rumornetwork-buyexact"),
                    ("I have a temporal gear. I want exceptional gossip.", "rumornetwork-buytranslocator"),
                    ("Never mind. I'm not interested.", rootCode)
                ),
                Trigger(
                    "rumornetwork-buyapproximate",
                    "buyapproximate",
                    "rumornetwork-rumor-result"
                ),
                Trigger(
                    "rumornetwork-buyexact",
                    "buyexact",
                    "rumornetwork-rumor-result"
                ),
                Trigger(
                    "rumornetwork-buytranslocator",
                    "buytranslocator",
                    "rumornetwork-rumor-result"
                ),
                Condition(
                    "rumornetwork-rumor-result",
                    "approximate",
                    "rumornetwork-rumor-approximate",
                    "rumornetwork-rumor-exact-check"
                ),
                Condition(
                    "rumornetwork-rumor-exact-check",
                    "exact",
                    "rumornetwork-rumor-exact",
                    "rumornetwork-rumor-translocator-check"
                ),
                Condition(
                    "rumornetwork-rumor-translocator-check",
                    "translocator",
                    "rumornetwork-rumor-translocator",
                    "rumornetwork-rumor-search-check"
                ),
                Condition(
                    "rumornetwork-rumor-search-check",
                    "searching",
                    "rumornetwork-rumor-searching",
                    "rumornetwork-rumor-failed"
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
                    "I know the location of an intact ancient translocator. I've marked it precisely on your map.",
                    rootCode
                ),
                NpcTalk(
                    "rumornetwork-rumor-searching",
                    "That kind of information takes time. Ask me again in a moment.",
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

            for (int index = 0;
                index < answers.Length;
                index++)
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

        private static DlgGenericComponent Trigger(
            string code,
            string action,
            string jumpTo
        )
        {
            return new DlgGenericComponent
            {
                Code = code,
                Type = "trigger",
                Trigger = "rumornetwork-action",
                TriggerData = JsonObject.FromJson(
                    "{\"action\":\"" + action + "\"}"
                ),
                JumpTo = jumpTo
            };
        }

        private static DlgConditionComponent Condition(
            string code,
            string expected,
            string thenJumpTo,
            string elseJumpTo
        )
        {
            return new DlgConditionComponent
            {
                Code = code,
                Type = "condition",
                Variable = "player.rumornetworkresult",
                IsValue = expected,
                ThenJumpTo = thenJumpTo,
                ElseJumpTo = elseJumpTo
            };
        }

        private static void AttachTriggerHandler(
            DialogueController __result
        )
        {
            if (
                __result == null ||
                __result.NPCEntity is not EntityTrader ||
                __result.NPCEntity.Api.Side != EnumAppSide.Server
            )
            {
                return;
            }

            if (AttachedControllers.TryGetValue(
                    __result,
                    out _
                ))
            {
                return;
            }

            AttachedControllers.Add(__result, new object());
            __result.DialogTriggers += (
                triggeringEntity,
                value,
                data
            ) =>
            {
                if (
                    value != "rumornetwork-action" ||
                    triggeringEntity is not EntityPlayer playerEntity ||
                    playerEntity.Player is not IServerPlayer player
                )
                {
                    return -1;
                }

                string action = data?["action"].AsString() ?? string.Empty;
                string result = RumorDialogueRuntime.Execute(
                    player,
                    action
                );

                __result.VarSys.SetVariable(
                    EnumActivityVariableScope.Player,
                    playerEntity,
                    "rumornetworkresult",
                    result
                );

                return -1;
            };
        }
    }
}
