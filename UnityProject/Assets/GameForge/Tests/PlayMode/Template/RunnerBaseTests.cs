#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameForge.Tests.PlayMode
{
    public sealed class RunnerBaseTests
    {
        private const string ScenePath = "Assets/GameForge/Content/Templates/Runner/RunnerBase.unity";
        private float previousCaptureDelta;
        private int previousVSync;
        private RunnerSettings alternateSettings;

        [SetUp]
        public void SetUp()
        {
            previousCaptureDelta = Time.captureDeltaTime;
            previousVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Time.captureDeltaTime = 0.05f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = previousCaptureDelta;
            QualitySettings.vSyncCount = previousVSync;
            yield return SceneManager.LoadSceneAsync("Assets/GameForge/Setup/Smoke.unity");
            if (alternateSettings != null)
                Object.Destroy(alternateSettings);
        }

        [UnityTest]
        public IEnumerator SourceSceneRunsScoresAndRestartsAfterThreeCollisions()
        {
            yield return LoadSource();
            var root = SceneManager.GetActiveScene().GetRootGameObjects().Single();
            var session = root.GetComponent<RunnerSession>();
            var stream = root.GetComponentInChildren<SegmentStream>();
            var motor = root.GetComponentInChildren<RunnerMotor>();
            var hud = root.GetComponentInChildren<RunnerHud>();
            var origin = motor.transform.position;
            var obstacleLayer = LayerMask.NameToLayer("RunnerObstacle");
            Assert.That(root.GetComponentsInChildren<Component>(true), Has.None.Null);
            Assert.That(EventSystem.current.currentInputModule, Is.TypeOf<InputSystemUIInputModule>());
            Assert.That(stream.transform.Find("TrackStraight(Clone)"), Is.Not.Null);
            Assert.That(Panel(hud, "Ready").activeInHierarchy, Is.True);
            Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Ready));

            for (int run = 0; run < 4; run++)
            {
                Click(hud, run == 0 ? "Ready/Start" : "Failed/Restart");
                Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Running));
                Assert.That(session.Feedback.Distance, Is.Zero);
                Assert.That(motor.transform.position, Is.EqualTo(origin));
                Assert.That(stream.FirstStartZ, Is.Zero);
                Assert.That(Panel(hud, "Running/Score").GetComponent<Text>().text, Is.EqualTo("0 m"));

                var obstacle = stream.GetComponentsInChildren<Collider>().Where(item => item.gameObject.layer == obstacleLayer)
                    .OrderBy(item => item.transform.position.z).First();
                Assert.That(obstacle.name, Does.StartWith("Block"));
                Click(hud, obstacle.transform.position.x < 0f ? "Running/Left" : "Running/Right");

                for (int frame = 0; frame < 100 && session.Feedback.State == RunnerState.Running; frame++)
                    yield return null;

                Assert.That(motor.transform.position.z, Is.GreaterThan(origin.z));
                Assert.That(Mathf.Abs(motor.transform.position.x), Is.EqualTo(RunnerTrack.LaneSpacing).Within(0.01f));
                Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Failed));
                Assert.That(session.Feedback.Distance, Is.GreaterThan(1f));
                Assert.That(Panel(hud, "Failed/Score").GetComponent<Text>().text,
                    Is.EqualTo($"{Mathf.FloorToInt(session.Feedback.Distance)} m"));
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NightTrackAndBarriersRecycleDuringFiveSimulatedMinutes()
        {
            RunnerCatalog catalog = null;
            var config = RunnerConfigV1.CreateDefault("theme-night", "obstacle-barrier");
            config.obstacleDensity = 3;
            void SetNightSettings(Scene scene, LoadSceneMode mode)
            {
                var bootstrap = new SerializedObject(scene.GetRootGameObjects().Single().GetComponent<RunnerBootstrap>());
                catalog = (RunnerCatalog)bootstrap.FindProperty("catalog").objectReferenceValue;
                Assert.That(RunnerConfigValidator.TryCreateSettings(config, catalog, out alternateSettings, out _), Is.True);
                bootstrap.FindProperty("settings").objectReferenceValue = alternateSettings;
                bootstrap.ApplyModifiedPropertiesWithoutUndo();
            }

            SceneManager.sceneLoaded += SetNightSettings;
            try
            {
                yield return LoadSource();
            }
            finally
            {
                SceneManager.sceneLoaded -= SetNightSettings;
            }

            var root = SceneManager.GetActiveScene().GetRootGameObjects().Single();
            var motor = root.GetComponentInChildren<RunnerMotor>();
            var stream = root.GetComponentInChildren<SegmentStream>();
            var session = root.GetComponent<RunnerSession>();
            var hud = root.GetComponentInChildren<RunnerHud>();
            Assert.That(stream.transform.Find("TrackNight(Clone)"), Is.Not.Null);
            Assert.That(stream.GetComponentsInChildren<BoxCollider>().Any(item => item.name.StartsWith("Barrier")), Is.True);

            Assert.That(catalog.TryGetTheme(config.themeId, out var theme), Is.True);
            var planner = new SegmentPlanner(config.seed, alternateSettings,
                theme.Segments.Select(segment => segment.Id), config.obstacleIds);
            var next = planner.Next();
            int lane = RunnerTrack.CenterLane;
            Click(hud, "Ready/Start");
            float finish = Time.time + 300f;
            while (Time.time < finish && session.Feedback.State == RunnerState.Running)
            {
                if (motor.transform.position.z >= next.Index * SegmentPlanner.SegmentLength)
                {
                    if (next.SafeLane != lane)
                        Click(hud, next.SafeLane < lane ? "Running/Left" : "Running/Right");
                    lane = next.SafeLane;
                    next = planner.Next();
                }

                yield return null;
            }

            Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Running));
            Assert.That(motor.transform.position.z, Is.GreaterThan(3000f));
            Assert.That(stream.FirstStartZ, Is.GreaterThan(2900f));
            Assert.That(stream.ActiveSegmentCount, Is.EqualTo(6));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadSource()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static GameObject Panel(RunnerHud hud, string path) => hud.transform.Find("SafeArea/" + path).gameObject;

        private static void Click(RunnerHud hud, string path)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(Panel(hud, path), pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
#endif
