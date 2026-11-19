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
        private int displayedScore = -1;

        public event Action<LaneIntent> DirectionRequested;

        private void Awake()
        {
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
            displayedScore = -1;
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
                readyPanel.SetActive(feedback.State == RunnerState.Ready);
                runningPanel.SetActive(feedback.State == RunnerState.Running);
                failedPanel.SetActive(feedback.State == RunnerState.Failed);
                displayedState = feedback.State;
                var selected = feedback.State == RunnerState.Ready ? start :
                    feedback.State == RunnerState.Failed ? restart : null;
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(selected ? selected.gameObject : null);
            }

            int distance = Mathf.FloorToInt(feedback.Distance);
            if (distance == displayedScore)
                return;

            displayedScore = distance;
            score.text = $"{distance} m";
            finalScore.text = $"{distance} m";
        }
    }
}
