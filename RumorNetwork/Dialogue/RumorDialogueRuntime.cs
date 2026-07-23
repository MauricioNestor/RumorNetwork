using System;
using RumorNetwork.Offers;
using RumorNetwork.Purchases;
using RumorNetwork.Rumors;
using RumorNetwork.Traders;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RumorNetwork.Dialogue
{
    public static class RumorDialogueRuntime
    {
        private static RumorPurchaseService? rumorPurchaseService;
        private static TraderLocationPurchaseService? traderPurchaseService;
        private static RumorRegistry? rumorRegistry;

        public static bool Ready =>
            rumorPurchaseService != null &&
            traderPurchaseService != null &&
            rumorRegistry != null;

        public static void Configure(
            RumorPurchaseService rumorPurchases,
            TraderLocationPurchaseService traderPurchases,
            RumorRegistry registry
        )
        {
            rumorPurchaseService = rumorPurchases;
            traderPurchaseService = traderPurchases;
            rumorRegistry = registry;
        }

        public static void Reset()
        {
            rumorPurchaseService = null;
            traderPurchaseService = null;
            rumorRegistry = null;
        }

        public static string Execute(
            IServerPlayer player,
            string action
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
                    "buytrader" => BuyTrader(player),
                    "buyapproximate" => BuyGeneral(
                        player,
                        RumorKnowledgeLevel.Approximate
                    ),
                    "buyexact" => BuyGeneral(
                        player,
                        RumorKnowledgeLevel.Exact
                    ),
                    "buytranslocator" => BuyTranslocator(player),
                    _ => "unavailable"
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

        private static string BuyTrader(
            IServerPlayer player
        )
        {
            bool purchased =
                traderPurchaseService!.TryPurchase(
                    player,
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

            if (error.Contains("ampliando", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("aguarde", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("busca", StringComparison.OrdinalIgnoreCase))
            {
                return "searching";
            }

            if (error.Contains("já vendeu", StringComparison.OrdinalIgnoreCase))
            {
                return "quota";
            }

            if (error.Contains("pagamento", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("possui", StringComparison.OrdinalIgnoreCase))
            {
                return "nofunds";
            }

            return "none";
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

        private static string BuyTranslocator(
            IServerPlayer player
        )
        {
            bool purchased =
                rumorPurchaseService!.TryPurchaseTranslocator(
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
            if (error.Contains("descoberta remota", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("Peeks", StringComparison.OrdinalIgnoreCase))
            {
                return "searching";
            }

            if (error.Contains("pagamento", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("possui", StringComparison.OrdinalIgnoreCase))
            {
                return "nofunds";
            }

            return "none";
        }
    }
}
