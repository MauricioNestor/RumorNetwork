using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RumorNetwork.Purchases
{
    public sealed class RumorPaymentReceipt
    {
        private readonly List<ItemStack>
            removedStacks;

        public IReadOnlyList<ItemStack>
            RemovedStacks => removedStacks;

        public RumorPaymentReceipt(
            IEnumerable<ItemStack> stacks
        )
        {
            removedStacks =
                new List<ItemStack>(stacks);
        }
    }
}
