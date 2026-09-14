using System;
using Cave.Axioms.Elemental;
using UnityEngine;

namespace Cave.Axioms.Control
{
    [Serializable]
    public struct AxiomControlReferenceProfile
    {
        public AxiomKind Kind;
        public AxiomTrajectoryReference Reference;
        public float OpportunityLifetime;
        public float MinimumErrorMagnitude;
        public float RefreshMagnitudeDelta;
        public float MinimumControlQuality;
        public float InterventionScale;
        public AxiomTemporalControlProfile Temporal;

        public static AxiomControlReferenceProfile DefaultFor(AxiomKind kind)
        {
            AxiomTrajectoryReference reference = new AxiomTrajectoryReference
            {
                EvaluateState = true,
                State = .7f,
                StateTolerance = 1f,
                EvaluateRate = true,
                Rate = 0f,
                RateTolerance = .75f,
                EvaluateAcceleration = true,
                Acceleration = 0f,
                AccelerationTolerance = 3f
            };
            return new AxiomControlReferenceProfile
            {
                Kind = kind,
                Reference = reference,
                OpportunityLifetime = 1.1f,
                MinimumErrorMagnitude = .05f,
                RefreshMagnitudeDelta = .25f,
                MinimumControlQuality = .45f,
                InterventionScale = .12f,
                Temporal = AxiomTemporalControlProfile.DefaultFor(kind)
            };
        }
    }

    public struct AxiomControlOpportunity
    {
        public AxiomControlOpportunity(
            AxiomKind kind,
            AxiomErrorState error,
            AxiomControlReferenceProfile profile,
            float createdAt)
        {
            Kind = kind;
            Error = error;
            Profile = profile;
            CreatedAt = createdAt;
            ExpiresAt = createdAt + Mathf.Max(.05f, profile.OpportunityLifetime);
        }

        public AxiomKind Kind { get; }
        public AxiomErrorState Error { get; }
        public AxiomControlReferenceProfile Profile { get; }
        public float CreatedAt { get; }
        public float ExpiresAt { get; }
        public bool IsActiveAt(float timestamp) => timestamp < ExpiresAt;
    }

