#if UNITY_EDITOR
using System.Collections.Generic;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameForge.Tests.PlayMode
{
    public sealed class RunnerHudTests
    {
        private const string Prefab = "Assets/GameForge/Content/GameUI/RunnerHud.prefab";
        private GameObject instance;
        private GameObject host;
        private RunnerSettings settings;
        private RunnerSession session;
        private RunnerHud hud;
        private EventSystem eventSystem;

        [SetUp]
        public void SetUp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Assert.That(asset, Is.Not.Null);
            instance = Object.Instantiate(asset);
            host = new GameObject("HUD fixture");
            eventSystem = host.AddComponent<EventSystem>();
            session = host.AddComponent<RunnerSession>();
            Assert.That(RunnerConfigValidator.TryCreateSettings(RunnerConfigV1.CreateDefault(),
                new FixtureCatalog(), out settings, out _), Is.True);
            session.Configure(settings);
            hud = instance.GetComponent<RunnerHud>();
            hud.Bind(session);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void MenusAndTouchButtonsFollowTheSession()
        {
            var directions = new List<LaneIntent>();
            hud.DirectionRequested += directions.Add;
            Assert.That(Panel("Ready").activeSelf, Is.True);
            Assert.That(Panel("Running").activeSelf, Is.False);
            Click("Running/Left");
            Assert.That(directions, Is.Empty);

            var start = Panel("Ready/Start");
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(start));
            ExecuteEvents.Execute(start, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Running));

            Click("Running/Left");
            Click("Running/Right");
            Assert.That(directions, Is.EqualTo(new[] { LaneIntent.Left, LaneIntent.Right }));
            session.Advance(2f);
            Assert.That(Panel("Running/Score").GetComponent<Text>().text, Is.EqualTo("12 m"));

            Assert.That(session.ReportCollision(), Is.True);
            Assert.That(Panel("Failed").activeSelf, Is.True);
            Assert.That(Panel("Running").activeSelf, Is.False);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(Panel("Failed/Restart")));
            Click("Failed/Restart");
            Assert.That(session.Feedback.State, Is.EqualTo(RunnerState.Running));
            Assert.That(Panel("Running/Score").GetComponent<Text>().text, Is.EqualTo("0 m"));
        }

        [TestCase(1080, 2400)]
        [TestCase(720, 1280)]
        public void TouchButtonsFitInsideSimulatedPortraitSafeArea(int width, int height)
        {
            var canvas = instance.GetComponent<Canvas>();
            canvas.enabled = false;
            var viewport = instance.GetComponent<RectTransform>();
            viewport.sizeDelta = new Vector2(width, height);
            var area = new Rect(24, 36, width - 48, height - 126);
            hud.ApplySafeArea(area, new Vector2Int(width, height));
            session.StartRun();
            Canvas.ForceUpdateCanvases();

            var safe = instance.transform.Find("SafeArea").GetComponent<RectTransform>();
            Assert.That(safe.anchorMin.x, Is.EqualTo(area.xMin / width).Within(0.0001f));
            Assert.That(safe.anchorMin.y, Is.EqualTo(area.yMin / height).Within(0.0001f));
            Assert.That(safe.anchorMax.x, Is.EqualTo(area.xMax / width).Within(0.0001f));
            Assert.That(safe.anchorMax.y, Is.EqualTo(area.yMax / height).Within(0.0001f));

            var left = WorldRect(Panel("Running/Left").GetComponent<RectTransform>());
            var right = WorldRect(Panel("Running/Right").GetComponent<RectTransform>());
            var bounds = WorldRect(safe);
            Assert.That(bounds.Contains(left.min) && bounds.Contains(left.max), Is.True);
            Assert.That(bounds.Contains(right.min) && bounds.Contains(right.max), Is.True);
            Assert.That(left.Overlaps(right), Is.False);
        }

        [Test]
        public void SprintShowsItsGoalCountdownAndTimeout()
        {
            var config = RunnerConfigV1.CreateDefault();
            config.objective = RunnerObjective.Sprint;
            config.goalDistance = 100;
            config.timeLimitSeconds = 3;
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, new FixtureCatalog(), out var sprint, out _), Is.True);
            session.Configure(sprint);
            Object.DestroyImmediate(settings);
            settings = sprint;

            Assert.That(Panel("Ready/Title").GetComponent<Text>().text, Does.Contain("100 m en 3 s"));
            Click("Ready/Start");
            Assert.That(Panel("Running/Score").GetComponent<Text>().text, Is.EqualTo("0 / 100 m · 3 s"));
            session.Advance(3f);
            Assert.That(Panel("Failed").activeSelf, Is.True);
            Assert.That(Panel("Failed/Title").GetComponent<Text>().text, Is.EqualTo("TEMPS ÉCOULÉ"));
            Click("Failed/Restart");
            Assert.That(Panel("Running/Score").GetComponent<Text>().text, Is.EqualTo("0 / 100 m · 3 s"));
        }

        private GameObject Panel(string path) => instance.transform.Find("SafeArea/" + path).gameObject;

        private void Click(string path)
        {
            var data = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(Panel(path), data, ExecuteEvents.pointerClickHandler);
        }

        private static Rect WorldRect(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private sealed class FixtureCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default";
            public bool HasObstacle(string id) => id == "obstacle-block";
        }
    }
}
#endif
