namespace RumorNetwork.Rumors
{
    public sealed class RumorPreparedDelivery
    {
        public RumorRecord Record { get; }

        public RumorKnowledgeLevel Knowledge { get; }

        public RumorTargetResolution Resolution { get; }

        public RumorPreparedDelivery(
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTargetResolution resolution
        )
        {
            Record = record;
            Knowledge = knowledge;
            Resolution = resolution;
        }
    }
}
