using System;
using UnityEngine;

namespace Cave.Axioms
{
    /// <summary>
    /// Actor-local owner for future Axiom channels. It has no Update loop: channels
    /// advance only when a caller supplies an input sample.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomRuntimeState : MonoBehaviour
    {
        [Header("Trajectory Sampling")]
        [SerializeField, Min(4)] private int historyCapacity = 64;
        [SerializeField, Min(0.01f)] private float shortWindowSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float longWindowSeconds = 1f;
        [SerializeField, Min(0.00001f)] private float minimumDeltaTime = 0.0001f;

        [Header("Qualitative Thresholds")]
        [SerializeField, Min(0f)] private float stableRateThreshold = 0.05f;
        [SerializeField, Min(0f)] private float fastRateThreshold = 1f;
        [SerializeField, Min(0f)] private float stableAccelerationThreshold = 0.05f;

        private AxiomObservationChannel[] channels;
        private AxiomTrajectoryState[] lastTrajectories;
        private AxiomErrorKind[] lastErrorKinds;
        private PlayerMovementTelemetry playerMovementTelemetry;

        public event Action<AxiomFeedbackEvent> FeedbackRaised;

        public AxiomTrajectoryThresholds Thresholds => new AxiomTrajectoryThresholds(
            stableRateThreshold,
            fastRateThreshold,
            stableAccelerationThreshold);

        private void Awake()
        {
            BuildChannels();
            playerMovementTelemetry = GetComponent<PlayerMovementTelemetry>();
        }

        public void ApplyInput(AxiomInput input)
        {
            AxiomObservationChannel channel = GetChannel(input.Kind);
            channel.ApplyDelta(input.Amount, input.Timestamp);
            AxiomTrajectoryState trajectory = channel.GetTrajectory(Thresholds);
            int index = (int)input.Kind;
            AxiomTrajectoryState previous = lastTrajectories[index];
            lastTrajectories[index] = trajectory;
            RaiseFeedback(new AxiomFeedbackEvent(
                input.Kind,
                AxiomFeedbackType.StateChanged,
                input.Amount < 0f ? -input.Amount : input.Amount,
                input.Source,
                input.Target,
                input.Timestamp));

            if (previous.Direction != trajectory.Direction
                || previous.RateIntensity != trajectory.RateIntensity
                || previous.Curvature != trajectory.Curvature)
            {
                RaiseFeedback(new AxiomFeedbackEvent(
                    input.Kind,
                    AxiomFeedbackType.TrajectoryChanged,
                    trajectory.HasShortRate ? Abs(trajectory.ShortRate) : 0f,
                    input.Source,
                    input.Target,
                    input.Timestamp));
            }
        }

        public void ApplyDelta(
            AxiomKind kind,
            float amount,
            GameObject source,
            GameObject target,
            float timestamp)
        {
            ApplyInput(new AxiomInput(kind, amount, source, target, timestamp));
        }

        public bool TryGetTrajectory(AxiomKind kind, out AxiomTrajectoryState trajectory)
        {
            if (channels == null || (int)kind < 0 || (int)kind >= channels.Length)
            {
                trajectory = default(AxiomTrajectoryState);
                return false;
            }

            trajectory = channels[(int)kind].GetTrajectory(Thresholds);
            return true;
        }

        public AxiomErrorState EvaluateError(
            AxiomKind kind,
            AxiomTrajectoryReference reference,
            float timestamp,
            GameObject source = null,
            GameObject target = null)
        {
            AxiomTrajectoryState trajectory;
            if (!TryGetTrajectory(kind, out trajectory))
            {
                return new AxiomErrorState(kind, AxiomErrorKind.None, 0f, timestamp);
            }

            AxiomErrorState error = AxiomErrorEvaluator.Evaluate(trajectory, reference, timestamp);
            int index = (int)kind;
            if (lastErrorKinds[index] != error.ErrorKind)
            {
                AxiomFeedbackType type = error.HasError
                    ? AxiomFeedbackType.ErrorDetected
                    : AxiomFeedbackType.ErrorCleared;
                RaiseFeedback(new AxiomFeedbackEvent(
                    kind,
                    type,
                    error.Magnitude,
                    source,
                    target,
                    timestamp));
                lastErrorKinds[index] = error.ErrorKind;
            }

            return error;
        }

        public bool TryGetPlayerMovement(out PlayerMovementObservation observation)
        {
            if (playerMovementTelemetry == null)
            {
                playerMovementTelemetry = GetComponent<PlayerMovementTelemetry>();
                if (playerMovementTelemetry == null)
                {
                    observation = default(PlayerMovementObservation);
                    return false;
                }
            }

            observation = playerMovementTelemetry.Observation;
            return true;
        }

        public void RaiseFeedback(AxiomFeedbackEvent feedback)
        {
            FeedbackRaised?.Invoke(feedback);
        }

        private void BuildChannels()
        {
            int count = Enum.GetValues(typeof(AxiomKind)).Length;
            channels = new AxiomObservationChannel[count];
            lastTrajectories = new AxiomTrajectoryState[count];
            lastErrorKinds = new AxiomErrorKind[count];
            int resolvedCapacity = Mathf.Max(4, historyCapacity);
            float resolvedShortWindow = Mathf.Max(0.01f, shortWindowSeconds);
            float resolvedLongWindow = Mathf.Max(resolvedShortWindow, longWindowSeconds);
            float resolvedMinimumDelta = Mathf.Max(0.00001f, minimumDeltaTime);
            for (int index = 0; index < count; index++)
            {
                channels[index] = new AxiomObservationChannel(
                    (AxiomKind)index,
                    resolvedCapacity,
                    resolvedShortWindow,
                    resolvedLongWindow,
                    resolvedMinimumDelta);
            }
        }

        private AxiomObservationChannel GetChannel(AxiomKind kind)
        {
            if (channels == null)
            {
                BuildChannels();
            }

            return channels[(int)kind];
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
