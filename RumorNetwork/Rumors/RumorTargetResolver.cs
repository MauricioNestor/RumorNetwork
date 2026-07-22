using RumorNetwork.Caves;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors
{
    public sealed class RumorTargetResolver
    {
        private readonly CaveBoundaryScanner boundaryScanner;
        private readonly CaveSkyConnectionSearch skyConnectionSearch;

        public RumorTargetResolver(
            CaveBoundaryScanner boundaryScanner,
            CaveSkyConnectionSearch skyConnectionSearch
        )
        {
            this.boundaryScanner = boundaryScanner;
            this.skyConnectionSearch = skyConnectionSearch;
        }

        public bool TryResolve(
            RumorRecord record,
            out RumorTarget? target,
            out string error
        )
        {
            bool resolved = TryResolveAll(
                record,
                out RumorTargetResolution? resolution,
                out _,
                out error
            );

            target = resolution?.PrimaryTarget;
            return resolved;
        }

        public bool TryResolveAll(
            RumorRecord record,
            out RumorTargetResolution? resolution,
            out RumorResolutionFailureKind failure,
            out string error
        )
        {
            if (record.Kind == StructureKind.UndergroundRuin)
            {
                return TryResolveUndergroundRuin(
                    record,
                    out resolution,
                    out failure,
                    out error
                );
            }

            resolution = new RumorTargetResolution(
                CreateCenterTarget(record)
            );

            failure = RumorResolutionFailureKind.None;
            error = string.Empty;
            return true;
        }

        private bool TryResolveUndergroundRuin(
            RumorRecord record,
            out RumorTargetResolution? resolution,
            out RumorResolutionFailureKind failure,
            out string error
        )
        {
            Cuboidi location = record.CreateLocation();
            CaveBoundaryScanResult boundaryResult =
                boundaryScanner.Scan(location);

            CaveSkyConnectionResult skyResult =
                skyConnectionSearch.Search(
                    boundaryResult.ScannedBox,
                    boundaryResult.Openings
                );

            if (
                !skyResult.IsConnected ||
                skyResult.SkyPosition == null ||
                skyResult.SourceOpening == null
            )
            {
                resolution = null;
                failure = MapSkyFailure(
                    skyResult.Status
                );
                error = CreateSkyResolutionError(
                    failure
                );
                return false;
            }

            RumorTarget caveEntrance = new(
                ToCenteredPosition(
                    skyResult.SkyPosition
                ),
                RumorTargetKind.CaveEntrance
            );

            RumorTarget structureEntrance = new(
                ToCenteredPosition(
                    skyResult.SourceOpening.InsidePosition
                ),
                RumorTargetKind.StructureEntrance
            );

            resolution = new RumorTargetResolution(
                caveEntrance,
                structureEntrance
            );

            failure = RumorResolutionFailureKind.None;
            error = string.Empty;
            return true;
        }

        private static RumorTarget CreateCenterTarget(
            RumorRecord record
        )
        {
            Vec3i center = record.CreateLocation().Center;

            return new RumorTarget(
                new Vec3d(
                    center.X + 0.5,
                    center.Y + 0.5,
                    center.Z + 0.5
                ),
                RumorTargetKind.StructureCenter
            );
        }

        private static Vec3d ToCenteredPosition(
            BlockPos position
        )
        {
            return new Vec3d(
                position.X + 0.5,
                position.Y + 0.5,
                position.Z + 0.5
            );
        }

        private static RumorResolutionFailureKind
            MapSkyFailure(
                CaveSkyConnectionStatus status
            )
        {
            return status switch
            {
                CaveSkyConnectionStatus.NoOpenings =>
                    RumorResolutionFailureKind.NoOpenings,

                CaveSkyConnectionStatus.Enclosed =>
                    RumorResolutionFailureKind.Enclosed,

                CaveSkyConnectionStatus.ChunksUnavailable =>
                    RumorResolutionFailureKind.ChunksUnavailable,

                CaveSkyConnectionStatus.SearchLimitReached =>
                    RumorResolutionFailureKind.SearchLimitReached,

                _ =>
                    RumorResolutionFailureKind.Unknown
            };
        }

        private static string CreateSkyResolutionError(
            RumorResolutionFailureKind failure
        )
        {
            return failure switch
            {
                RumorResolutionFailureKind.NoOpenings =>
                    "A ruína subterrânea não possui uma abertura detectável.",

                RumorResolutionFailureKind.Enclosed =>
                    "A ruína subterrânea não está conectada ao céu.",

                RumorResolutionFailureKind.SearchLimitReached =>
                    "A busca pela entrada da caverna atingiu o limite de segurança.",

                RumorResolutionFailureKind.ChunksUnavailable =>
                    "A entrada da caverna atravessa chunks que não estão carregados.",

                _ =>
                    "Não foi possível localizar uma entrada de caverna para a ruína."
            };
        }
    }
}
