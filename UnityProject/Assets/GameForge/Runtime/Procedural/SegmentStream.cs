using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameForge.Runtime
{
    public sealed class SegmentStream : MonoBehaviour
    {
        private Dictionary<string, GameObject> segmentPrefabs;
        private Dictionary<string, GameObject> obstaclePrefabs;
        private BoundedPrefabPool segmentPool;
        private BoundedPrefabPool obstaclePool;
        private SegmentPlanner planner;
        private readonly Queue<ActiveSegment> active = new Queue<ActiveSegment>();
        private int windowSize;
        private Color? trackTint;
        private Color? obstacleTint;
        private RunnerEnemyDesign enemyDesign;

        public int ActiveSegmentCount => active.Count;
        public int PooledSegmentCount => segmentPool?.Count ?? 0;
        public int PooledObstacleCount => obstaclePool?.Count ?? 0;
        public float FirstStartZ => active.Count > 0 ? StartZ(active.Peek().Index) : transform.position.z;
        public float EndZ => FirstStartZ + active.Count * SegmentPlanner.SegmentLength;

        public void Configure(IReadOnlyList<TrackSegmentPrefab> segments, IReadOnlyList<TrackObstaclePrefab> obstacles,
            int windowSize = 6)
        {
            if (segments == null || segments.Count == 0)
                throw new ArgumentException("Au moins un préfabriqué de piste est requis.", nameof(segments));
            if (obstacles == null)
                throw new ArgumentNullException(nameof(obstacles));
            if (windowSize < 3 || windowSize > 16)
                throw new ArgumentOutOfRangeException(nameof(windowSize), "La fenêtre doit contenir de 3 à 16 segments.");
            if (transform.lossyScale != Vector3.one || Quaternion.Angle(transform.rotation, Quaternion.identity) >= 0.01f)
                throw new InvalidOperationException("Le flux doit suivre l’axe Z sans mise à l’échelle.");

            var tracks = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment.Id) || !segment.HasCompatibleAnchors() ||
                    !tracks.TryAdd(segment.Id, segment.Prefab))
                    throw new ArgumentException("Identifiant de piste ou ancrages Entry/Exit invalides.", nameof(segments));
            }

            var props = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var obstacle in obstacles)
            {
                if (string.IsNullOrWhiteSpace(obstacle.Id) || obstacle.Prefab == null ||
                    !props.TryAdd(obstacle.Id, obstacle.Prefab))
                    throw new ArgumentException("Identifiant ou préfabriqué d’obstacle invalide.", nameof(obstacles));
            }

            ReleaseActive();
            segmentPool?.Clear();
            obstaclePool?.Clear();
            planner = null;
            segmentPrefabs = tracks;
            obstaclePrefabs = props;
            this.windowSize = windowSize;
            segmentPool = new BoundedPrefabPool(windowSize, transform, TintTrack);
            obstaclePool = new BoundedPrefabPool(windowSize * 3, transform, TintObstacle);
        }

        public void Reset(int seed, RunnerSettings settings)
        {
            if (segmentPrefabs == null)
                throw new InvalidOperationException("Configurer la piste avant de la réinitialiser.");

            var nextPlanner = new SegmentPlanner(seed, settings, segmentPrefabs.Keys, obstaclePrefabs.Keys);
            ReleaseActive();
            var nextTrackTint = settings.TrackColor;
            var nextObstacleTint = settings.ObstacleColor;
            if (!Nullable.Equals(trackTint, nextTrackTint))
                segmentPool.Clear();
            if (!Nullable.Equals(obstacleTint, nextObstacleTint) || enemyDesign != settings.EnemyDesign)
                obstaclePool.Clear();
            trackTint = nextTrackTint;
            obstacleTint = nextObstacleTint;
            enemyDesign = settings.EnemyDesign;
            planner = nextPlanner;
            for (int i = 0; i < windowSize; i++)
                SpawnNext();
        }

        public void AdvanceTo(float runnerZ)
        {
            if (planner == null)
                throw new InvalidOperationException("Réinitialiser la piste avant de la faire avancer.");
            if (float.IsNaN(runnerZ) || float.IsInfinity(runnerZ) || runnerZ >= EndZ)
                throw new ArgumentOutOfRangeException(nameof(runnerZ), "Le joueur doit rester dans la fenêtre active.");

            // shortcut: avance continue, réinitialiser avant une téléportation hors fenêtre.
            while (runnerZ >= FirstStartZ + 2f * SegmentPlanner.SegmentLength)
            {
                Release(active.Dequeue());
                SpawnNext();
            }
        }

        private float StartZ(int index) => transform.position.z + index * SegmentPlanner.SegmentLength;

        private void TintTrack(GameObject segment)
        {
            // The catalog keeps its lane markings separate from the floor.
            if (trackTint is Color color && segment.transform.Find("Floor") is Transform floor)
                RunnerTint.Apply(floor.gameObject, color);
        }

        private void TintObstacle(GameObject obstacle)
        {
            if (enemyDesign != RunnerEnemyDesign.Catalog)
                RunnerEnemyVisual.Apply(obstacle, enemyDesign);
            if (obstacleTint is Color color)
                RunnerTint.Apply(obstacle, color);
        }

        private void SpawnNext()
        {
            TrackSegmentPlan plan = planner.Next();
            GameObject trackPrefab = segmentPrefabs[plan.SegmentId];
            GameObject track = segmentPool.Rent(trackPrefab, transform,
                Vector3.forward * (plan.Index * SegmentPlanner.SegmentLength));

            var props = new PoolItem[plan.Obstacles.Count];
            for (int i = 0; i < props.Length; i++)
            {
                var placement = plan.Obstacles[i];
                var prefab = obstaclePrefabs[placement.Id];
                float x = (placement.Lane - RunnerTrack.CenterLane) * RunnerTrack.LaneSpacing;
                var prop = obstaclePool.Rent(prefab, track.transform, new Vector3(x, 0f, placement.Offset));
                props[i] = new PoolItem(prefab, prop);
            }

            active.Enqueue(new ActiveSegment(plan.Index, new PoolItem(trackPrefab, track), props));
        }

        private void ReleaseActive()
        {
            while (active.Count > 0)
                Release(active.Dequeue());
        }

        private void Release(ActiveSegment segment)
        {
            foreach (var obstacle in segment.Obstacles)
                obstaclePool.Return(obstacle);
            segmentPool.Return(segment.Track);
        }

        private readonly struct ActiveSegment
        {
            public ActiveSegment(int index, PoolItem track, PoolItem[] obstacles)
            {
                Index = index;
                Track = track;
                Obstacles = obstacles;
            }

            public int Index { get; }
            public PoolItem Track { get; }
            public PoolItem[] Obstacles { get; }
        }

        private readonly struct PoolItem
        {
            public PoolItem(GameObject prefab, GameObject instance)
            {
                Prefab = prefab;
                Instance = instance;
            }

            public GameObject Prefab { get; }
            public GameObject Instance { get; }
        }

        private sealed class BoundedPrefabPool
        {
            private readonly List<PoolItem> parked = new List<PoolItem>();
            private readonly int capacity;
            private readonly Transform root;
            private readonly Action<GameObject> initialize;

            public BoundedPrefabPool(int capacity, Transform root, Action<GameObject> initialize)
            {
                this.capacity = capacity;
                this.root = root;
                this.initialize = initialize;
            }

            public int Count => parked.Count;

            public GameObject Rent(GameObject prefab, Transform parent, Vector3 position)
            {
                GameObject instance = null;
                for (int i = parked.Count - 1; i >= 0; i--)
                {
                    if (parked[i].Prefab != prefab)
                        continue;

                    instance = parked[i].Instance;
                    parked.RemoveAt(i);
                    break;
                }

                if (instance == null)
                {
                    instance = UnityEngine.Object.Instantiate(prefab, parent);
                    initialize(instance);
                }
                else
                    instance.transform.SetParent(parent, false);

                instance.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
                instance.SetActive(true);
                return instance;
            }

            public void Return(PoolItem item)
            {
                item.Instance.SetActive(false);
                item.Instance.transform.SetParent(root, false);
                if (parked.Count == capacity)
                {
                    UnityEngine.Object.Destroy(parked[0].Instance);
                    parked.RemoveAt(0);
                }

                parked.Add(item);
            }

            public void Clear()
            {
                foreach (var item in parked)
                    UnityEngine.Object.Destroy(item.Instance);
                parked.Clear();
            }
        }
    }
}
