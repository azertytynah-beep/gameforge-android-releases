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
        private float elapsed;
        private bool resetting;

        private bool IsTimed => settings.Objective is RunnerObjective.Sprint or RunnerObjective.Endurance;

        public RunnerFeedback Feedback => settings == null
            ? new RunnerFeedback(state, distance, speed)
            : new RunnerFeedback(state, distance, speed, settings.Objective, settings.GoalDistance,
                IsTimed ? Mathf.Max(0, settings.TimeLimitSeconds - elapsed) : 0);

        public event Action<RunnerFeedback> FeedbackChanged;
        public event Action ResetRequested;

        public void Configure(RunnerSettings value)
        {
            settings = value ? value : throw new ArgumentNullException(nameof(value));
            state = RunnerState.Ready;
            distance = 0;
            elapsed = 0;
            speed = settings.InitialSpeed;
            Publish();
        }

        public bool StartRun() => state == RunnerState.Ready && BeginRun();

        public bool Restart() => (state == RunnerState.Failed || state == RunnerState.Succeeded) && BeginRun();

        public bool ReportCollision()
        {
            if (state != RunnerState.Running)
                return false;

            state = RunnerState.Failed;
            Publish();
            return true;
        }

        public void Advance(float seconds) => Advance(seconds, null);

        internal void Advance(float seconds, Action<float, float> move)
        {
            if (state != RunnerState.Running || !(seconds > 0f) || float.IsInfinity(seconds))
                return;

            float remaining = IsTimed ? settings.TimeLimitSeconds - elapsed : float.PositiveInfinity;
            seconds = Mathf.Min(seconds, remaining);
            speed = Mathf.Min(settings.MaxSpeed, speed + settings.Acceleration * seconds);
            float distanceToGoal = settings.GoalDistance - distance;
            bool reachedGoal = (settings.Objective is RunnerObjective.Distance or RunnerObjective.Sprint) &&
                speed * seconds >= distanceToGoal;
            if (reachedGoal)
                seconds = distanceToGoal / speed;

            // A collision during the final step still ends the run before the objective is awarded.
            move?.Invoke(speed, seconds);
            if (state != RunnerState.Running)
                return;

            distance = reachedGoal ? settings.GoalDistance : distance + speed * seconds;
            bool timeExpired = IsTimed && seconds >= remaining;
            if (IsTimed)
                elapsed = timeExpired ? settings.TimeLimitSeconds : elapsed + seconds;
            if (reachedGoal || (timeExpired && settings.Objective == RunnerObjective.Endurance))
                state = RunnerState.Succeeded;
            else if (timeExpired)
                state = RunnerState.Failed;
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
                elapsed = 0;
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
