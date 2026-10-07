using System;
using Cave.Axioms.Elemental;
using Cave.Domain;
using UnityEngine;

namespace Cave.Axioms
{
    /// <summary>
    /// Actor-local read/feedback facade over AxiomDynamicsState's authoritative
    /// continuous S/R/A channels. It owns no separate physical stack state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomRuntimeState : MonoBehaviour
    {
        [Header("Legacy Trajectory Sampling")]
        [SerializeField, Min(4)] private int historyCapacity = 64;
        [SerializeField, Min(0.01f)] private float shortWindowSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float longWindowSeconds = 1f;
        [SerializeField, Min(0.00001f)] private float minimumDeltaTime = 0.0001f;

        [Header("Qualitative Thresholds")]
        [SerializeField, Min(0f)] private float stableRateThreshold = 0.05f;
        [SerializeField, Min(0f)] private float fastRateThreshold = 1f;
        [SerializeField, Min(0f)] private float stableAccelerationThreshold = 0.05f;

        private AxiomTrajectoryState[] lastTrajectories;
        private AxiomErrorKind[] lastErrorKinds;
        private PlayerMovementTelemetry playerMovementTelemetry;
        private AxiomDynamicsState dynamics;

        public event Action<AxiomFeedbackEvent> FeedbackRaised;

        public AxiomTrajectoryThresholds Thresholds => new AxiomTrajectoryThresholds(
            stableRateThreshold,
            fastRateThreshold,
            stableAccelerationThreshold);

        private void Awake()
        {
            InitializeState();
            dynamics = AxiomDynamicsState.EnsureOn(gameObject);
            Elemental.AxiomMassStateModel.EnsureOn(gameObject);
            DomainRuntimeCarrierIdentity.EnsureOn(gameObject);
            playerMovementTelemetry = GetComponent<PlayerMovementTelemetry>();
            // Local presentation observer only; it subscribes to this actor's events
            // and performs no scene scan or gameplay mutation.
            if (GetComponent<Vfx.AxiomVfxPresenter>() == null)
            {
                gameObject.AddComponent<Vfx.AxiomVfxPresenter>();
            }
        }

        private void OnEnable()
        {
            // A component added while this authority is awakening can receive
            // its OnEnable before this component is itself live. Re-register
            // here so the explicit world bridge observes the final live state.
            DomainRuntimeCarrierIdentity identity = DomainRuntimeCarrierIdentity.EnsureOn(gameObject);
            if (identity != null) DomainRuntimeCarrierRegistry.Register(identity);
        }

        public void ApplyInput(AxiomInput input)
        {
            if (lastTrajectories == null || lastErrorKinds == null)
            {
                InitializeState();
            }

            AxiomDynamicsState activeDynamics = GetDynamics();
            if (activeDynamics == null) return;
            activeDynamics.ApplyInput(input.Kind, input.Amount, input.Timestamp);
            AxiomTrajectoryState trajectory;
            if (!activeDynamics.TryGetActualTrajectory(input.Kind, input.Timestamp, Thresholds, out trajectory)) return;
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
            return TryGetTrajectory(kind, Time.time, out trajectory);
        }

        /// <summary>Timestamp-explicit read used by deterministic event paths.</summary>
        public bool TryGetTrajectory(AxiomKind kind, float timestamp, out AxiomTrajectoryState trajectory)
        {
            if ((int)kind < 0 || (int)kind >= Enum.GetValues(typeof(AxiomKind)).Length)
            {
                trajectory = default(AxiomTrajectoryState);
                return false;
            }

            AxiomDynamicsState activeDynamics = GetDynamics();
            if (activeDynamics == null)
            {
                trajectory = default(AxiomTrajectoryState);
                return false;
            }

            return activeDynamics.TryGetActualTrajectory(kind, timestamp, Thresholds, out trajectory);
        }

        /// <summary>Domain-facing projection. Only phenomena with an established semantic adapter are exposed.</summary>
        public bool TryReadDomainSemantic(LawPhenomenon phenomenon,float timestamp,out PhenomenonSemanticSnapshot snapshot)
        {
            AxiomDomainSemanticCodecRejection rejection;
            return AxiomDomainSemanticCodec.TryProject(this,phenomenon,timestamp,out snapshot,out rejection);
        }
        /// <summary>Applies an approved semantic S value directly while preserving the live channel's R/A trajectory.</summary>
        public bool TryCommitDomainSemantic(PhenomenonSemanticSnapshot approved,float timestamp)
        {
            if(approved==null)return false;float state;AxiomDomainSemanticCodecRejection rejection;AxiomKind kind;
            return AxiomDomainSemanticCodec.TryMapPhenomenon(approved.Phenomenon,out kind)
                && AxiomDomainSemanticCodec.TryResolveCommittedState(this,approved.Phenomenon,approved.SemanticValue,out state,out rejection)
                && GetDynamics()!=null&&GetDynamics().TrySetDomainState(kind,state,timestamp);
        }

        /// <summary>Read-only S bounds for the codec; this exposes no mutable channel API.</summary>
        public bool TryGetDomainStateBounds(AxiomKind kind, out float minimum, out float maximum)
        {
            AxiomDynamicsState activeDynamics = GetDynamics();
            if (activeDynamics != null)
            {
                return activeDynamics.TryGetDomainStateBounds(kind, out minimum, out maximum);
            }

            minimum = 0f;
            maximum = 0f;
            return false;
        }

        public bool HasNaturalMassBaseline()
        {
            Elemental.AxiomMassStateModel mass = GetComponent<Elemental.AxiomMassStateModel>();
            return mass != null && mass.HasValidNaturalMassBaseline;
        }

        /// <summary>Reads a typed Pattern relationship without exposing Unity references to Domain snapshots.</summary>
        public bool TryReadPatternContext(AxiomKind kind, out string identity, out uint revision)
        {
            AxiomPatternContextState context = GetComponent<AxiomPatternContextState>();
            if (context != null)
                return context.TryRead(kind, out identity, out revision);
            identity = null;
            revision = 0;
            return false;
        }

        /// <summary>
        /// Gameplay pattern sources update only S after establishing a real
        /// relationship context. This does not use ordinary input integration
        /// and preserves the live R/A trajectory.
        /// </summary>
        public bool TrySetPatternStrength(AxiomKind kind, float strength, float timestamp)
        {
            string identity;
            uint revision;
            return (kind == AxiomKind.Resonance || kind == AxiomKind.Phase)
                && TryReadPatternContext(kind, out identity, out revision)
                && GetDynamics() != null
                && GetDynamics().TrySetDomainState(kind, strength, timestamp);
        }

        public AxiomErrorState EvaluateError(
            AxiomKind kind,
            AxiomTrajectoryReference reference,
            float timestamp,
            GameObject source = null,
            GameObject target = null)
        {
            if (lastTrajectories == null || lastErrorKinds == null)
            {
                InitializeState();
            }

            AxiomTrajectoryState trajectory;
            if (!TryGetTrajectory(kind, timestamp, out trajectory))
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

        private void InitializeState()
        {
            int count = Enum.GetValues(typeof(AxiomKind)).Length;
            lastTrajectories = new AxiomTrajectoryState[count];
            lastErrorKinds = new AxiomErrorKind[count];
        }

        private AxiomDynamicsState GetDynamics()
        {
            if (dynamics == null)
            {
                dynamics = AxiomDynamicsState.EnsureOn(gameObject);
            }

            return dynamics;
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
