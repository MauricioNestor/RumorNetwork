using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Configuration
{
    public static class RumorConfigLoader
    {
        public const string FileName =
            "rumornetwork.json";

        public static RumorNetworkConfig Load(
            ICoreServerAPI api,
            ILogger logger
        )
        {
            try
            {
                RumorNetworkConfig? config =
                    api.LoadModConfig<RumorNetworkConfig>(
                        FileName
                    );

                config ??= new RumorNetworkConfig();
                config.Debug ??= RumorDebugConfig.CreateDefault();
                config.BetterRuins ??=
                    BetterRuinsRumorConfig.CreateDefault();

                config.Normalize();
                config.Debug.Normalize();
                config.BetterRuins.Normalize();

                api.StoreModConfig(
                    config,
                    FileName
                );

                return config;
            }
            catch (Exception exception)
            {
                logger.Error(
                    "Não foi possível carregar " +
                    $"{FileName}: {exception.Message}. " +
                    "Os preços padrão serão usados."
                );

                RumorNetworkConfig fallback = new();
                fallback.Debug ??=
                    RumorDebugConfig.CreateDefault();
                fallback.BetterRuins ??=
                    BetterRuinsRumorConfig.CreateDefault();
                fallback.Normalize();
                fallback.Debug.Normalize();
                fallback.BetterRuins.Normalize();
                return fallback;
            }
        }
    }
}
