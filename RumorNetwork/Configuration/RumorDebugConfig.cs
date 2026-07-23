namespace RumorNetwork.Configuration
{
    public sealed partial class RumorNetworkConfig
    {
        public RumorDebugConfig Debug =
            RumorDebugConfig.CreateDefault();

        private void NormalizeDebug(
            int sourceVersion
        )
        {
            if (sourceVersion < 6)
            {
                Debug = RumorDebugConfig.CreateDefault();
            }

            Debug ??= RumorDebugConfig.CreateDefault();
            Debug.Normalize();
        }
    }

    public sealed class RumorDebugConfig
    {
        public bool Enabled = false;

        public static RumorDebugConfig CreateDefault()
        {
            return new RumorDebugConfig();
        }

        public void Normalize()
        {
        }
    }
}
