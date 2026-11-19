using System;
using System.Collections.Generic;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    internal sealed class GameForgeSteps : VisualElement
    {
        private const string SelectedClass = "gameforge-step--active";
        private const double Duration = 0.25;

        private readonly Button[] steps;
        private readonly VisualElement lens = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly VisualElement glint = new VisualElement { pickingMode = PickingMode.Ignore };
        private IVisualElementScheduledItem animation;
        private int active = -1;
        private bool hasFrame;
        private bool hasPointer;
        private Vector2 pointer;
        private Rect displayed;
        private Rect departure;
        private Rect destination;
        private double started;

        public GameForgeSteps(IReadOnlyList<string> labels, Action<int> select)
        {
            if (labels == null || labels.Count == 0)
                throw new ArgumentException("Au moins une étape est requise.", nameof(labels));
            if (select == null)
                throw new ArgumentNullException(nameof(select));

            AddToClassList("gameforge-steps");
            lens.AddToClassList("gameforge-lens");
            lens.style.position = Position.Absolute;
            lens.style.overflow = Overflow.Hidden;
            lens.style.visibility = Visibility.Hidden;
            glint.AddToClassList("gameforge-lens-glint");
            glint.style.position = Position.Absolute;
            lens.Add(glint);
            Add(lens);

            steps = new Button[labels.Count];
            for (int index = 0; index < labels.Count; index++)
            {
                int step = index;
                var button = new Button(() =>
                {
                    SetActive(step);
                    select(step);
                }) { name = $"gameforge-step-{step}", text = labels[step] };
                button.AddToClassList("gameforge-step");
                button.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    if (step == active)
                        MoveToSelection(false);
                });
                Add(button);
                steps[step] = button;
            }

            RegisterCallback<AttachToPanelEvent>(_ => MoveToSelection(false));
            RegisterCallback<GeometryChangedEvent>(_ => MoveToSelection(false));
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                animation?.Pause();
                hasFrame = hasPointer = false;
                lens.style.visibility = Visibility.Hidden;
            });
            RegisterCallback<PointerMoveEvent>(evt =>
            {
                pointer = this.WorldToLocal((Vector2)evt.position);
                hasPointer = true;
                UpdateGlint();
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                hasPointer = false;
                UpdateGlint();
            });
            SetActive(0);
        }

        public void SetActive(int index)
        {
            if (index < 0 || index >= steps.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (index == active)
                return;

            if (active >= 0)
                steps[active].RemoveFromClassList(SelectedClass);
            active = index;
            steps[active].AddToClassList(SelectedClass);
            MoveToSelection(true);
        }

        private void MoveToSelection(bool animate)
        {
            if (active < 0 || panel == null)
                return;

            Rect target = steps[active].layout;
            float bounds = target.x + target.y + target.width + target.height;
            if (target.width <= 0 || target.height <= 0 || float.IsNaN(bounds) || float.IsInfinity(bounds))
                return;
            if (hasFrame && target.Equals(destination))
                return;

            destination = target;
            if (!animate || !hasFrame || ReduceMotion())
            {
                animation?.Pause();
                Draw(target);
                return;
            }

            departure = displayed;
            started = EditorApplication.timeSinceStartup;
            if (animation == null)
                animation = schedule.Execute(Advance).Every(16);
            else
                animation.Resume();
        }

        private void Advance()
        {
            float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - started) / Duration));
            if (progress >= 1)
            {
                Draw(destination);
                animation?.Pause();
                return;
            }

            float follow = Mathf.SmoothStep(0, 1, progress);
            // The leading edge arrives first so the lens stretches without moving the buttons.
            float lead = Mathf.Abs(departure.center.y - destination.center.y) < destination.height / 2
                ? 1 - Mathf.Pow(1 - progress, 3)
                : follow;
            bool movingRight = destination.center.x >= departure.center.x;
            float left = Mathf.Lerp(departure.xMin, destination.xMin, movingRight ? follow : lead);
            float right = Mathf.Lerp(departure.xMax, destination.xMax, movingRight ? lead : follow);
            Draw(new Rect(left, Mathf.Lerp(departure.y, destination.y, follow), Mathf.Max(0, right - left),
                Mathf.Lerp(departure.height, destination.height, follow)));
        }

        private void Draw(Rect frame)
        {
            displayed = frame;
            hasFrame = true;
            lens.style.visibility = Visibility.Visible;
            lens.style.left = frame.x;
            lens.style.top = frame.y;
            lens.style.width = frame.width;
            lens.style.height = frame.height;
            UpdateGlint();
        }

        private void UpdateGlint()
        {
            if (!hasFrame)
                return;

            bool overLens = hasPointer && displayed.Contains(pointer);
            float width = Mathf.Min(72, displayed.width * 0.6f);
            float position = overLens ? pointer.x - displayed.x : displayed.width / 2;
            glint.style.width = width;
            glint.style.left = Mathf.Clamp(position - width / 2, 0, displayed.width - width);
            glint.style.opacity = overLens ? 0.85f : 0.35f;
        }

        private static bool ReduceMotion()
        {
#if UNITY_EDITOR_WIN
            const uint getClientAreaAnimation = 0x1042;
            return SystemParametersInfo(getClientAreaAnimation, 0, out int enabled, 0) && enabled == 0;
#else
            return false;
#endif
        }

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint action, uint parameter, out int value, uint flags);
#endif
    }
}
