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

        internal static RunnerSettings FromValidatedConfig(RunnerConfigV1 config)
        {
            var settings = CreateInstance<RunnerSettings>();
            settings.configuration = config.Copy();
            return settings;
        }

        public RunnerConfigV1 ToConfig() => configuration.Copy();
    }
}
