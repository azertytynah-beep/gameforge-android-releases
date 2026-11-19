using System;
using System.Collections.Generic;
using System.Linq;

namespace GameForge.Runtime
{
    public readonly struct ObstaclePlacement
    {
        internal ObstaclePlacement(string id, int lane, float offset)
        {
            Id = id;
            Lane = lane;
            Offset = offset;
        }

        public string Id { get; }
        public int Lane { get; }
        public float Offset { get; }
    }

    public readonly struct TrackSegmentPlan
    {
        internal TrackSegmentPlan(int index, string segmentId, int safeLane, ObstaclePlacement[] obstacles)
        {
            Index = index;
            SegmentId = segmentId;
            SafeLane = safeLane;
            Obstacles = Array.AsReadOnly(obstacles);
        }

        public int Index { get; }
        public string SegmentId { get; }
        public int SafeLane { get; }
        public IReadOnlyList<ObstaclePlacement> Obstacles { get; }
    }

    public sealed class SegmentPlanner
    {
        public const float SegmentLength = 12f;
        public const float MaxObstacleHalfDepth = 0.5f;
        public const float MaxObstacleHalfWidth = 0.65f;
        public const float ReactionSeconds = 0.15f;
        public const float PlayerHalfDepthAllowance = 0.5f;

        private static readonly float[] SlotOffsets = { 4.5f, 6f, 7.5f };

        private readonly string[] segmentIds;
        private readonly string[] obstacleIds;
        private readonly int obstacleCount;
        private uint randomState;
        private int segmentIndex;
        private int safeLane = RunnerTrack.CenterLane;

        public SegmentPlanner(int seed, RunnerSettings settings, IEnumerable<string> segmentIds,
            IEnumerable<string> availableObstacleIds)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (segmentIds == null)
                throw new ArgumentNullException(nameof(segmentIds));
            if (availableObstacleIds == null)
                throw new ArgumentNullException(nameof(availableObstacleIds));

            this.segmentIds = segmentIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (this.segmentIds.Length == 0)
                throw new ArgumentException("Une piste est requise.", nameof(segmentIds));

            obstacleIds = availableObstacleIds.Intersect(settings.ObstacleIds, StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            obstacleCount = obstacleIds.Length > 0 ? settings.ObstacleDensity : 0;

            // Le joueur change de voie entre les deux rangées, à distance des colliders.
            float usableGap = SegmentLength + SlotOffsets[0] - SlotOffsets[SlotOffsets.Length - 1]
                - 2f * (MaxObstacleHalfDepth + PlayerHalfDepthAllowance);
            if (usableGap < settings.MaxSpeed * (RunnerTrack.LaneChangeSeconds + ReactionSeconds))
                throw new ArgumentException("La piste ne laisse pas assez de distance pour changer de voie.", nameof(settings));

            randomState = unchecked((uint)seed);
        }

        public TrackSegmentPlan Next()
        {
            if (segmentIndex > 0)
            {
                int firstLane = Math.Max(0, safeLane - 1);
                int lastLane = Math.Min(RunnerTrack.LaneCount - 1, safeLane + 1);
                safeLane = firstLane + Pick(lastLane - firstLane + 1);
            }

            string segment = segmentIds[Pick(segmentIds.Length)];
            var obstacles = obstacleCount == 0 ? Array.Empty<ObstaclePlacement>() : new ObstaclePlacement[obstacleCount];
            for (int i = 0; i < obstacles.Length; i++)
            {
                int lane = Pick(RunnerTrack.LaneCount - 1);
                if (lane >= safeLane)
                    lane++;
                obstacles[i] = new ObstaclePlacement(obstacleIds[Pick(obstacleIds.Length)], lane, SlotOffsets[i]);
            }

            return new TrackSegmentPlan(segmentIndex++, segment, safeLane, obstacles);
        }

        private int Pick(int count)
        {
            // Cette récurrence entière garde la même suite entre les runtimes Unity.
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return (int)((randomState >> 16) % (uint)count);
        }
    }
}
