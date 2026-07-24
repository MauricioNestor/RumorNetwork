using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace RumorNetwork.Rumors
{
    public static class RumorDebugDeliveryRegistry
    {
        private const int MaximumSnapshots = 128;

        private static readonly object SyncRoot = new();
        private static readonly Dictionary<int, RumorDebugDeliverySnapshot>
            Snapshots = new();
        private static readonly Queue<int> Order = new();

        private static int nextToken = 1;

        public static int Reserve(
            string playerUid,
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            RumorTargetResolution resolution
        )
        {
            lock (SyncRoot)
            {
                int token = nextToken++;

                if (nextToken == int.MaxValue)
                {
                    nextToken = 1;
                }

                Snapshots[token] = new RumorDebugDeliverySnapshot(
                    token,
                    playerUid,
                    CloneRecord(record),
                    knowledge,
                    CloneTargets(resolution.Targets)
                );

                Order.Enqueue(token);
                Trim();
                return token;
            }
        }

        public static void Complete(
            int token,
            IEnumerable<RumorWaypointHandle> handles
        )
        {
            lock (SyncRoot)
            {
                if (Snapshots.TryGetValue(
                        token,
                        out RumorDebugDeliverySnapshot? snapshot
                    ))
                {
                    snapshot.SetWaypoints(
                        handles.Select(handle =>
                            handle.Position.Clone()
                        )
                    );
                }
            }
        }

        public static void Remove(int token)
        {
            lock (SyncRoot)
            {
                Snapshots.Remove(token);
            }
        }

        public static bool TryGet(
            int token,
            string playerUid,
            out RumorDebugDeliverySnapshot? snapshot
        )
        {
            lock (SyncRoot)
            {
                if (
                    Snapshots.TryGetValue(token, out snapshot) &&
                    string.Equals(
                        snapshot.PlayerUid,
                        playerUid,
                        StringComparison.Ordinal
                    )
                )
                {
                    return true;
                }

                snapshot = null;
                return false;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                Snapshots.Clear();
                Order.Clear();
                nextToken = 1;
            }
        }

        private static void Trim()
        {
            while (Order.Count > MaximumSnapshots)
            {
                int oldest = Order.Dequeue();
                Snapshots.Remove(oldest);
            }
        }

        private static RumorRecord CloneRecord(
            RumorRecord source
        )
        {
            return new RumorRecord
            {
                Id = source.Id,
                Kind = source.Kind,
                Family = source.Family,
                SourceCode = source.SourceCode,
                SourceGroup = source.SourceGroup,
                PartCount = source.PartCount,
                Knowledge = source.Knowledge,
                X1 = source.X1,
                Y1 = source.Y1,
                Z1 = source.Z1,
                X2 = source.X2,
                Y2 = source.Y2,
                Z2 = source.Z2
            };
        }

        private static IReadOnlyList<RumorTarget>
            CloneTargets(
                IEnumerable<RumorTarget> targets
            )
        {
            return targets
                .Select(target =>
                    new RumorTarget(
                        target.Position.Clone(),
                        target.Kind
                    )
                )
                .ToList()
                .AsReadOnly();
        }
    }

    public sealed class RumorDebugDeliverySnapshot
    {
        private IReadOnlyList<Vec3d> waypointPositions =
            Array.Empty<Vec3d>();

        public int Token { get; }

        public string PlayerUid { get; }

        public RumorRecord Record { get; }

        public RumorKnowledgeLevel Knowledge { get; }

        public IReadOnlyList<RumorTarget> Targets { get; }

        public IReadOnlyList<Vec3d> WaypointPositions =>
            waypointPositions;

        public RumorDebugDeliverySnapshot(
            int token,
            string playerUid,
            RumorRecord record,
            RumorKnowledgeLevel knowledge,
            IReadOnlyList<RumorTarget> targets
        )
        {
            Token = token;
            PlayerUid = playerUid;
            Record = record;
            Knowledge = knowledge;
            Targets = targets;
        }

        internal void SetWaypoints(
            IEnumerable<Vec3d> positions
        )
        {
            waypointPositions = positions
                .Select(position => position.Clone())
                .ToList()
                .AsReadOnly();
        }
    }
}
