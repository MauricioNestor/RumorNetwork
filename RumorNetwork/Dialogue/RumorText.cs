using Vintagestory.API.Config;

namespace RumorNetwork.Dialogue
{
    internal static class RumorText
    {
        public static string Get(
            string key,
            params object[] args
        )
        {
            return Lang.Get(
                $"rumornetwork:{key}",
                args
            );
        }
    }
}
