using UnityEngine;

namespace GameForge.Runtime
{
    [RequireComponent(typeof(Camera))]
    public sealed class RunnerCameraFollow : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
        [SerializeField, Min(0f)] private float lookAhead = 4f;

        private Transform target;

        public void Bind(Transform player)
        {
            target = player;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var position = target.position + offset;
            var direction = target.position + Vector3.forward * lookAhead - position;
            var rotation = direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : transform.rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
