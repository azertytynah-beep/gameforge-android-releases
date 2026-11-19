using System.Collections;
using System.Linq;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameForge.Tests.PlayMode
{
    public sealed class SegmentStreamTests
    {
        private const int WindowSize = 5;

        [UnityTest]
        public IEnumerator StreamRecyclesOneThousandSegmentsAndRestoresTheSameStart()
        {
            var root = new GameObject("TrackStream");
            root.transform.position = new Vector3(2f, 0f, 30f);
            var firstTrack = CreateTrack("Track-A");
            var secondTrack = CreateTrack("Track-B");
            var obstacle = new GameObject("Obstacle-A", typeof(BoxCollider));
            obstacle.SetActive(false);
            var config = RunnerConfigV1.CreateDefault();
            config.maxSpeed = 18f;
            config.obstacleDensity = 3;
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, new TestCatalog(), out var settings, out _), Is.True);

            try
            {
                var stream = root.AddComponent<SegmentStream>();
                var tracks = new[]
                {
                    new TrackSegmentPrefab("straight-a", firstTrack), new TrackSegmentPrefab("straight-b", secondTrack)
                };
                var obstacles = new[] { new TrackObstaclePrefab("obstacle-block", obstacle) };
                stream.Configure(tracks, obstacles, WindowSize);
                stream.Reset(-37, settings);
                float origin = root.transform.position.z;
                var firstWindow = Snapshot(root.transform);

                for (int i = 0; i < 1000; i++)
                {
                    float runnerZ = origin + (i + 2) * SegmentPlanner.SegmentLength + 0.5f;
                    stream.AdvanceTo(runnerZ);
                    Assert.That(stream.FirstStartZ, Is.LessThanOrEqualTo(runnerZ));
                    Assert.That(stream.EndZ, Is.GreaterThan(runnerZ));
                    Assert.That(stream.ActiveSegmentCount, Is.EqualTo(WindowSize));
                    Assert.That(stream.PooledSegmentCount, Is.LessThanOrEqualTo(WindowSize));
                    Assert.That(stream.PooledObstacleCount, Is.LessThanOrEqualTo(WindowSize * 3));

                    if (i % 40 == 0 || i == 999)
                    {
                        yield return null;
                        Transform[] active = Tracks(root.transform);
                        Assert.That(active.Length, Is.EqualTo(WindowSize));
                        Assert.That(active.Sum(track => track.Cast<Transform>().Count(IsObstacle)), Is.EqualTo(WindowSize * 3));
                        Assert.That(root.transform.childCount, Is.LessThanOrEqualTo(WindowSize * 5));
                        for (int slot = 1; slot < active.Length; slot++)
                            Assert.That(active[slot].Find("Entry").position, Is.EqualTo(active[slot - 1].Find("Exit").position));
                    }
                }

                stream.Reset(-37, settings);
                Assert.That(stream.FirstStartZ, Is.EqualTo(origin));
                Assert.That(Snapshot(root.transform), Is.EqualTo(firstWindow));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(firstTrack);
                Object.Destroy(secondTrack);
                Object.Destroy(obstacle);
                Object.Destroy(settings);
            }
        }

        private static Transform[] Tracks(Transform root)
        {
            return root.Cast<Transform>().Where(child => child.gameObject.activeSelf && child.Find("Entry") != null)
                .OrderBy(child => child.position.z).ToArray();
        }

        private static bool IsObstacle(Transform candidate) => candidate.name.StartsWith("Obstacle-");

        private static string[] Snapshot(Transform root)
        {
            return Tracks(root).SelectMany(track => new[] { track.name }.Concat(track.Cast<Transform>()
                .Where(IsObstacle).OrderBy(prop => prop.localPosition.z)
                .Select(prop => $"{prop.name}:{prop.localPosition}"))).ToArray();
        }

        private static GameObject CreateTrack(string name)
        {
            var track = new GameObject(name);
            var entry = new GameObject("Entry").transform;
            entry.SetParent(track.transform, false);
            var exit = new GameObject("Exit").transform;
            exit.SetParent(track.transform, false);
            exit.localPosition = Vector3.forward * SegmentPlanner.SegmentLength;
            track.SetActive(false);
            return track;
        }

        private sealed class TestCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default";
            public bool HasObstacle(string id) => id == "obstacle-block";
        }
    }
}