    /// <summary>
    /// Actor-local, event/query-driven control layer over existing scalar
    /// trajectories. It owns at most one fresh opportunity per supported Axiom.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomControlState : MonoBehaviour
    {
        [SerializeField] private AxiomControlReferenceProfile[] profileOverrides = new AxiomControlReferenceProfile[0];

        private AxiomControlOpportunity? heat;
        private AxiomControlOpportunity? order;
        private AxiomControlOpportunity? flow;
        private AxiomControlOpportunity? mass;
        private AxiomTemporalControlChannel heatTemporal;
        private AxiomTemporalControlChannel orderTemporal;
        private AxiomTemporalControlChannel flowTemporal;
        private AxiomTemporalControlChannel massTemporal;
        private AxiomTemporalLockStage heatStage;
        private AxiomTemporalLockStage orderStage;
        private AxiomTemporalLockStage flowStage;
        private AxiomTemporalLockStage massStage;

        public float LastInterventionQuality { get; private set; }
        public event Action<AxiomKind, AxiomErrorState, float, float> InterventionSucceeded;
        /// <summary>Raised only for a new or materially changed real error opportunity.</summary>
        public event Action<AxiomKind, AxiomErrorState, bool> OpportunityOpened;
        /// <summary>Coarse transition only; presentation never determines control state.</summary>
        public event Action<AxiomKind, AxiomTemporalLockStage, AxiomTemporalLockStage> TemporalStageChanged;
        public event Action<AxiomKind> TemporalActivityStarted;

        private void Awake()
        {
            BuildTemporalChannels();
        }

        private void Update()
        {
            AdvanceTemporalAll(Time.time);
        }
        public bool TryGetActiveOpportunity(AxiomKind kind, float timestamp, out AxiomControlOpportunity opportunity)
        {
            AxiomControlOpportunity? current = GetOpportunity(kind);
            if (!current.HasValue)
            {
                opportunity = default(AxiomControlOpportunity);
                return false;
            }

            if (!current.Value.IsActiveAt(timestamp))
            {
                ClearOpportunity(kind);
                opportunity = default(AxiomControlOpportunity);
                return false;
            }

            opportunity = current.Value;
            return true;
        }

        public static AxiomControlState EnsureOn(GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            AxiomControlState state = actor.GetComponent<AxiomControlState>();
            if (state != null)
            {
                return state;
            }

            state = actor.AddComponent<AxiomControlState>();
            actor.GetComponent<Cave.Axioms.Vfx.AxiomVfxPresenter>()?.RefreshBindings();
            return state;
        }

        public void EvaluateAndRefresh(
            AxiomRuntimeState runtime,
            AxiomKind kind,
            float timestamp,
            GameObject source,
            GameObject target)
        {
            if (runtime == null || !IsSupported(kind))
            {
                return;
            }

            AxiomControlReferenceProfile profile = ResolveProfile(kind);
            AxiomTemporalControlSnapshot snapshot = AdvanceTemporal(kind, timestamp, profile);
            AxiomErrorState error = EvaluateTemporalError(kind, snapshot, profile, timestamp);
            if (!error.HasError || error.Magnitude < profile.MinimumErrorMagnitude)
            {
                return;
            }

            AxiomControlOpportunity current;
            bool hasCurrent = TryGetActiveOpportunity(kind, timestamp, out current);
            if (hasCurrent
                && current.Error.ErrorKind == error.ErrorKind
                && Abs(current.Error.Magnitude - error.Magnitude) < profile.RefreshMagnitudeDelta)
            {
                return;
            }

            bool refreshed = hasCurrent;
            SetOpportunity(kind, new AxiomControlOpportunity(kind, error, profile, timestamp));
            OpportunityOpened?.Invoke(kind, error, refreshed);
            runtime.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                refreshed ? AxiomFeedbackType.ControlOpportunityRefreshed : AxiomFeedbackType.ControlOpportunityOpened,
                error.Magnitude,
                source,
                target,
                timestamp));
        }

        /// <summary>Called only after a qualifying successful elemental application.</summary>
        public void RecordSuccessfulApplication(AxiomKind kind, float amount, float timestamp)
        {
            if (!IsSupported(kind) || amount <= 0f)
            {
                return;
            }

            AxiomControlReferenceProfile profile = ResolveProfile(kind);
            AxiomTemporalControlChannel channel = GetTemporalChannel(kind);
            if (channel == null)
            {
                return;
            }

            bool wasInactive = channel.Snapshot().Activity <= .001f;
            channel.RecordSuccessfulApplication(amount, timestamp, profile.Reference);
            if (wasInactive)
            {
                TemporalActivityStarted?.Invoke(kind);
            }
            RaiseStageIfChanged(kind, channel.Snapshot());
        }

        public bool TryGetTemporalSnapshot(AxiomKind kind, float timestamp, out AxiomTemporalControlSnapshot snapshot)
        {
            if (!IsSupported(kind))
            {
                snapshot = default(AxiomTemporalControlSnapshot);
                return false;
            }

            snapshot = AdvanceTemporal(kind, timestamp, ResolveProfile(kind));
            return true;
        }

        /// <summary>
        /// Event-time quality query used by Convergence. It advances only the four
        /// actor-local temporal channels and never introduces a polling loop.
        /// </summary>
        public bool HasStrongCurrentControl(float timestamp)
        {
            return HasStrongCurrentControl(AxiomKind.Flow, timestamp)
                || HasStrongCurrentControl(AxiomKind.Mass, timestamp)
                || LastInterventionQuality >= .8f;
        }

        private bool HasStrongCurrentControl(AxiomKind kind, float timestamp)
        {
            AxiomTemporalControlSnapshot snapshot;
            return TryGetTemporalSnapshot(kind, timestamp, out snapshot)
                && (snapshot.RateHeld || snapshot.AccelerationHeld);
        }

        public bool TryIntervene(
            AxiomRuntimeState runtime,
            AxiomDynamicsState dynamics,
            AxiomKind kind,
            float correctionDirection,
            float quality,
            float timestamp,
            GameObject source,
            GameObject target)
        {
            LastInterventionQuality = Mathf.Clamp01(quality);
            AxiomControlOpportunity opportunity;
            if (runtime == null || dynamics == null || !TryGetActiveOpportunity(kind, timestamp, out opportunity))
            {
                return false;
            }

            bool correctDirection = correctionDirection * opportunity.Error.SignedDifference < 0f;
            if (!correctDirection || LastInterventionQuality < opportunity.Profile.MinimumControlQuality)
            {
                runtime.RaiseFeedback(new AxiomFeedbackEvent(
                    kind,
                    AxiomFeedbackType.ControlInterventionFailed,
                    LastInterventionQuality,
                    source,
                    target,
                    timestamp));
                return false;
            }

            float normalizedError = Mathf.Clamp01(opportunity.Error.Magnitude / (1f + opportunity.Error.Magnitude));
            float baseCorrection = opportunity.Profile.InterventionScale
                * (.5f + normalizedError)
                * LastInterventionQuality;
            float desirableCorrection = baseCorrection;
            float counterReduction = baseCorrection * .75f;
            if (opportunity.Error.ErrorKind == AxiomErrorKind.Rate)
            {
                desirableCorrection *= .75f;
                counterReduction *= 1.1f;
            }
            else if (opportunity.Error.ErrorKind == AxiomErrorKind.Acceleration)
            {
                desirableCorrection *= .35f;
                counterReduction *= 1.45f;
            }
            AxiomDynamicResponse response;
            dynamics.ApplyControlCorrection(kind, desirableCorrection, counterReduction, timestamp, out response);
            ClearOpportunity(kind);
            runtime.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                AxiomFeedbackType.ControlInterventionSucceeded,
                desirableCorrection,
                source,
                target,
                timestamp));
            InterventionSucceeded?.Invoke(kind, opportunity.Error, LastInterventionQuality, timestamp);
            return true;
        }

        public void ResolveExpired(AxiomRuntimeState runtime, float timestamp)
        {
            ResolveExpiry(runtime, AxiomKind.Heat, timestamp);
            ResolveExpiry(runtime, AxiomKind.Order, timestamp);
            ResolveExpiry(runtime, AxiomKind.Flow, timestamp);
            ResolveExpiry(runtime, AxiomKind.Mass, timestamp);
        }

        private void ResolveExpiry(AxiomRuntimeState runtime, AxiomKind kind, float timestamp)
        {
            AxiomControlOpportunity? opportunity = GetOpportunity(kind);
            if (!opportunity.HasValue || opportunity.Value.IsActiveAt(timestamp))
            {
                return;
            }

            ClearOpportunity(kind);
            runtime?.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                AxiomFeedbackType.ControlOpportunityExpired,
                opportunity.Value.Error.Magnitude,
                gameObject,
                gameObject,
                timestamp));
        }

        private AxiomControlReferenceProfile ResolveProfile(AxiomKind kind)
        {
            if (profileOverrides != null)
            {
                for (int index = 0; index < profileOverrides.Length; index++)
                {
                    if (profileOverrides[index].Kind == kind)
                    {
                        return profileOverrides[index];
                    }
                }
            }

            return AxiomControlReferenceProfile.DefaultFor(kind);
        }

        private void BuildTemporalChannels()
        {
            heatTemporal = new AxiomTemporalControlChannel(AxiomKind.Heat, ResolveProfile(AxiomKind.Heat).Temporal);
            orderTemporal = new AxiomTemporalControlChannel(AxiomKind.Order, ResolveProfile(AxiomKind.Order).Temporal);
            flowTemporal = new AxiomTemporalControlChannel(AxiomKind.Flow, ResolveProfile(AxiomKind.Flow).Temporal);
            massTemporal = new AxiomTemporalControlChannel(AxiomKind.Mass, ResolveProfile(AxiomKind.Mass).Temporal);
        }

        private void AdvanceTemporalAll(float timestamp)
        {
            AdvanceTemporal(AxiomKind.Heat, timestamp, ResolveProfile(AxiomKind.Heat));
            AdvanceTemporal(AxiomKind.Order, timestamp, ResolveProfile(AxiomKind.Order));
            AdvanceTemporal(AxiomKind.Flow, timestamp, ResolveProfile(AxiomKind.Flow));
            AdvanceTemporal(AxiomKind.Mass, timestamp, ResolveProfile(AxiomKind.Mass));
        }

        private AxiomTemporalControlSnapshot AdvanceTemporal(AxiomKind kind, float timestamp, AxiomControlReferenceProfile profile)
        {
            AxiomTemporalControlChannel channel = GetTemporalChannel(kind);
            if (channel == null)
            {
                return default(AxiomTemporalControlSnapshot);
            }

            AxiomTemporalControlSnapshot snapshot = channel.AdvanceTo(timestamp, profile.Reference);
            RaiseStageIfChanged(kind, snapshot);
            return snapshot;
        }

        private AxiomTemporalControlChannel GetTemporalChannel(AxiomKind kind)
        {
            if (heatTemporal == null) BuildTemporalChannels();
            switch (kind)
            {
                case AxiomKind.Heat: return heatTemporal;
                case AxiomKind.Order: return orderTemporal;
                case AxiomKind.Flow: return flowTemporal;
                case AxiomKind.Mass: return massTemporal;
                default: return null;
            }
        }

        private void RaiseStageIfChanged(AxiomKind kind, AxiomTemporalControlSnapshot snapshot)
        {
            AxiomTemporalLockStage previous = GetStage(kind);
            AxiomTemporalLockStage current = snapshot.PresentationStage;
            if (previous == current) return;
            SetStage(kind, current);
            TemporalStageChanged?.Invoke(kind, current, previous);
        }

        private AxiomTemporalLockStage GetStage(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat: return heatStage;
                case AxiomKind.Order: return orderStage;
                case AxiomKind.Flow: return flowStage;
                case AxiomKind.Mass: return massStage;
                default: return AxiomTemporalLockStage.Uncontrolled;
            }
        }

        private void SetStage(AxiomKind kind, AxiomTemporalLockStage stage)
        {
            switch (kind)
            {
                case AxiomKind.Heat: heatStage = stage; break;
                case AxiomKind.Order: orderStage = stage; break;
                case AxiomKind.Flow: flowStage = stage; break;
                case AxiomKind.Mass: massStage = stage; break;
            }
        }

        private static AxiomErrorState EvaluateTemporalError(AxiomKind kind, AxiomTemporalControlSnapshot snapshot, AxiomControlReferenceProfile profile, float timestamp)
        {
            AxiomTemporalControlProfile temporal = profile.Temporal.Sanitized(kind);
            float state = snapshot.Activity - profile.Reference.State;
            float rate = snapshot.Rate - profile.Reference.Rate;
            float acceleration = snapshot.Acceleration - profile.Reference.Acceleration;
            float stateOver = Abs(state) - temporal.StateHoldTolerance;
            float rateOver = Abs(rate) - temporal.RateHoldTolerance;
            float accelerationOver = Abs(acceleration) - temporal.AccelerationHoldTolerance;
            if (accelerationOver > 0f && accelerationOver >= rateOver && accelerationOver >= stateOver)
                return new AxiomErrorState(kind, AxiomErrorKind.Acceleration, acceleration, timestamp);
            if (rateOver > 0f && rateOver >= stateOver)
                return new AxiomErrorState(kind, AxiomErrorKind.Rate, rate, timestamp);
            if (stateOver > 0f)
                return new AxiomErrorState(kind, AxiomErrorKind.State, state, timestamp);
            return new AxiomErrorState(kind, AxiomErrorKind.None, 0f, timestamp);
        }

        private AxiomControlOpportunity? GetOpportunity(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat: return heat;
                case AxiomKind.Order: return order;
                case AxiomKind.Flow: return flow;
                case AxiomKind.Mass: return mass;
                default: return null;
            }
        }

        private void SetOpportunity(AxiomKind kind, AxiomControlOpportunity value)
        {
            switch (kind)
            {
                case AxiomKind.Heat: heat = value; break;
                case AxiomKind.Order: order = value; break;
                case AxiomKind.Flow: flow = value; break;
                case AxiomKind.Mass: mass = value; break;
            }
        }

        private void ClearOpportunity(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat: heat = null; break;
                case AxiomKind.Order: order = null; break;
                case AxiomKind.Flow: flow = null; break;
                case AxiomKind.Mass: mass = null; break;
            }
        }

        private static bool IsSupported(AxiomKind kind)
        {
            return kind == AxiomKind.Heat || kind == AxiomKind.Order
                || kind == AxiomKind.Flow || kind == AxiomKind.Mass;
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
