using System;
using RumorNetwork.Catalog;
using RumorNetwork.Purchases;
using RumorNetwork.Rumors;
using RumorNetwork.Traders;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RumorNetwork.Dialogue
{
    public static class RumorDialogueRuntime
    {
        private static RumorPurchaseService? rumorPurchaseService;
        private static BetterRuinsPurchaseService? betterRuinsPurchaseService;
        private static TranslocatorPurchaseService? translocatorPurchaseService;
        private static TraderLocationPurchaseService? traderPurchaseService;
        private static RumorRegistry? rumorRegistry;

        public static bool Ready =>
            rumorPurchaseService != null &&
            betterRuinsPurchaseService != null &&
            translocatorPurchaseService != null &&
            traderPurchaseService != null &&
            rumorRegistry != null;

        public static void Configure(
            RumorPurchaseService rumorPurchases,
            BetterRuinsPurchaseService betterRuinsPurchases,
            TranslocatorPurchaseService translocatorPurchases,
            TraderLocationPurchaseService traderPurchases,
            RumorRegistry registry
        )
        {
            rumorPurchaseService = rumorPurchases;
            betterRuinsPurchaseService = betterRuinsPurchases;
            translocatorPurchaseService = translocatorPurchases;
            traderPurchaseService = traderPurchases;
            rumorRegistry = registry;
        }

        public static void Reset()
        {
            rumorPurchaseService = null;
            betterRuinsPurchaseService = null;
            translocatorPurchaseService = null;
            traderPurchaseService = null;
            rumorRegistry = null;
        }

        public static string Execute(
            IServerPlayer player,
            string action,
            Entity? npcEntity = null
        )
        {
            if (!Ready)
            {
                return "unavailable";
            }

            try
            {
                return action switch
                {
                    "checktrader" =>
                        CheckTrader(player, npcEntity),

                    "buytrader" =>
                        BuyTrader(player, npcEntity),

                    "buyapproximate" =>
                        BuyGeneral(
                            player,
                            RumorKnowledgeLevel.Approximate
                        ),

                    "buyexact" =>
                        BuyGeneral(
                            player,
                            RumorKnowledgeLevel.Exact
                        ),

                    "buybetterruins" =>
                        BuyBetterRuins(player),

                    "buytranslocator" =>
                        BuyTranslocator(player),

                    _ =>
                        "unavailable"
                };
            }
            catch (Exception exception)
            {
                player.SendMessage(
                    GlobalConstants.GeneralChatGroup,
                    "Rumor Network: " +
                    exception.GetBaseException().Message,
                    EnumChatType.Notification
                );

                return "unavailable";
            }
        }

        public static void MarkTranslocatorRepaired(
            BlockPos position
        )
        {
            rumorRegistry?.RemoveNear(
                StructureKind.Translocator,
                position.X,
                position.Y,
                position.Z,
                4
            );
        }

        private static string CheckTrader(
            IServerPlayer player,
            Entity? npcEntity
        )
        {
            ScanLoadedTraders(player);
            npcEntity ??= ResolveNearbySellerEntity(player);

            return traderPurchaseService!
                .CheckAvailability(player, npcEntity);
        }

        private static string BuyTrader(
            IServerPlayer player,
            Entity? npcEntity
        )
        {
            ScanLoadedTraders(player);
            npcEntity ??= ResolveNearbySellerEntity(player);

            bool purchased =
                traderPurchaseService!.TryPurchase(
                    player,
                    npcEntity,
                    out TraderLocationPurchaseResult? result,
                    out string error
                );

            if (purchased && result != null)
            {
                return "success";
            }

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                error,
                EnumChatType.Notification
            );

            if (
                error.Contains(
                    "ampliando",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                error.Contains(
                    "aguarde",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                error.Contains(
                    "busca",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return "searching";
            }

            if (error.Contains(
                    "já vendeu",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return "quota";
            }

            if (
                error.Contains(
                    "pagamento",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                error.Contains(
                    "possui",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return "nofunds";
            }

            return "none";
        }

        private static Entity? ResolveNearbySellerEntity(
            IServerPlayer player
        )
        {
            Vec3d position = new(
                player.Entity.Pos.X,
                player.Entity.Pos.Y,
                player.Entity.Pos.Z
            );

            return player.Entity.Api.World.GetNearestEntity(
                position,
                12f,
                8f,
                entity =>
                    entity is EntityTradingHumanoid &&
                    entity.Alive
            );
        }

        private static void ScanLoadedTraders(
            IServerPlayer player
        )
        {
            LiveTraderDiscoveryPatch.ScanLoadedTraders(
                new Vec3d(
                    player.Entity.Pos.X,
                    player.Entity.Pos.Y,
                    player.Entity.Pos.Z
                )
            );
        }

        private static string BuyGeneral(
            IServerPlayer player,
            RumorKnowledgeLevel knowledge
        )
        {
            bool purchased =
                rumorPurchaseService!.TryPurchase(
                    player,
                    knowledge,
                    out RumorPurchaseResult? result,
                    out string error
                );

            if (purchased && result != null)
            {
                return knowledge == RumorKnowledgeLevel.Approximate
                    ? "approximate"
                    : "exact";
            }

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                error,
                EnumChatType.Notification
            );

            return ClassifyGeneralFailure(error);
        }

        private static string BuyBetterRuins(
            IServerPlayer player
        )
        {
            bool purchased =
                betterRuinsPurchaseService!.TryPurchase(
                    player,
                    out RumorPurchaseResult? result,
                    out string error
                );

            if (purchased && result != null)
            {
                return "betterruins";
            }

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                error,
                EnumChatType.Notification
            );

            return ClassifyGeneralFailure(error);
        }

        private static string BuyTranslocator(
            IServerPlayer player
        )
        {
            bool purchased =
                translocatorPurchaseService!.TryPurchase(
                    player,
                    out RumorPurchaseResult? result,
                    out string error
                );

            if (purchased && result != null)
            {
                return "translocator";
            }

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                error,
                EnumChatType.Notification
            );

            return ClassifyGeneralFailure(error);
        }

        private static string ClassifyGeneralFailure(
            string error
        )
        {
            if (
                error.Contains(
                    "Peeks",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                error.Contains(
                    "procurando",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return "searching";
            }

            if (
                error.Contains(
                    "pagamento",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                error.Contains(
                    "possui",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return "nofunds";
            }

            return "none";
        }
    }
}
