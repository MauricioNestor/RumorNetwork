namespace RumorNetwork.Configuration
{
    public sealed partial class RumorNetworkConfig
    {
        public RumorDebugConfig Debug =
            RumorDebugConfig.CreateDefault();
    }

    public sealed class RumorDebugConfig
    {
        public bool Enabled = false;

        public static RumorDebugConfig CreateDefault()
        {
            return new RumorDebugConfig();
        }
    }
}
