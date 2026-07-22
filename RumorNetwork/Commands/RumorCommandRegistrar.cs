using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public static class RumorCommandRegistrar
    {
        public static void Register(
            ICoreServerAPI api,
            ILogger logger,
            RumorRegistry rumorRegistry,
            RumorTargetResolver rumorTargetResolver,
            int regionSearchRadius
        )
        {
            IChatCommand rumorCommand = api.ChatCommands
                .Create("rumor")
                .WithDescription("Comandos do Rumor Network.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(_ =>
                    TextCommandResult.Success(
                        "Rumor Network está funcionando."
                    )
                );

            new StructureDebugCommands(
                api,
                logger,
                regionSearchRadius
            ).Register(rumorCommand);

            new RumorRegistryCommands(
                api,
                logger,
                rumorRegistry,
                regionSearchRadius
            ).Register(rumorCommand);

            new RumorDeliveryCommands(
                api,
                logger,
                rumorRegistry,
                rumorTargetResolver
            ).Register(rumorCommand);
        }
    }
}
