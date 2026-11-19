using System;
using System.Linq;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GameForge.Tests.EditMode
{
    public sealed class SegmentPlannerTests
    {
        private static readonly string[] Tracks = { "straight-a", "straight-b" };
        private static readonly string[] Obstacles = { "barrier-a", "barrier-b" };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FiftySeedsKeepAReachableCorridorAndAStableSequence(int density)
        {
            var settings = CreateSettings(density);
            try
            {
                for (int seed = -25; seed < 25; seed++)
                {
                    var planner = new SegmentPlanner(seed, settings, Tracks, Obstacles);
                    var replay = new SegmentPlanner(seed, settings, Tracks.Reverse(), Obstacles.Reverse());
                    TrackSegmentPlan previous = default;

                    for (int index = 0; index < 100; index++)
                    {
                        TrackSegmentPlan segment = planner.Next();
                        TrackSegmentPlan repeated = replay.Next();
                        Assert.That(segment.Index, Is.EqualTo(index));
                        Assert.That(segment.SegmentId, Is.EqualTo(repeated.SegmentId));
                        Assert.That(segment.SafeLane, Is.InRange(0, RunnerTrack.LaneCount - 1));
                        Assert.That(segment.SafeLane, Is.EqualTo(repeated.SafeLane));
                        Assert.That(segment.Obstacles.Count, Is.EqualTo(density));
                        Assert.That(repeated.Obstacles.Count, Is.EqualTo(density));
                        if (index == 0)
                            Assert.That(segment.SafeLane, Is.EqualTo(RunnerTrack.CenterLane));
                        else
                            Assert.That(Math.Abs(previous.SafeLane - segment.SafeLane), Is.LessThanOrEqualTo(1));

                        for (int i = 0; i < density; i++)
                        {
                            ObstaclePlacement prop = segment.Obstacles[i];
                            ObstaclePlacement repeatedProp = repeated.Obstacles[i];
                            Assert.That(prop.Lane, Is.InRange(0, RunnerTrack.LaneCount - 1));
                            Assert.That(prop.Lane, Is.Not.EqualTo(segment.SafeLane));
                            Assert.That(prop.Offset, Is.InRange(SegmentPlanner.MaxObstacleHalfDepth,
                                SegmentPlanner.SegmentLength - SegmentPlanner.MaxObstacleHalfDepth));
                            Assert.That(repeatedProp.Id, Is.EqualTo(prop.Id));
                            Assert.That(repeatedProp.Lane, Is.EqualTo(prop.Lane));
                            Assert.That(repeatedProp.Offset, Is.EqualTo(prop.Offset));
                        }

                        if (index > 0 && density > 0 && previous.SafeLane != segment.SafeLane)
                        {
                            float gap = SegmentPlanner.SegmentLength + segment.Obstacles[0].Offset
                                - previous.Obstacles[density - 1].Offset
                                - 2f * (SegmentPlanner.MaxObstacleHalfDepth + SegmentPlanner.PlayerHalfDepthAllowance);
                            float required = settings.MaxSpeed * (RunnerTrack.LaneChangeSeconds + SegmentPlanner.ReactionSeconds);
                            Assert.That(gap, Is.GreaterThanOrEqualTo(required));
                        }

                        previous = segment;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SingleTrackAndNoMatchingObstacleStillProduceAPath()
        {
            var settings = CreateSettings(3);
            try
            {
                var planner = new SegmentPlanner(int.MinValue, settings, new[] { "straight-a" }, new[] { "unselected" });
                var replay = new SegmentPlanner(int.MinValue, settings, new[] { "straight-a" }, Array.Empty<string>());
                for (int i = 0; i < 30; i++)
                {
                    TrackSegmentPlan first = planner.Next();
                    Assert.That(first.SegmentId, Is.EqualTo("straight-a"));
                    Assert.That(first.Obstacles, Is.Empty);
                    Assert.That(first.SafeLane, Is.EqualTo(replay.Next().SafeLane));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static RunnerSettings CreateSettings(int density)
        {
            var config = RunnerConfigV1.CreateDefault();
            config.maxSpeed = 18f;
            config.obstacleDensity = density;
            config.obstacleIds = Obstacles;
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, new TestCatalog(), out var settings, out _), Is.True);
            return settings;
        }

        private sealed class TestCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default";
            public bool HasObstacle(string id) => Obstacles.Contains(id);
        }
    }
}
