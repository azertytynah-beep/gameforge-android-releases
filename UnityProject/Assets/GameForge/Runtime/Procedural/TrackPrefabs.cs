using System;
using UnityEngine;

namespace GameForge.Runtime
{
    [Serializable]
    public struct TrackSegmentPrefab
    {
        [SerializeField] private string id;
        [SerializeField] private GameObject prefab;

        public TrackSegmentPrefab(string id, GameObject prefab)
        {
            this.id = id;
            this.prefab = prefab;
        }

        public string Id => id;
        public GameObject Prefab => prefab;

        internal bool HasCompatibleAnchors()
        {
            if (prefab == null || prefab.transform.localScale != Vector3.one)
                return false;

            return IsAligned(prefab.transform.Find("Entry"), Vector3.zero) &&
                   IsAligned(prefab.transform.Find("Exit"), Vector3.forward * SegmentPlanner.SegmentLength);
        }

        private static bool IsAligned(Transform anchor, Vector3 position)
        {
            return anchor != null && anchor.localPosition == position &&
                   Quaternion.Angle(anchor.localRotation, Quaternion.identity) < 0.01f;
        }
    }

    [Serializable]
    public struct TrackObstaclePrefab
    {
        [SerializeField] private string id;
        [SerializeField] private GameObject prefab;

        public TrackObstaclePrefab(string id, GameObject prefab)
        {
            this.id = id;
            this.prefab = prefab;
        }

        public string Id => id;
        public GameObject Prefab => prefab;
    }
}
