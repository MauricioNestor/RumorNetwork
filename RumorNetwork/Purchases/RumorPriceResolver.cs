using System;
using System.Collections.Generic;
using RumorNetwork.Configuration;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;

namespace RumorNetwork.Purchases
{
    public sealed class RumorPriceResolver
    {
        private readonly IWorldAccessor world;
        private readonly RumorNetworkConfig config;

        public RumorPriceResolver(
            IWorldAccessor world,
            RumorNetworkConfig config
        )
        {
            this.world = world;
            this.config = config;
        }

        public bool TryResolve(
            StructureKind structureKind,
            RumorKnowledgeLevel knowledge,
            out RumorPrice? price,
            out string error
        )
        {
            RumorPriceConfig selectedConfig =
                SelectPriceConfig(
                    structureKind,
                    knowledge
                );

            return TryResolveConfig(
                selectedConfig,
                out price,
                out error
            );
        }

        public bool TryResolveGeneralPreview(
            RumorKnowledgeLevel knowledge,
            out RumorPrice? price,
            out string error
        )
        {
            RumorPriceConfig selectedConfig =
                FindKnowledgePrice(
                    knowledge.ToString()
                ) ?? config.Pricing.Fallback;

            return TryResolveConfig(
                selectedConfig,
                out price,
                out error
            );
        }

        public bool HasStructureSpecificPrice(
            RumorKnowledgeLevel knowledge
        )
        {
            foreach (
                RumorStructurePriceConfig rule
                in config.Pricing.Structures.Values
            )
            {
                if (rule?.Get(knowledge) != null)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveTraderLocation(
            out RumorPrice? price,
            out string error
        )
        {
            if (!config.TraderLocations.Enabled)
            {
                price = null;
                error =
                    "Rumores de comerciantes estão " +
                    "desativados na configuração.";

                return false;
            }

            return TryResolveConfig(
                config.TraderLocations.ExactPrice,
                out price,
                out error
            );
        }

        private RumorPriceConfig SelectPriceConfig(
            StructureKind structureKind,
            RumorKnowledgeLevel knowledge
        )
        {
            RumorStructurePriceConfig? structureRule =
                FindStructureRule(
                    structureKind.ToString()
                );

            RumorPriceConfig? structurePrice =
                structureRule?.Get(knowledge);

            if (structurePrice != null)
            {
                return structurePrice;
            }

            RumorPriceConfig? knowledgePrice =
                FindKnowledgePrice(
                    knowledge.ToString()
                );

            return knowledgePrice
                ?? config.Pricing.Fallback;
        }

        private RumorStructurePriceConfig?
            FindStructureRule(
                string requestedKey
            )
        {
            foreach (
                KeyValuePair<
                    string,
                    RumorStructurePriceConfig
                > pair
                in config.Pricing.Structures
            )
            {
                if (string.Equals(
                        pair.Key,
                        requestedKey,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private RumorPriceConfig? FindKnowledgePrice(
            string requestedKey
        )
        {
            foreach (
                KeyValuePair<string, RumorPriceConfig> pair
                in config.Pricing.KnowledgeDefaults
            )
            {
                if (string.Equals(
                        pair.Key,
                        requestedKey,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private bool TryResolveConfig(
            RumorPriceConfig priceConfig,
            out RumorPrice? price,
            out string error
        )
        {
            price = null;
            error = string.Empty;

            if (
                priceConfig.Items == null ||
                priceConfig.Items.Count == 0
            )
            {
                error =
                    "A configuração de preço não possui itens.";

                return false;
            }

            List<RumorPriceComponent> components = new();

            Dictionary<string, RumorPriceComponent>
                mergedComponents =
                    new(StringComparer.Ordinal);

            foreach (
                RumorPriceItemConfig configuredItem
                in priceConfig.Items
            )
            {
                if (configuredItem.Quantity <= 0)
                {
                    error =
                        $"Quantidade inválida para " +
                        $"{configuredItem.Code}: " +
                        $"{configuredItem.Quantity}.";

                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                        configuredItem.Code
                    ))
                {
                    error =
                        "Um item de preço não possui código.";

                    return false;
                }

                bool resolved =
                    TryResolveCollectible(
                        configuredItem,
                        out CollectibleObject? collectible,
                        out EnumItemClass itemClass,
                        out error
                    );

                if (!resolved || collectible == null)
                {
                    return false;
                }

                string componentKey =
                    $"{(int)itemClass}:{collectible.Id}";

                if (mergedComponents.TryGetValue(
                        componentKey,
                        out RumorPriceComponent?
                            existingComponent
                    ))
                {
                    existingComponent.Stack.StackSize +=
                        configuredItem.Quantity;

                    continue;
                }

                ItemStack stack = new(
                    collectible,
                    configuredItem.Quantity
                );

                RumorPriceComponent component = new(
                    stack,
                    configuredItem.Code
                );

                mergedComponents.Add(
                    componentKey,
                    component
                );

                components.Add(component);
            }

            price = new RumorPrice(components);
            return true;
        }

        private bool TryResolveCollectible(
            RumorPriceItemConfig configuredItem,
            out CollectibleObject? collectible,
            out EnumItemClass itemClass,
            out string error
        )
        {
            collectible = null;
            itemClass = EnumItemClass.Item;
            error = string.Empty;

            AssetLocation code;

            try
            {
                code = new AssetLocation(
                    configuredItem.Code
                );
            }
            catch (Exception exception)
            {
                error =
                    $"Código inválido " +
                    $"{configuredItem.Code}: " +
                    $"{exception.Message}";

                return false;
            }

            if (string.Equals(
                    configuredItem.Type,
                    "item",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                itemClass = EnumItemClass.Item;
                collectible = world.GetItem(code);
            }
            else if (string.Equals(
                    configuredItem.Type,
                    "block",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                itemClass = EnumItemClass.Block;
                collectible = world.GetBlock(code);
            }
            else
            {
                error =
                    $"Tipo de preço inválido " +
                    $"'{configuredItem.Type}' para " +
                    $"{configuredItem.Code}. " +
                    "Use item ou block.";

                return false;
            }

            if (collectible == null)
            {
                error =
                    "O preço referencia um collectible " +
                    $"inexistente: {configuredItem.Type} " +
                    $"{configuredItem.Code}.";

                return false;
            }

            return true;
        }
    }
}
