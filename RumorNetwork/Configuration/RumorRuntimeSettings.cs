namespace RumorNetwork.Configuration
{
    public static class RumorRuntimeSettings
    {
        private static RumorNetworkConfig current =
            CreateDefault();

        public static RumorNetworkConfig Current => current;

        public static GeneralRumorConfig GeneralRumors =>
            current.GeneralRumors;

        public static StructureClassificationConfig
            StructureClassification =>
                current.StructureClassification;

        public static RumorWaypointConfig Waypoints =>
            current.Waypoints;

        public static void Configure(
            RumorNetworkConfig config
        )
        {
            current = config ?? CreateDefault();
        }

        private static RumorNetworkConfig CreateDefault()
        {
            RumorNetworkConfig config = new();
            config.Normalize();
            return config;
        }
    }
}
