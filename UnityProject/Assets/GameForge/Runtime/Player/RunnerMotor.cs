using System;
using UnityEngine;

namespace GameForge.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RunnerMotor : MonoBehaviour
    {
        [SerializeField] private LayerMask obstacleLayers;

        private CharacterController controller;
        private float trackCenterX;
        private float maxSpeed;
        private int lane = RunnerTrack.CenterLane;
        private bool running;
        private bool hitFatalObstacle;

        public event Action FatalCollision;

        public LayerMask ObstacleLayers
        {
            get => obstacleLayers;
            set => obstacleLayers = value;
        }

        public void Configure(RunnerSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            maxSpeed = settings.MaxSpeed;
            controller = GetComponent<CharacterController>();
            trackCenterX = transform.position.x;
        }

        public void ResetPosition(Vector3 origin)
        {
            running = false;
            hitFatalObstacle = false;
            lane = RunnerTrack.CenterLane;
            trackCenterX = origin.x;

            var capsule = GetComponent<CharacterController>();
            var wasEnabled = capsule.enabled;
            capsule.enabled = false;
            transform.position = origin;
            capsule.enabled = wasEnabled;
        }

        public void SetRunning(bool value) => running = value && !hitFatalObstacle;

        public void RequestLane(LaneIntent intent)
        {
            if (running && (intent == LaneIntent.Left || intent == LaneIntent.Right))
                lane = Mathf.Clamp(lane + (int)intent, 0, RunnerTrack.LaneCount - 1);
        }

        public void Step(float speed, float deltaTime)
        {
            if (!running || controller == null || !controller.enabled || !(deltaTime > 0f) ||
                float.IsInfinity(deltaTime) || float.IsNaN(speed))
            {
                return;
            }

            var currentX = transform.position.x;
            var targetX = trackCenterX + (lane - RunnerTrack.CenterLane) * RunnerTrack.LaneSpacing;
            var laneSpeed = RunnerTrack.LaneSpacing / RunnerTrack.LaneChangeSeconds;
            var nextX = Mathf.MoveTowards(currentX, targetX, laneSpeed * deltaTime);
            var forward = Mathf.Clamp(speed, 0f, maxSpeed) * deltaTime;
            controller.Move(new Vector3(nextX - currentX, 0f, forward));

            if (hitFatalObstacle)
                FatalCollision?.Invoke();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!running || (obstacleLayers.value & (1 << hit.collider.gameObject.layer)) == 0)
                return;

            running = false;
            hitFatalObstacle = true;
        }
    }
}
