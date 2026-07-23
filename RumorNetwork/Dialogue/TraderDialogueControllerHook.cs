using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public sealed class TraderDialogueControllerHook : ModSystem
    {
        private const string HarmonyId =
            "rumornetwork.trader-dialogue-controller-hook";

        private static readonly ConditionalWeakTable<
            EntityBehaviorConversable,
            object
        > RegisteredBehaviors = new();

        private static readonly FieldInfo? EntityField =
            AccessTools.Field(
                typeof(EntityBehavior),
                "entity"
            );

        private static readonly FieldInfo? BehaviorDialogueField =
            AccessTools.Field(
                typeof(EntityBehaviorConversable),
                "dialogue"
            );

        private static readonly FieldInfo? DialogueLocationField =
            AccessTools.Field(
                typeof(EntityBehaviorConversable),
                "dialogueLoc"
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

            MethodInfo? initialize = AccessTools.Method(
                typeof(EntityBehaviorConversable),
                nameof(EntityBehaviorConversable.Initialize),
                new[]
                {
                    typeof(EntityProperties),
                    typeof(JsonObject)
                }
            );

            if (initialize == null)
            {
                api.Logger.Error(
                    "Rumor Network não encontrou " +
                    "EntityBehaviorConversable.Initialize; " +
                    "não será possível assinar OnControllerCreated."
                );

                return;
            }

            harmony.Patch(
                initialize,
                postfix: new HarmonyMethod(
                    typeof(TraderDialogueControllerHook),
                    nameof(AfterConversableInitialized)
                )
            );
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        private static void AfterConversableInitialized(
            EntityBehaviorConversable __instance
        )
        {
            Entity? entity = EntityField?.GetValue(__instance) as Entity;

            if (entity is not EntityTradingHumanoid)
            {
                return;
            }

            if (RegisteredBehaviors.TryGetValue(__instance, out _))
            {
                return;
            }

            RegisteredBehaviors.Add(__instance, new object());

            __instance.OnControllerCreated += controller =>
                InjectIntoController(__instance, controller);
        }

        private static void InjectIntoController(
            EntityBehaviorConversable behavior,
            DialogueController controller
        )
        {
            if (
                controller == null ||
                controller.NPCEntity is not EntityTradingHumanoid ||
                BehaviorDialogueField == null ||
                ControllerDialogueField == null
            )
            {
                return;
            }

            DialogueConfig? config =
                BehaviorDialogueField.GetValue(behavior)
                    as DialogueConfig;

            DialogueComponent[]? existing =
                config?.components ??
                ControllerDialogueField.GetValue(controller)
                    as DialogueComponent[];

            if (existing == null || existing.Length == 0)
            {
                controller.NPCEntity.Api.Logger.Warning(
                    "Rumor Network recebeu um DialogueController sem " +
                    $"componentes para {controller.NPCEntity.Code}."
                );

                return;
            }

            if (!existing.Any(component =>
                    component.Code ==
                    "rumornetwork-root-traders"
                ))
            {
                if (!TryInjectComponents(
                        existing,
                        out DialogueComponent[] combined,
                        out string failure
                    ))
                {
                    controller.NPCEntity.Api.Logger.Warning(
                        "Rumor Network não conseguiu localizar o menu " +
                        $"principal de {controller.NPCEntity.Code}: " +
                        failure
                    );

                    return;
                }

                existing = combined;
            }

            if (config != null)
            {
                config.components = existing;
                BehaviorDialogueField.SetValue(behavior, config);
            }

            ControllerDialogueField.SetValue(controller, existing);

            foreach (DialogueComponent component in existing)
            {
                component.SetReferences(
                    controller,
                    behavior.Dialog
                );
            }

            AssetLocation? dialogueLocation =
                DialogueLocationField?.GetValue(behavior)
                    as AssetLocation;

            controller.NPCEntity.Api.Logger.Notification(
                "Rumor Network injetou opções de rumores no diálogo " +
                $"{dialogueLocation ?? controller.NPCEntity.Code}."
            );
        }

        private static bool TryInjectComponents(
            DialogueComponent[] existing,
            out DialogueComponent[] combined,
            out string failure
        )
        {
            combined = existing;
            failure = string.Empty;

            DlgTalkComponent? root = FindTradingRoot(existing);
            if (root == null)
            {
                failure = DescribePlayerMenus(existing);
                return false;
            }

            if (
                CreateTraderBranchMethod == null ||
                CreateRumorBranchMethod == null
            )
            {
                failure = "métodos de criação dos submenus não encontrados";
                return false;
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

            object? traderBranches =
                CreateTraderBranchMethod.Invoke(
                    null,
                    new object[] { root.Code }
                );

            object? rumorBranches =
                CreateRumorBranchMethod.Invoke(
                    null,
                    new object[] { root.Code }
                );

            if (
                traderBranches is not
                    IEnumerable<DialogueComponent> traderComponents ||
                rumorBranches is not
                    IEnumerable<DialogueComponent> rumorComponents
            )
            {
                failure = "submenus retornaram formato inesperado";
                return false;
            }

            additions.AddRange(traderComponents);
            additions.AddRange(rumorComponents);

            foreach (DialogueComponent component in additions)
            {
                component.Init(ref nextId);
            }

            combined = existing
                .Concat(additions)
                .ToArray();

            return true;
        }

        private static DlgTalkComponent? FindTradingRoot(
            DialogueComponent[] components
        )
        {
            DlgTalkComponent[] playerMenus = components
                .OfType<DlgTalkComponent>()
                .Where(component =>
                    component.Owner == "player" &&
                    component.Text != null &&
                    component.Text.Length > 0
                )
                .ToArray();

            DlgTalkComponent? directTradeMenu =
                playerMenus.FirstOrDefault(component =>
                    component.Text!.Any(answer =>
                        string.Equals(
                            answer.JumpTo,
                            "opentrade",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                );

            if (directTradeMenu != null)
            {
                return directTradeMenu;
            }

            Dictionary<string, DialogueComponent> byCode =
                components
                    .Where(component =>
                        !string.IsNullOrWhiteSpace(component.Code)
                    )
                    .GroupBy(component => component.Code)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.OrdinalIgnoreCase
                    );

            DlgTalkComponent? indirectTradeMenu =
                playerMenus.FirstOrDefault(component =>
                    component.Text!.Any(answer =>
                        CanReachOpenTrade(
                            answer.JumpTo,
                            byCode,
                            new HashSet<string>(
                                StringComparer.OrdinalIgnoreCase
                            ),
                            0
                        )
                    )
                );

            if (indirectTradeMenu != null)
            {
                return indirectTradeMenu;
            }

            // Vanilla trader root menus are the broad player-choice menus.
            // This fallback also supports dialogue variants whose trade path
            // is implemented through triggers instead of a named opentrade
            // component.
            return playerMenus
                .OrderByDescending(component =>
                    component.Text!.Length
                )
                .FirstOrDefault();
        }

        private static bool CanReachOpenTrade(
            string? code,
            IReadOnlyDictionary<string, DialogueComponent> byCode,
            ISet<string> visited,
            int depth
        )
        {
            if (
                string.IsNullOrWhiteSpace(code) ||
                depth > 12
            )
            {
                return false;
            }

            if (string.Equals(
                    code,
                    "opentrade",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }

            if (!visited.Add(code))
            {
                return false;
            }

            if (!byCode.TryGetValue(code, out DialogueComponent? component))
            {
                return false;
            }

            if (CanReachOpenTrade(
                    component.JumpTo,
                    byCode,
                    visited,
                    depth + 1
                ))
            {
                return true;
            }

            if (component is DlgTalkComponent talk)
            {
                foreach (
                    DialogeTextElement answer
                    in talk.Text ?? Array.Empty<DialogeTextElement>()
                )
                {
                    if (CanReachOpenTrade(
                            answer.JumpTo,
                            byCode,
                            visited,
                            depth + 1
                        ))
                    {
                        return true;
                    }
                }
            }

            if (component is DlgConditionComponent condition)
            {
                return
                    CanReachOpenTrade(
                        condition.ThenJumpTo,
                        byCode,
                        visited,
                        depth + 1
                    ) ||
                    CanReachOpenTrade(
                        condition.ElseJumpTo,
                        byCode,
                        visited,
                        depth + 1
                    );
            }

            return false;
        }

        private static string DescribePlayerMenus(
            IEnumerable<DialogueComponent> components
        )
        {
            string description = string.Join(
                "; ",
                components
                    .OfType<DlgTalkComponent>()
                    .Where(component =>
                        component.Owner == "player"
                    )
                    .Select(component =>
                        $"{component.Code}=[" +
                        string.Join(
                            ",",
                            component.Text?
                                .Select(answer => answer.JumpTo)
                                ?? Array.Empty<string>()
                        ) +
                        "]"
                    )
            );

            return string.IsNullOrWhiteSpace(description)
                ? "nenhum componente de fala do jogador"
                : description;
        }
    }
}
