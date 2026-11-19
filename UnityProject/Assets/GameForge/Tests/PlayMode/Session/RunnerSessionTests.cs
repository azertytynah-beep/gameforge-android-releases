using System.Linq;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GameForge.Tests.PlayMode
{
    public sealed class RunnerSessionTests
    {
        private RunnerSettings settings;
        private GameObject host;
        private RunnerSession session;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void RunsAtBoundedSpeedAndFreezesAfterCollision()
        {
            Configure(acceleration: 0.5f, maximum: 7f);
            Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Ready));
            Assert.That(session.Feedback.Distance, Is.Zero);

            session.Advance(1f);
            Assert.That(session.Feedback.Distance, Is.Zero);
            Assert.That(session.StartRun(), Is.True);
            Assert.That(session.StartRun(), Is.False);
            session.Advance(2f);
            Assert.That(session.Feedback.Distance, Is.EqualTo(14f).Within(0.001f));
            session.Advance(1f);
            Assert.That(session.Feedback.Speed, Is.EqualTo(7f));
            Assert.That(session.ReportCollision(), Is.True);

            float stoppedDistance = session.Feedback.Distance;
            session.Advance(60f);
            Assert.That(session.ReportCollision(), Is.False);
            Assert.That(session.StartRun(), Is.False);
            Assert.That(session.Feedback.Distance, Is.EqualTo(stoppedDistance));
            Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Failed));
        }

        [Test]
        public void RepeatedRestartsResetBeforePublishingRunning()
        {
            Configure(acceleration: 0f, maximum: 12f);
            int resets = 0;
            int runningNotifications = 0;
            session.ResetRequested += () =>
            {
                Assert.That(session.Feedback.State, Is.Not.EqualTo(RunnerState.Running));
                Assert.That(session.StartRun(), Is.False);
                Assert.That(session.Restart(), Is.False);
                resets++;
            };
            session.FeedbackChanged += feedback =>
            {
                if (feedback.State == RunnerState.Running && feedback.Distance == 0f)
                    runningNotifications++;
            };

            Assert.That(session.Restart(), Is.False);
            Assert.That(session.StartRun(), Is.True);
            for (int run = 0; run < 2; run++)
            {
                session.Advance(1f);
                session.Advance(float.NaN);
                Assert.That(session.Feedback.Distance, Is.EqualTo(6f));
                Assert.That(session.ReportCollision(), Is.True);
                Assert.That(session.Restart(), Is.True);
                Assert.That(session.Feedback.Distance, Is.Zero);
                Assert.That(session.Feedback.Speed, Is.EqualTo(6f));
            }

            Assert.That(resets, Is.EqualTo(3));
            Assert.That(runningNotifications, Is.EqualTo(3));
        }

        private void Configure(float acceleration, float maximum)
        {
            var config = RunnerConfigV1.CreateDefault();
            config.acceleration = acceleration;
            config.maxSpeed = maximum;
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, new FixtureCatalog(), out settings, out var errors),
                Is.True, string.Join(", ", errors.Select(error => error.Field)));
            host = new GameObject("Session test");
            session = host.AddComponent<RunnerSession>();
            session.Configure(settings);
        }

        private sealed class FixtureCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default";
            public bool HasObstacle(string id) => id == "obstacle-block";
        }
    }
}
