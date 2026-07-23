using System.Collections.Generic;
using RumorNetwork.Configuration;
using RumorNetwork.Dialogue;
using RumorNetwork.Purchases;
using RumorNetwork.Rumors;

namespace RumorNetwork.Offers
{
    public sealed class RumorOfferService
    {
        private readonly RumorNetworkConfig config;
        private readonly RumorPriceResolver priceResolver;

        public RumorOfferService(
            RumorNetworkConfig config,
            RumorPriceResolver priceResolver
        )
        {
            this.config = config;
            this.priceResolver = priceResolver;
        }

        public bool TryGetOffers(
            out IReadOnlyList<RumorOffer> offers,
            out string error
        )
        {
            List<RumorOffer> resolvedOffers = new();
            offers = resolvedOffers.AsReadOnly();
            error = string.Empty;

            if (
                config.GeneralRumors.Enabled &&
                config.GeneralRumors.ApproximateEnabled &&
                !TryAddGeneralOffer(
                    resolvedOffers,
                    RumorKnowledgeLevel.Approximate,
                    RumorOfferKind.GeneralApproximate,
                    RumorText.Get(
                        "offer-general-approximate-title"
                    ),
                    RumorText.Get(
                        "offer-general-approximate-description"
                    ),
                    out error
                )
            )
            {
                return false;
            }

            if (
                config.GeneralRumors.Enabled &&
                config.GeneralRumors.ExactEnabled &&
                !TryAddGeneralOffer(
                    resolvedOffers,
                    RumorKnowledgeLevel.Exact,
                    RumorOfferKind.GeneralExact,
                    RumorText.Get(
                        "offer-general-exact-title"
                    ),
                    RumorText.Get(
                        "offer-general-exact-description"
                    ),
                    out error
                )
            )
            {
                return false;
            }

            if (config.TraderLocations.Enabled)
            {
                bool traderPriceResolved =
                    priceResolver.TryResolveTraderLocation(
                        out RumorPrice? traderPrice,
                        out error
                    );

                if (
                    !traderPriceResolved ||
                    traderPrice == null
                )
                {
                    return false;
                }

                resolvedOffers.Add(
                    new RumorOffer(
                        "trader-location-exact",
                        RumorOfferKind.TraderLocationExact,
                        RumorText.Get("offer-trader-title"),
                        RumorText.Get(
                            "offer-trader-description"
                        ),
                        RumorKnowledgeLevel.Exact,
                        traderPrice,
                        false
                    )
                );
            }

            offers = resolvedOffers.AsReadOnly();
            return true;
        }

        private bool TryAddGeneralOffer(
            ICollection<RumorOffer> offers,
            RumorKnowledgeLevel knowledge,
            RumorOfferKind kind,
            string title,
            string description,
            out string error
        )
        {
            bool priceResolved =
                priceResolver.TryResolveGeneralPreview(
                    knowledge,
                    out RumorPrice? previewPrice,
                    out error
                );

            if (!priceResolved || previewPrice == null)
            {
                return false;
            }

            string id = knowledge ==
                RumorKnowledgeLevel.Approximate
                    ? "general-approximate"
                    : "general-exact";

            offers.Add(
                new RumorOffer(
                    id,
                    kind,
                    title,
                    description,
                    knowledge,
                    previewPrice,
                    priceResolver.HasStructureSpecificPrice(
                        knowledge
                    )
                )
            );

            return true;
        }
    }
}
