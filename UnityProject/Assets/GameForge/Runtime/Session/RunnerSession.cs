using System;
using UnityEngine;

namespace GameForge.Runtime
{
    public sealed class RunnerSession : MonoBehaviour
    {
        private RunnerSettings settings;
        private RunnerState state;
        private float distance;
        private float speed;
        private bool resetting;

        public RunnerFeedback Feedback => new RunnerFeedback(state, distance, speed);

        public event Action<RunnerFeedback> FeedbackChanged;
        public event Action ResetRequested;

        public void Configure(RunnerSettings value)
        {
            settings = value ? value : throw new ArgumentNullException(nameof(value));
            state = RunnerState.Ready;
            distance = 0;
            speed = settings.InitialSpeed;
            Publish();
        }

        public bool StartRun() => state == RunnerState.Ready && BeginRun();

        public bool Restart() => state == RunnerState.Failed && BeginRun();

        public bool ReportCollision()
        {
            if (state != RunnerState.Running)
                return false;

            state = RunnerState.Failed;
            Publish();
            return true;
        }

        public void Advance(float seconds)
        {
            if (state != RunnerState.Running || !(seconds > 0f) || float.IsInfinity(seconds))
                return;

            speed = Mathf.Min(settings.MaxSpeed, speed + settings.Acceleration * seconds);
            distance += speed * seconds;
            Publish();
        }

        private bool BeginRun()
        {
            if (settings == null || resetting)
                return false;

            resetting = true;
            try
            {
                // Keep input disabled until the motor and procedural track have both reset.
                ResetRequested?.Invoke();
                distance = 0;
                speed = settings.InitialSpeed;
                state = RunnerState.Running;
            }
            finally
            {
                resetting = false;
            }

            Publish();
            return true;
        }

        private void Publish() => FeedbackChanged?.Invoke(Feedback);
    }
}
