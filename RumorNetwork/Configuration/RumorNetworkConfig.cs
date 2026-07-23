using System.Collections.Generic;
using Newtonsoft.Json;
using RumorNetwork.Rumors;

namespace RumorNetwork.Configuration
{
    public sealed class RumorNetworkConfig
    {
        public int Version;

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public RumorPricingConfig Pricing =
            RumorPricingConfig.CreateDefault();

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public TraderLocationRumorConfig TraderLocations =
            TraderLocationRumorConfig.CreateDefault();

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public RemoteStructureCatalogConfig RemoteCatalog =
            RemoteStructureCatalogConfig.CreateDefault();

        public void Normalize()
        {
            Pricing ??=
                RumorPricingConfig.CreateDefault();

            TraderLocations ??=
                TraderLocationRumorConfig.CreateDefault();

            RemoteCatalog ??=
                RemoteStructureCatalogConfig.CreateDefault();

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

            if (Version < 4)
            {
                // Older config files were populated into the field
                // initializer collections by Json.NET. Every reload could
                // append another 4-gear component, producing prices such
                // as 8, 12 and 16 gears. Reset once and use replacement
                // collection semantics from now on.
                TraderLocations.ExactPrice =
                    RumorPriceConfig.Single(
                        "game:gear-rusty",
                        4
                    );

                RemoteCatalog.ScanOnPlayerReady = false;
                RemoteCatalog.PeekIntervalMs = 250;
                RemoteCatalog.MaxConcurrentPeeks = 1;
                RemoteCatalog.MaxPeekColumnsPerSearch = 2048;
                RemoteCatalog.MaxSearchRadiusChunks = 96;
                RemoteCatalog.PauseWhenGeneratingChunksAbove = 8;
                RemoteCatalog.StopAfterNewTargets = 1;
            }

            Pricing.Normalize();
            TraderLocations.Normalize();
            RemoteCatalog.Normalize();

            Version = 4;
        }
    }

    public sealed class RumorPricingConfig
    {
        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public RumorPriceConfig Fallback =
            RumorPriceConfig.Single(
                "game:gear-rusty",
                1
            );

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public Dictionary<string, RumorPriceConfig>
            KnowledgeDefaults = new();

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
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

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
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

    public sealed class RemoteStructureCatalogConfig
    {
        public bool Enabled = true;

        public bool ScanOnPlayerReady = false;

        // Delay between starting peeks. This is the main sustained CPU
        // throttle. Peeks already running are not interrupted.
        public int PeekIntervalMs = 250;

        // Keep this low: each peek performs temporary world generation.
        public int MaxConcurrentPeeks = 1;

        // Hard per-search CPU/work budget.
        public int MaxPeekColumnsPerSearch = 2048;

        // Geometric limit around the seller/player, in chunk columns.
        public int MaxSearchRadiusChunks = 96;

        // Give normal player-driven worldgen priority.
        public int PauseWhenGeneratingChunksAbove = 8;

        // Stop as soon as this many new verified targets of the requested
        // kind have entered the registry.
        public int StopAfterNewTargets = 1;

        public static RemoteStructureCatalogConfig
            CreateDefault()
        {
            return new RemoteStructureCatalogConfig();
        }

        public void Normalize()
        {
            if (PeekIntervalMs < 25)
            {
                PeekIntervalMs = 25;
            }

            if (MaxConcurrentPeeks <= 0)
            {
                MaxConcurrentPeeks = 1;
            }

            if (MaxConcurrentPeeks > 4)
            {
                MaxConcurrentPeeks = 4;
            }

            if (MaxPeekColumnsPerSearch <= 0)
            {
                MaxPeekColumnsPerSearch = 2048;
            }

            if (MaxSearchRadiusChunks <= 0)
            {
                MaxSearchRadiusChunks = 96;
            }

            if (MaxSearchRadiusChunks > 1024)
            {
                MaxSearchRadiusChunks = 1024;
            }

            if (PauseWhenGeneratingChunksAbove < 0)
            {
                PauseWhenGeneratingChunksAbove = 0;
            }

            if (StopAfterNewTargets <= 0)
            {
                StopAfterNewTargets = 1;
            }

            if (StopAfterNewTargets > 8)
            {
                StopAfterNewTargets = 8;
            }
        }
    }

    public sealed class RumorStructurePriceConfig
    {
        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
        public RumorPriceConfig? Approximate;

        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
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
        [JsonProperty(
            ObjectCreationHandling =
                ObjectCreationHandling.Replace
        )]
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
