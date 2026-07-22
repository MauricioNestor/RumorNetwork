using System.Collections.Generic;
using RumorNetwork.Rumors;

namespace RumorNetwork.Configuration
{
    public sealed class RumorNetworkConfig
    {
        public int Version;

        public RumorPricingConfig Pricing =
            RumorPricingConfig.CreateDefault();

        public TraderLocationRumorConfig TraderLocations =
            TraderLocationRumorConfig.CreateDefault();

        public void Normalize()
        {
            Pricing ??=
                RumorPricingConfig.CreateDefault();

            TraderLocations ??=
                TraderLocationRumorConfig.CreateDefault();

            if (Version < 2)
            {
                TraderLocations.ExactPrice =
                    RumorPriceConfig.Single(
                        "game:gear-rusty",
                        4
                    );

                TraderLocations
                    .MaxLocationsSoldPerTrader = 2;
            }

            Pricing.Normalize();
            TraderLocations.Normalize();

            Version = 2;
        }
    }

    public sealed class RumorPricingConfig
    {
        public RumorPriceConfig Fallback =
            RumorPriceConfig.Single(
                "game:gear-rusty",
                1
            );

        public Dictionary<string, RumorPriceConfig>
            KnowledgeDefaults = new();

        public Dictionary<string, RumorStructurePriceConfig>
            Structures = new();

        public static RumorPricingConfig CreateDefault()
        {
            RumorPricingConfig config = new();

            config.KnowledgeDefaults[
                RumorKnowledgeLevel.Approximate.ToString()
            ] = RumorPriceConfig.Single(
                "game:gear-rusty",
                1
            );

            config.KnowledgeDefaults[
                RumorKnowledgeLevel.Exact.ToString()
            ] = RumorPriceConfig.Single(
                "game:gear-rusty",
                3
            );

            config.Structures[
                StructureKind.Translocator.ToString()
            ] = new RumorStructurePriceConfig
            {
                Exact = RumorPriceConfig.Single(
                    "game:gear-temporal",
                    1
                )
            };

            return config;
        }

        public void Normalize()
        {
            Fallback ??=
                RumorPriceConfig.Single(
                    "game:gear-rusty",
                    1
                );

            Fallback.Normalize();

            KnowledgeDefaults ??= new();
            Structures ??= new();

            foreach (
                RumorPriceConfig price
                in KnowledgeDefaults.Values
            )
            {
                price?.Normalize();
            }

            foreach (
                RumorStructurePriceConfig rule
                in Structures.Values
            )
            {
                rule?.Normalize();
            }
        }
    }

    public sealed class TraderLocationRumorConfig
    {
        public bool Enabled = true;

        public double SellerMatchRadius = 48;

        public int MaxLocationsSoldPerTrader = 2;

        public RumorPriceConfig ExactPrice =
            RumorPriceConfig.Single(
                "game:gear-rusty",
                4
            );

        public static TraderLocationRumorConfig
            CreateDefault()
        {
            return new TraderLocationRumorConfig();
        }

        public void Normalize()
        {
            if (SellerMatchRadius <= 0)
            {
                SellerMatchRadius = 48;
            }

            if (MaxLocationsSoldPerTrader <= 0)
            {
                MaxLocationsSoldPerTrader = 2;
            }

            ExactPrice ??=
                RumorPriceConfig.Single(
                    "game:gear-rusty",
                    4
                );

            ExactPrice.Normalize();
        }
    }

    public sealed class RumorStructurePriceConfig
    {
        public RumorPriceConfig? Approximate;

        public RumorPriceConfig? Exact;

        public RumorPriceConfig? Get(
            RumorKnowledgeLevel knowledge
        )
        {
            return knowledge switch
            {
                RumorKnowledgeLevel.Approximate =>
                    Approximate,

                RumorKnowledgeLevel.Exact =>
                    Exact,

                _ =>
                    null
            };
        }

        public void Normalize()
        {
            Approximate?.Normalize();
            Exact?.Normalize();
        }
    }

    public sealed class RumorPriceConfig
    {
        public List<RumorPriceItemConfig> Items =
            new();

        public static RumorPriceConfig Single(
            string code,
            int quantity,
            string type = "item"
        )
        {
            return new RumorPriceConfig
            {
                Items = new List<RumorPriceItemConfig>
                {
                    new()
                    {
                        Type = type,
                        Code = code,
                        Quantity = quantity
                    }
                }
            };
        }

        public void Normalize()
        {
            Items ??= new();
        }
    }

    public sealed class RumorPriceItemConfig
    {
        public string Type = "item";

        public string Code =
            "game:gear-rusty";

        public int Quantity = 1;
    }
}
