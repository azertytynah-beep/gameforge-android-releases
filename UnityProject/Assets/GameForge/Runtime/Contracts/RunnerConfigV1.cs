using System;

namespace GameForge.Runtime
{
    [Serializable]
    public sealed class RunnerConfigV1
    {
        public const int Version = 1;
        public const string RunnerTemplateId = "runner-3d";

        public int schemaVersion;
        public string templateId;
        public string title;
        public int seed;
        public float initialSpeed;
        public float maxSpeed;
        public float acceleration;
        public int obstacleDensity;
        public string themeId;
        public string[] obstacleIds;

        public static RunnerConfigV1 CreateDefault(string themeId = "theme-default", string obstacleId = "obstacle-block")
        {
            return new RunnerConfigV1
            {
                schemaVersion = Version,
                templateId = RunnerTemplateId,
                title = "Endless Runner",
                seed = 0,
                initialSpeed = 6f,
                maxSpeed = 12f,
                acceleration = 0.15f,
                obstacleDensity = 1,
                themeId = themeId,
                obstacleIds = new[] { obstacleId }
            };
        }

        public RunnerConfigV1 Copy()
        {
            var copy = (RunnerConfigV1)MemberwiseClone();
            copy.obstacleIds = obstacleIds == null ? null : (string[])obstacleIds.Clone();
            return copy;
        }
    }
}
