using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameForge.Runtime
{
    [Serializable]
    public sealed class RunnerTheme
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private TrackSegmentPrefab[] segments = Array.Empty<TrackSegmentPrefab>();

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<TrackSegmentPrefab> Segments => segments;
    }

    public sealed class RunnerCatalog : ScriptableObject, IRunnerCatalog
    {
        [SerializeField] private RunnerTheme[] themes = Array.Empty<RunnerTheme>();
        [SerializeField] private TrackObstaclePrefab[] obstacles = Array.Empty<TrackObstaclePrefab>();

        public IReadOnlyList<RunnerTheme> Themes => themes;
        public IReadOnlyList<TrackObstaclePrefab> Obstacles => obstacles;

        public bool HasTheme(string id) => TryGetTheme(id, out _);

        public bool TryGetTheme(string id, out RunnerTheme theme)
        {
            foreach (var candidate in themes)
            {
                if (candidate != null && candidate.Id == id && !string.IsNullOrEmpty(id))
                {
                    theme = candidate;
                    return true;
                }
            }

            theme = null;
            return false;
        }

        public bool HasObstacle(string id)
        {
            foreach (var obstacle in obstacles)
            {
                if (obstacle.Id == id && !string.IsNullOrEmpty(id))
                    return true;
            }

            return false;
        }
    }
}
