namespace GameForge.Runtime
{
    public interface IRunnerCatalog
    {
        bool HasTheme(string id);
        bool HasObstacle(string id);
    }

    public enum RunnerState
    {
        Ready,
        Running,
        Failed,
        Succeeded
    }

    public enum RunnerObjective
    {
        Endless,
        Distance,
        Sprint,
        Endurance
    }

    public enum RunnerEnemyDesign
    {
        Catalog,
        Robot,
        Creature,
        Golem,
        Drone,
        Plant
    }

    public enum LaneIntent
    {
        Left = -1,
        Right = 1
    }

    public static class RunnerTrack
    {
        public const int LaneCount = 3;
        public const int CenterLane = 1;
        public const float LaneSpacing = 2f;
        public const float LaneChangeSeconds = 0.22f;
    }

    public readonly struct RunnerFeedback
    {
        public RunnerFeedback(RunnerState state, float distance, float speed)
            : this(state, distance, speed, RunnerObjective.Endless, 0, 0)
        {
        }

        public RunnerFeedback(RunnerState state, float distance, float speed, RunnerObjective objective,
            int goalDistance, float remainingSeconds)
        {
            State = state;
            Distance = distance;
            Speed = speed;
            Objective = objective;
            GoalDistance = goalDistance;
            RemainingSeconds = remainingSeconds;
        }

        public RunnerState State { get; }
        public float Distance { get; }
        public float Speed { get; }
        public RunnerObjective Objective { get; }
        public int GoalDistance { get; }
        public float RemainingSeconds { get; }
    }

    public readonly struct RunnerDiagnostic
    {
        public RunnerDiagnostic(string field, string message)
        {
            Field = field;
            Message = message;
        }

        public string Field { get; }
        public string Message { get; }
    }
}
