using RumorNetwork.Purchases;
using RumorNetwork.Rumors;

namespace RumorNetwork.Offers
{
    public sealed class RumorOffer
    {
        public string Id { get; }

        public RumorOfferKind Kind { get; }

        public string Title { get; }

        public string Description { get; }

        public RumorKnowledgeLevel Knowledge { get; }

        public RumorPrice PreviewPrice { get; }

        public bool PriceMayVary { get; }

        public RumorOffer(
            string id,
            RumorOfferKind kind,
            string title,
            string description,
            RumorKnowledgeLevel knowledge,
            RumorPrice previewPrice,
            bool priceMayVary
        )
        {
            Id = id;
            Kind = kind;
            Title = title;
            Description = description;
            Knowledge = knowledge;
            PreviewPrice = previewPrice;
            PriceMayVary = priceMayVary;
        }
    }
}
