namespace RumorNetwork.Configuration
{
    public static class RumorRuntimeSettings
    {
        private static RumorNetworkConfig current =
            CreateDefault();

        public static RumorNetworkConfig Current => current;

        public static bool BetterRuinsInstalled { get; private set; }

        public static GeneralRumorConfig GeneralRumors =>
            current.GeneralRumors;

        public static BetterRuinsRumorConfig BetterRuins =>
            current.BetterRuins;

        public static StructureClassificationConfig
            StructureClassification =>
                current.StructureClassification;

        public static RumorWaypointConfig Waypoints =>
            current.Waypoints;

        public static void Configure(
            RumorNetworkConfig config,
            bool betterRuinsInstalled = false
        )
        {
            current = config ?? CreateDefault();
            BetterRuinsInstalled = betterRuinsInstalled;
        }

        private static RumorNetworkConfig CreateDefault()
        {
            RumorNetworkConfig config = new();
            config.BetterRuins ??=
                BetterRuinsRumorConfig.CreateDefault();
            config.BetterRuins.Normalize();
            config.Normalize();
            return config;
        }
    }
}
