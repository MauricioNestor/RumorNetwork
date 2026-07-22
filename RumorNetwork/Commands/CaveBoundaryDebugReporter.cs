using RumorNetwork.Caves;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Commands
{
    internal static class CaveBoundaryDebugReporter
    {
        private const int MaximumLoggedOpenings = 20;

        public static void Log(
            ILogger logger,
            CaveBoundaryScanResult result
        )
        {
            logger.Notification(
                "=== Rumor Network: cave boundary scan ==="
            );

            Cuboidi box = result.ScannedBox;

            logger.Notification(
                $"ScannedBox=({box.X1},{box.Y1},{box.Z1})-" +
                $"({box.X2},{box.Y2},{box.Z2})"
            );

            logger.Notification(
                $"Status={result.Status} | " +
                $"Pairs={result.ScannedPairCount} | " +
                $"Openings={result.Openings.Count} | " +
                $"Unknown={result.UnknownPairCount} | " +
                $"Unavailable={result.UnavailablePairCount}"
            );

            int loggedCount = 0;

            foreach (
                CaveBoundaryOpening opening
                in result.Openings
            )
            {
                if (loggedCount >= MaximumLoggedOpenings)
                {
                    break;
                }

                logger.Notification(
                    $"[{loggedCount}] Face={opening.Face} | " +
                    $"Inside={FormatPosition(opening.InsidePosition)} " +
                    $"{FormatCell(opening.InsideCell)} | " +
                    $"Outside={FormatPosition(opening.OutsidePosition)} " +
                    $"{FormatCell(opening.OutsideCell)}"
                );

                loggedCount++;
            }

            int omittedCount =
                result.Openings.Count - loggedCount;

            if (omittedCount > 0)
            {
                logger.Notification(
                    $"... {omittedCount} aberturas adicionais omitidas."
                );
            }
        }

        private static string FormatPosition(
            BlockPos position
        )
        {
            return $"({position.X},{position.Y},{position.Z})";
        }

        private static string FormatCell(
            CaveCellInfo cell
        )
        {
            return $"[{cell.Traversal}/{cell.Medium}]";
        }
    }
}
