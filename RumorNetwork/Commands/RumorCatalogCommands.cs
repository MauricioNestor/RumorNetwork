using RumorNetwork.Catalog;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class RumorCatalogCommands
    {
        private readonly RumorRegistry rumorRegistry;
        private readonly VerifiedStructureDiscoveryService
            discoveryService;

        public RumorCatalogCommands(
            RumorRegistry rumorRegistry,
            VerifiedStructureDiscoveryService discoveryService
        )
        {
            this.rumorRegistry = rumorRegistry;
            this.discoveryService = discoveryService;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("catalog")
                .WithDescription(
                    "Mostra o catálogo verificado de traders e " +
                    "translocadores."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ShowCatalog)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("catalogbackfill")
                .WithDescription(
                    "Inicia descoberta temporária e limitada de " +
                    "traders e translocadores sem salvar chunks."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(RequestDiscovery)
                .EndSubCommand();
        }

        private TextCommandResult ShowCatalog(
            TextCommandCallingArgs args
        )
        {
            int traders =
                rumorRegistry.CountByKind(
                    StructureKind.Trader
                );

            int translocators =
                rumorRegistry.CountByKind(
                    StructureKind.Translocator
                );

            return TextCommandResult.Success(
                "Catálogo verificado: " +
                $"Traders={traders} | " +
                $"Translocators={translocators} | " +
                $"Chunks inspecionados=" +
                $"{discoveryService.InspectedChunkCount} | " +
                $"Buscas={discoveryService.ActiveSearchCount} | " +
                $"Peeks ativos={discoveryService.ActivePeekCount} | " +
                $"Orçamento pendente=" +
                $"{discoveryService.PendingPeekBudget}."
            );
        }

        private TextCommandResult RequestDiscovery(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return TextCommandResult.Error(
                    "O comando precisa ser executado " +
                    "por um jogador."
                );
            }

            int blockX = (int)player.Entity.Pos.X;
            int blockZ = (int)player.Entity.Pos.Z;

            bool traderStarted =
                discoveryService.RequestAdditional(
                    StructureKind.Trader,
                    blockX,
                    blockZ
                );

            bool translocatorStarted =
                discoveryService.RequestAdditional(
                    StructureKind.Translocator,
                    blockX,
                    blockZ
                );

            if (traderStarted || translocatorStarted)
            {
                return TextCommandResult.Success(
                    "Descoberta verificada iniciada. O worldgen " +
                    "será simulado em memória dentro dos limites " +
                    "configurados; nenhum chunk será salvo."
                );
            }

            return TextCommandResult.Success(
                discoveryService.IsWorking
                    ? "A descoberta verificada já está em andamento."
                    : "As buscas deste ponto já esgotaram o raio " +
                        "e orçamento atuais sem novos alvos."
            );
        }
    }
}
