using System.Collections.Generic;
using UnityEngine;

namespace GameForge.Runtime
{
    public sealed class RunnerSettings : ScriptableObject
    {
        [SerializeField] private RunnerConfigV1 configuration;

        public string Title => configuration.title;
        public int Seed => configuration.seed;
        public float InitialSpeed => configuration.initialSpeed;
        public float MaxSpeed => configuration.maxSpeed;
        public float Acceleration => configuration.acceleration;
        public int ObstacleDensity => configuration.obstacleDensity;
        public string ThemeId => configuration.themeId;
        public IReadOnlyList<string> ObstacleIds => System.Array.AsReadOnly(configuration.obstacleIds);
        public RunnerObjective Objective => configuration.objective;
        public int GoalDistance => configuration.goalDistance;
        public int TimeLimitSeconds => configuration.timeLimitSeconds;
        internal RunnerEnemyDesign EnemyDesign => configuration.enemyDesign;
        internal Color? SkyColor => ParseColor(configuration.skyColor);
        internal Color? TrackColor => ParseColor(configuration.trackColor);
        internal Color? PlayerColor => ParseColor(configuration.playerColor);
        internal Color? ObstacleColor => ParseColor(configuration.obstacleColor);

        internal static RunnerSettings FromValidatedConfig(RunnerConfigV1 config)
        {
            var settings = CreateInstance<RunnerSettings>();
            settings.configuration = config.Copy();
            return settings;
        }

        public RunnerConfigV1 ToConfig() => configuration.Copy();

        private static Color? ParseColor(string value) =>
            !string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out var color) ? color : (Color?)null;
    }
}
