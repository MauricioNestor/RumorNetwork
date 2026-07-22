using RumorNetwork.Caves;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RumorNetwork.Commands
{
    public sealed class CaveDebugCommands
    {
        private readonly ILogger logger;
        private readonly IBlockAccessor blockAccessor;
        private readonly CaveCellClassifier caveCellClassifier;

        public CaveDebugCommands(
            ILogger logger,
            IBlockAccessor blockAccessor,
            CaveCellClassifier caveCellClassifier
        )
        {
            this.logger = logger;
            this.blockAccessor = blockAccessor;
            this.caveCellClassifier = caveCellClassifier;
        }

        public void Register(
            IChatCommand rumorCommand
        )
        {
            rumorCommand
                .BeginSubCommand("probe")
                .WithDescription(
                    "Classifica a célula apontada para diagnóstico de cavernas."
                )
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ProbeCell)
                .EndSubCommand();
        }

        private TextCommandResult ProbeCell(
            TextCommandCallingArgs args
        )
        {
            IServerPlayer? player =
                args.Caller.Player as IServerPlayer;

            if (player == null)
            {
                return TextCommandResult.Error(
                    "O comando precisa ser executado por um jogador."
                );
            }

            BlockSelection? selection =
                player.CurrentBlockSelection;

            BlockPos position;
            string source;

            if (selection?.Position != null)
            {
                position = selection.Position;
                source = "AimedBlock";
            }
            else
            {
                position = args.Caller.Entity.Pos.AsBlockPos;
                source = "PlayerCell";
            }

            Block solidBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.Solid
                );

            Block mostSolidBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.MostSolid
                );

            Block fluidBlock =
                blockAccessor.GetBlock(
                    position,
                    BlockLayersAccess.Fluid
                );

            Cuboidf[]? collisionBoxes =
                mostSolidBlock.GetCollisionBoxes(
                    blockAccessor,
                    position
                );

            CaveCellInfo cellInfo =
                caveCellClassifier.Classify(position);

            logger.Notification(
                "=== Rumor Network: cave cell probe ==="
            );

            logger.Notification(
                $"Source={source} | " +
                $"Position=({position.X},{position.Y},{position.Z})"
            );

            logger.Notification(
                $"Solid={FormatBlock(solidBlock)} | " +
                $"MostSolid={FormatBlock(mostSolidBlock)} | " +
                $"Fluid={FormatBlock(fluidBlock)}"
            );

            logger.Notification(
                $"CollisionBoxes={collisionBoxes?.Length ?? 0} | " +
                $"Traversal={cellInfo.Traversal} | " +
                $"Medium={cellInfo.Medium} | " +
                $"Traversable={cellInfo.IsTraversable}"
            );

            return TextCommandResult.Success(
                $"Célula {position.X}, {position.Y}, {position.Z}: " +
                $"{cellInfo.Traversal}, {cellInfo.Medium}. " +
                "Veja o console."
            );
        }

        private static string FormatBlock(
            Block block
        )
        {
            return block.Code?.ToString()
                ?? $"(sem código, id={block.Id})";
        }
    }
}
