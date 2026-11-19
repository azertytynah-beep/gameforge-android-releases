using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameForge.Runtime
{
    public sealed class RunnerHud : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private GameObject runningPanel;
        [SerializeField] private GameObject failedPanel;
        [SerializeField] private Button start;
        [SerializeField] private Button restart;
        [SerializeField] private Button left;
        [SerializeField] private Button right;
        [SerializeField] private Text score;
        [SerializeField] private Text finalScore;

        private RunnerSession session;
        private RunnerState? displayedState;
        private Rect lastArea;
        private Vector2Int lastResolution;
        private Text readyTitle;
        private Text resultTitle;
        private string defaultReadyTitle;
        private string defaultResultTitle;
        private (RunnerObjective objective, int distance, int goal, int seconds)? displayedScore;

        public event Action<LaneIntent> DirectionRequested;

        private void Awake()
        {
            // Existing variants share the same authored title and result label hierarchy.
            readyTitle = readyPanel.transform.Find("Title")?.GetComponent<Text>();
            resultTitle = failedPanel.transform.Find("Title")?.GetComponent<Text>();
            defaultReadyTitle = readyTitle != null ? readyTitle.text : string.Empty;
            defaultResultTitle = resultTitle != null ? resultTitle.text : string.Empty;
            start.onClick.AddListener(() => session?.StartRun());
            restart.onClick.AddListener(() => session?.Restart());
            left.onClick.AddListener(() => Move(LaneIntent.Left));
            right.onClick.AddListener(() => Move(LaneIntent.Right));
        }

        public void Bind(RunnerSession target)
        {
            if (session != null)
                session.FeedbackChanged -= Present;

            session = target ? target : throw new ArgumentNullException(nameof(target));
            displayedState = null;
            displayedScore = null;
            session.FeedbackChanged += Present;
            Present(session.Feedback);
        }

        private void OnDestroy()
        {
            if (session != null)
                session.FeedbackChanged -= Present;
        }

        private void Update()
        {
            ApplySafeArea(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                Move(LaneIntent.Left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                Move(LaneIntent.Right);
        }

        public void ApplySafeArea(Rect area, Vector2Int resolution)
        {
            if (resolution.x <= 0 || resolution.y <= 0 || (area == lastArea && resolution == lastResolution))
                return;

            var size = new Vector2(resolution.x, resolution.y);
            safeArea.anchorMin = Vector2.Max(Vector2.zero, new Vector2(area.xMin / size.x, area.yMin / size.y));
            safeArea.anchorMax = Vector2.Min(Vector2.one, new Vector2(area.xMax / size.x, area.yMax / size.y));
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            lastArea = area;
            lastResolution = resolution;
        }

        private void Move(LaneIntent intent)
        {
            if (session != null && session.Feedback.State == RunnerState.Running)
                DirectionRequested?.Invoke(intent);
        }

        private void Present(RunnerFeedback feedback)
        {
            if (displayedState != feedback.State)
            {
                bool ended = feedback.State is RunnerState.Failed or RunnerState.Succeeded;
                readyPanel.SetActive(feedback.State == RunnerState.Ready);
                runningPanel.SetActive(feedback.State == RunnerState.Running);
                failedPanel.SetActive(ended);
                displayedState = feedback.State;
                if (ended && resultTitle != null)
                    resultTitle.text = ResultTitle(feedback);
                var selected = feedback.State == RunnerState.Ready ? start : ended ? restart : null;
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(selected ? selected.gameObject : null);
            }

            var readings = (objective: feedback.Objective, distance: Mathf.FloorToInt(feedback.Distance),
                goal: feedback.GoalDistance, seconds: Mathf.CeilToInt(feedback.RemainingSeconds));
            if (displayedScore == readings)
                return;

            displayedScore = readings;
            var distance = $"{readings.distance} m";
            var progress = $"{readings.distance} / {readings.goal} m";
            int seconds = readings.seconds;
            score.text = readings.objective switch
            {
                RunnerObjective.Distance => progress,
                RunnerObjective.Sprint => $"{progress} · {seconds} s",
                RunnerObjective.Endurance => $"{seconds} s · {distance}",
                _ => distance
            };
            finalScore.text = readings.objective is RunnerObjective.Distance or RunnerObjective.Sprint
                ? progress : distance;
            bool timed = readings.objective is RunnerObjective.Sprint or RunnerObjective.Endurance;
            SetBestFit(score, timed);

            if (feedback.State == RunnerState.Ready && readyTitle != null)
            {
                readyTitle.text = readings.objective switch
                {
                    RunnerObjective.Distance => $"{defaultReadyTitle}\nAtteins {readings.goal} m",
                    RunnerObjective.Sprint => $"{defaultReadyTitle}\nAtteins {readings.goal} m en {seconds} s",
                    RunnerObjective.Endurance => $"{defaultReadyTitle}\nSurvis {seconds} s",
                    _ => defaultReadyTitle
                };
                SetBestFit(readyTitle, readings.objective != RunnerObjective.Endless);
            }
        }

        private string ResultTitle(RunnerFeedback feedback)
        {
            if (feedback.State == RunnerState.Succeeded)
                return "OBJECTIF ATTEINT";
            if (feedback.Objective == RunnerObjective.Sprint && feedback.RemainingSeconds == 0)
                return "TEMPS ÉCOULÉ";
            return defaultResultTitle;
        }

        private static void SetBestFit(Text label, bool enabled)
        {
            if (label.resizeTextForBestFit == enabled)
                return;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextForBestFit = enabled;
        }
    }
}
