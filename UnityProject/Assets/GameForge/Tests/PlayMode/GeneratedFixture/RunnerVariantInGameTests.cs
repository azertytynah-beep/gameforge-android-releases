#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameForge.Tests.PlayMode
{
    [PrebuildSetup("GameForge.Tests.EditMode.Generation.GeneratedRunnerFixture")]
    [PostBuildCleanup("GameForge.Tests.EditMode.Generation.GeneratedRunnerFixture")]
    public sealed class RunnerVariantInGameTests
    {
        [UnityTest]
        public IEnumerator PublishedNightRunnerStartsScoresAndRestartsAfterCollision() =>
            PlayVariant("GameForge.Tests.GeneratedRunnerFixture");

        [UnityTest]
        public IEnumerator RestoredNightRunnerStartsScoresAndRestartsAfterCollision() =>
            PlayVariant("GameForge.Tests.RestoredRunnerFixture");

        private static IEnumerator PlayVariant(string key)
        {
            var id = SessionState.GetString(key, "");
            Assert.That(id, Is.Not.Empty);
            var path = "Assets/GameForgeGenerated/" + id + "/Runner.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var root = SceneManager.GetActiveScene().GetRootGameObjects().Single();
            var session = root.GetComponentInChildren<RunnerSession>();
            var motor = root.GetComponentInChildren<RunnerMotor>();
            var stream = root.GetComponentInChildren<SegmentStream>();
            var hud = root.GetComponentInChildren<RunnerHud>().transform.Find("SafeArea");
            var start = motor.transform.position;
            Assert.That(stream.transform.Find("TrackNight(Clone)"), Is.Not.Null);
            Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Ready));
            Assert.That(session.Feedback.Speed, Is.EqualTo(7f));

            var previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 0.05f;
            try
            {
                hud.Find("Ready/Start").GetComponent<Button>().onClick.Invoke();
                var obstacleLayer = LayerMask.NameToLayer("RunnerObstacle");
                var obstacle = stream.GetComponentsInChildren<Collider>()
                    .Where(item => item.gameObject.layer == obstacleLayer)
                    .OrderBy(item => item.transform.position.z).First();
                Assert.That(obstacle.name, Does.StartWith("Barrier"));
                motor.RequestLane(obstacle.transform.position.x < 0 ? LaneIntent.Left : LaneIntent.Right);
                for (int frame = 0; frame < 100 && session.Feedback.State == RunnerState.Running; frame++)
                    yield return null;

                Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Failed));
                Assert.That(session.Feedback.Distance, Is.GreaterThan(1f));
                Assert.That(hud.Find("Failed").gameObject.activeInHierarchy, Is.True);

                hud.Find("Failed/Restart").GetComponent<Button>().onClick.Invoke();
                Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Running));
                Assert.That(session.Feedback.Distance, Is.Zero);
                Assert.That(motor.transform.position, Is.EqualTo(start));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDelta;
            }
        }

        [UnityTearDown]
        public IEnumerator CloseFixture()
        {
            yield return SceneManager.LoadSceneAsync("Assets/GameForge/Setup/Smoke.unity");
        }
    }
}
#endif
