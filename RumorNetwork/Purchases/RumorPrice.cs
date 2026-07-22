using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RumorNetwork.Purchases
{
    public sealed class RumorPriceComponent
    {
        public ItemStack Stack { get; }

        public string ConfiguredCode { get; }

        public string Description =>
            $"{Stack.StackSize}x {ConfiguredCode}";

        public RumorPriceComponent(
            ItemStack stack,
            string configuredCode
        )
        {
            Stack = stack;
            ConfiguredCode = configuredCode;
        }
    }

    public sealed class RumorPrice
    {
        private readonly List<RumorPriceComponent>
            components;

        public IReadOnlyList<RumorPriceComponent>
            Components => components;

        public string Description =>
            string.Join(
                " + ",
                components.Select(
                    component =>
                        component.Description
                )
            );

        public RumorPrice(
            IEnumerable<RumorPriceComponent>
                priceComponents
        )
        {
            components =
                new List<RumorPriceComponent>(
                    priceComponents
                );
        }
    }
}
