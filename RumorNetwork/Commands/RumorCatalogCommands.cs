using RumorNetwork.Catalog;
using RumorNetwork.Rumors;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class RumorCatalogCommands
    {
        private readonly RumorRegistry rumorRegistry;
        private readonly SelectiveStructureCatalogService
            catalogService;

        public RumorCatalogCommands(
            RumorRegistry rumorRegistry,
            SelectiveStructureCatalogService catalogService
        )
        {
            this.rumorRegistry = rumorRegistry;
            this.catalogService = catalogService;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("catalog")
                .WithDescription(
                    "Mostra o catálogo remoto de traders e " +
                    "translocadores."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ShowCatalog)
                .EndSubCommand();

            rumorCommand
                .BeginSubCommand("catalogbackfill")
                .WithDescription(
                    "Procura traders e translocadores em " +
                    "map regions já existentes no save."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(RequestBackfill)
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
                "Catálogo remoto: " +
                $"Traders={traders} | " +
                $"Translocators={translocators} | " +
                $"Regiões verificadas=" +
                $"{catalogService.ScannedRegionCount} | " +
                $"Pendentes={catalogService.PendingRegionCount}."
            );
        }

        private TextCommandResult RequestBackfill(
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

            int queued =
                catalogService.RequestBackfillAround(
                    (int)player.Entity.Pos.X,
                    (int)player.Entity.Pos.Z
                );

            return TextCommandResult.Success(
                queued > 0
                    ? $"{queued} map regions adicionadas à " +
                        "fila de backfill. A busca lê somente " +
                        "regiões já existentes no save."
                    : catalogService.IsWorking
                        ? "O backfill já está em andamento."
                        : "Não há novas regiões para verificar " +
                            "neste raio."
            );
        }
    }
}
