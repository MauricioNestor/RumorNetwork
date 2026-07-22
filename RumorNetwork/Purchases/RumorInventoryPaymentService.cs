using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Purchases
{
    public sealed class RumorInventoryPaymentService
    {
        private readonly ICoreServerAPI api;
        private readonly ILogger logger;

        public RumorInventoryPaymentService(
            ICoreServerAPI api,
            ILogger logger
        )
        {
            this.api = api;
            this.logger = logger;
        }

        public bool TryTake(
            IServerPlayer player,
            RumorPrice price,
            out RumorPaymentReceipt? receipt,
            out string error
        )
        {
            receipt = null;
            error = string.Empty;

            List<string> shortages =
                FindShortages(
                    player,
                    price
                );

            if (shortages.Count > 0)
            {
                error =
                    "Pagamento insuficiente. Faltam: " +
                    string.Join(
                        ", ",
                        shortages
                    ) +
                    ".";

                return false;
            }

            List<ItemStack> removedStacks = new();

            foreach (
                RumorPriceComponent component
                in price.Components
            )
            {
                int remaining =
                    component.Stack.StackSize;

                player.Entity.WalkInventory(
                    slot =>
                    {
                        if (remaining <= 0)
                        {
                            return false;
                        }

                        ItemStack? ownedStack =
                            slot.Itemstack;

                        if (
                            ownedStack == null ||
                            !slot.CanTake() ||
                            !Matches(
                                ownedStack,
                                component.Stack
                            )
                        )
                        {
                            return true;
                        }

                        int takeQuantity =
                            Math.Min(
                                remaining,
                                ownedStack.StackSize
                            );

                        ItemStack? removed =
                            slot.TakeOut(
                                takeQuantity
                            );

                        slot.MarkDirty();

                        if (
                            removed != null &&
                            removed.StackSize > 0
                        )
                        {
                            removedStacks.Add(removed);
                            remaining -=
                                removed.StackSize;
                        }

                        return remaining > 0;
                    }
                );

                if (remaining > 0)
                {
                    RumorPaymentReceipt partialReceipt =
                        new(removedStacks);

                    Refund(
                        player,
                        partialReceipt,
                        out _
                    );

                    error =
                        "O inventário mudou durante o " +
                        "pagamento. Nada foi cobrado.";

                    return false;
                }
            }

            receipt =
                new RumorPaymentReceipt(
                    removedStacks
                );

            return true;
        }

        public bool Refund(
            IServerPlayer player,
            RumorPaymentReceipt receipt,
            out string error
        )
        {
            error = string.Empty;

            try
            {
                foreach (
                    ItemStack removedStack
                    in receipt.RemovedStacks
                )
                {
                    ItemStack refundStack =
                        removedStack.Clone();

                    bool fullyGiven =
                        player.InventoryManager
                            .TryGiveItemstack(
                                refundStack,
                                true
                            );

                    if (
                        !fullyGiven &&
                        refundStack.StackSize > 0
                    )
                    {
                        Vec3d dropPosition = new(
                            player.Entity.Pos.X,
                            player.Entity.Pos.Y,
                            player.Entity.Pos.Z
                        );

                        api.World.SpawnItemEntity(
                            refundStack,
                            dropPosition
                        );

                        logger.Warning(
                            "Parte de um reembolso do " +
                            "Rumor Network não coube no " +
                            "inventário e foi deixada aos " +
                            "pés do jogador."
                        );
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                error =
                    "O pagamento não pôde ser devolvido: " +
                    exception.Message;

                logger.Error(error);
                return false;
            }
        }

        private static List<string> FindShortages(
            IServerPlayer player,
            RumorPrice price
        )
        {
            List<string> shortages = new();

            foreach (
                RumorPriceComponent component
                in price.Components
            )
            {
                int available =
                    CountMatching(
                        player,
                        component.Stack
                    );

                int required =
                    component.Stack.StackSize;

                if (available >= required)
                {
                    continue;
                }

                shortages.Add(
                    $"{required - available}x " +
                    component.ConfiguredCode
                );
            }

            return shortages;
        }

        private static int CountMatching(
            IServerPlayer player,
            ItemStack requiredStack
        )
        {
            int count = 0;

            player.Entity.WalkInventory(
                slot =>
                {
                    ItemStack? ownedStack =
                        slot.Itemstack;

                    if (
                        ownedStack != null &&
                        slot.CanTake() &&
                        Matches(
                            ownedStack,
                            requiredStack
                        )
                    )
                    {
                        count +=
                            ownedStack.StackSize;
                    }

                    return true;
                }
            );

            return count;
        }

        private static bool Matches(
            ItemStack ownedStack,
            ItemStack requiredStack
        )
        {
            return
                ownedStack.Class
                    == requiredStack.Class &&
                ownedStack.Id
                    == requiredStack.Id;
        }
    }
}
