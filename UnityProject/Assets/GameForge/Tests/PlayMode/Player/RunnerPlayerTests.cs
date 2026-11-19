using System.Collections;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameForge.Tests.PlayMode.Player
{
    public sealed class RunnerPlayerTests
    {
        private static readonly Vector3 Origin = new Vector3(10f, 1f, 40f);
        private const int ObstacleLayer = 8;
        private const int OtherLayer = 9;

        private GameObject fixture;
        private RunnerMotor motor;
        private RunnerSettings settings;

        [SetUp]
        public void SetUp()
        {
            fixture = new GameObject("Runner player fixture");
            motor = AddFixtureObject("Player").AddComponent<RunnerMotor>();
            motor.ObstacleLayers = 1 << ObstacleLayer;

            var config = RunnerConfigV1.CreateDefault();
            config.maxSpeed = 18f;
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, new TestCatalog(), out settings, out _), Is.True);
            motor.Configure(settings);
            motor.ResetPosition(Origin);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fixture);
            Object.DestroyImmediate(settings);
        }

        [TestCase(15)]
        [TestCase(60)]
        [TestCase(144)]
        public void AdvancesAndStaysOnTheThreeLanes(int stepsPerSecond)
        {
            motor.RequestLane(LaneIntent.Left);
            motor.Step(settings.InitialSpeed, 1f);
            Assert.That(motor.transform.position, Is.EqualTo(Origin));

            motor.SetRunning(true);
            AdvanceOneSecond(stepsPerSecond);
            Assert.That(motor.transform.position.z, Is.EqualTo(Origin.z + settings.InitialSpeed).Within(0.005f));
            AssertLane(0);

            motor.RequestLane(LaneIntent.Left);
            motor.RequestLane(LaneIntent.Left);
            AdvanceOneSecond(stepsPerSecond);
            AssertLane(-1);

            motor.RequestLane(LaneIntent.Right);
            AdvanceOneSecond(stepsPerSecond);
            AssertLane(0);

            motor.RequestLane(LaneIntent.Right);
            motor.RequestLane(LaneIntent.Right);
            AdvanceOneSecond(stepsPerSecond);
            AssertLane(1);
        }

        [Test]
        public void CapsTheForwardStepAtTheConfiguredMaximum()
        {
            motor.SetRunning(true);
            motor.Step(100f, 0.5f);
            Assert.That(motor.transform.position.z, Is.EqualTo(Origin.z + settings.MaxSpeed * 0.5f).Within(0.005f));
        }

        [TestCase(ObstacleLayer, 1)]
        [TestCase(OtherLayer, 0)]
        public void DetectsThinCollidersAtTopSpeedAndOnlySignalsTheObstacleLayer(int layer, int expectedSignals)
        {
            var obstacle = AddFixtureObject("Thin obstacle");
            obstacle.layer = layer;
            obstacle.transform.position = Origin + Vector3.forward * 3f;
            obstacle.AddComponent<BoxCollider>().size = new Vector3(1.5f, 2f, 0.04f);
            Physics.SyncTransforms();

            var signals = 0;
            motor.FatalCollision += () => signals++;
            motor.SetRunning(true);
            motor.Step(settings.MaxSpeed, 0.25f);

            Assert.That(motor.transform.position.z, Is.LessThan(obstacle.transform.position.z));
            Assert.That(signals, Is.EqualTo(expectedSignals));

            motor.SetRunning(true);
            motor.Step(settings.MaxSpeed, 0.25f);
            Assert.That(signals, Is.EqualTo(expectedSignals));

            motor.ResetPosition(Origin);
            motor.SetRunning(true);
            motor.Step(settings.MaxSpeed, 0.25f);
            Assert.That(signals, Is.EqualTo(expectedSignals * 2));
        }

        [UnityTest]
        public IEnumerator FollowCameraKeepsThePlayerInFrameDuringALaneChange()
        {
            var camera = AddFixtureObject("Camera").AddComponent<Camera>();
            camera.aspect = 9f / 16f;
            camera.gameObject.AddComponent<RunnerCameraFollow>().Bind(motor.transform);
            motor.SetRunning(true);
            motor.RequestLane(LaneIntent.Right);

            for (var step = 0; step < 5; step++)
            {
                motor.Step(settings.InitialSpeed, 0.05f);
                yield return null;

                var viewport = camera.WorldToViewportPoint(motor.transform.position);
                Assert.That(viewport.x, Is.InRange(0.1f, 0.9f));
                Assert.That(viewport.y, Is.InRange(0.1f, 0.9f));
                Assert.That(viewport.z, Is.Positive);
                Assert.That(camera.transform.position.x, Is.EqualTo(motor.transform.position.x).Within(0.001f));
            }

            AssertLane(1);
        }

        private void AdvanceOneSecond(int stepsPerSecond)
        {
            for (var step = 0; step < stepsPerSecond; step++)
                motor.Step(settings.InitialSpeed, 1f / stepsPerSecond);
        }

        private void AssertLane(int offset) => Assert.That(motor.transform.position.x,
            Is.EqualTo(Origin.x + offset * RunnerTrack.LaneSpacing).Within(0.005f));

        private GameObject AddFixtureObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(fixture.transform);
            return gameObject;
        }

        private sealed class TestCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default";
            public bool HasObstacle(string id) => id == "obstacle-block";
        }
    }
}
