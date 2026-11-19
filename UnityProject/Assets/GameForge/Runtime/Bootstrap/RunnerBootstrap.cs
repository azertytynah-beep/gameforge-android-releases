using System;
using UnityEngine;

namespace GameForge.Runtime
{
    public sealed class RunnerBootstrap : MonoBehaviour
    {
        [SerializeField] private RunnerSettings settings;
        [SerializeField] private RunnerCatalog catalog;
        [SerializeField] private RunnerMotor motor;
        [SerializeField] private RunnerCameraFollow cameraFollow;
        [SerializeField] private RunnerSession session;
        [SerializeField] private SegmentStream stream;
        [SerializeField] private RunnerHud hud;

        private Vector3 startPosition;

        private void Start()
        {
            if (!catalog.TryGetTheme(settings.ThemeId, out var theme))
                throw new InvalidOperationException($"Thème Runner inconnu : {settings.ThemeId}.");

            startPosition = motor.transform.position;
            motor.Configure(settings);
            cameraFollow.Bind(motor.transform);
            stream.Configure(theme.Segments, catalog.Obstacles);
            ResetRun();

            session.Configure(settings);
            hud.Bind(session);
            hud.DirectionRequested += motor.RequestLane;
            motor.FatalCollision += OnFatalCollision;
            session.ResetRequested += ResetRun;
            session.FeedbackChanged += ApplyState;
        }

        private void Update()
        {
            if (session.Feedback.State != RunnerState.Running)
                return;

            float seconds = Time.deltaTime;
            session.Advance(seconds);
            motor.Step(session.Feedback.Speed, seconds);
            stream.AdvanceTo(motor.transform.position.z);
        }

        private void ResetRun()
        {
            motor.ResetPosition(startPosition);
            stream.Reset(settings.Seed, settings);
            Physics.SyncTransforms();
        }

        private void ApplyState(RunnerFeedback feedback) => motor.SetRunning(feedback.State == RunnerState.Running);

        private void OnFatalCollision() => session.ReportCollision();

        private void OnDestroy()
        {
            if (hud != null && motor != null)
                hud.DirectionRequested -= motor.RequestLane;
            if (motor != null)
                motor.FatalCollision -= OnFatalCollision;
            if (session != null)
            {
                session.ResetRequested -= ResetRun;
                session.FeedbackChanged -= ApplyState;
            }
        }
    }
}
