using System;
using Cave.Axioms.Elemental;
using Cave.Axioms.Mastery;
using Cave.Axioms.Thresholds;
using Cave.Domain;
using UnityEngine;

namespace Cave.Axioms.Control
{
    [Serializable]
    public struct AxiomControlReferenceProfile
    {
        public AxiomKind Kind;
        // Compatibility/tolerance container. Its State/Rate/Acceleration values
        // are intentionally ignored by the authoritative resolver; desired S/R/A
        // comes from AxiomDynamicsState for the current actor and phenomenon.
        public AxiomTrajectoryReference Reference;
        public float OpportunityLifetime;
        public float MinimumErrorMagnitude;
        public float RefreshMagnitudeDelta;
        public float MinimumControlQuality;
        public float InterventionScale;
        public AxiomTemporalControlProfile Temporal;
        // Selection and stability tuning only; this must never define a desired
        // State/Rate/Acceleration target or a correction direction.
        public AxiomOpportunityResolverPolicy OpportunityPolicy;

        public static AxiomControlReferenceProfile DefaultFor(AxiomKind kind)
        {
            AxiomTrajectoryReference reference = new AxiomTrajectoryReference
            {
                EvaluateState = true,
                State = 0f,
                StateTolerance = 1f,
                EvaluateRate = true,
                Rate = 0f,
                RateTolerance = .75f,
                EvaluateAcceleration = true,
                Acceleration = 0f,
                AccelerationTolerance = 3f
            };
            AxiomControlReferenceProfile result = new AxiomControlReferenceProfile
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
            result.OpportunityPolicy = AxiomOpportunityResolverPolicy.DefaultFor(result);
            return result;
        }
    }

    public struct AxiomControlOpportunity
    {
        public AxiomControlOpportunity(
            AxiomOpportunityCandidate candidate,
            AxiomControlReferenceProfile profile,
            float createdAt)
        {
            Kind = candidate.Kind;
            Error = candidate.Error;
            Profile = profile;
            Significance = candidate.Significance;
            RequiredCorrectionDirection = candidate.RequiredCorrectionDirection;
            Source = candidate.Source;
            CreatedAt = createdAt;
            LastObservedAt = candidate.ObservedAt;
            MinimumActiveUntil = createdAt + profile.OpportunityPolicy.Sanitized(profile).MinimumActiveLifetime;
        }

        public AxiomKind Kind { get; }
        public AxiomErrorState Error { get; }
        public AxiomControlReferenceProfile Profile { get; }
        public float CreatedAt { get; }
        public float LastObservedAt { get; }
        public float MinimumActiveUntil { get; }
        public float Significance { get; }
        public float RequiredCorrectionDirection { get; }
        public AxiomOpportunityCandidateSource Source { get; }

        public AxiomControlOpportunity Refresh(AxiomOpportunityCandidate candidate)
        {
            return new AxiomControlOpportunity(
                candidate,
                Profile,
                CreatedAt,
                MinimumActiveUntil);
        }

        private AxiomControlOpportunity(
            AxiomOpportunityCandidate candidate,
            AxiomControlReferenceProfile profile,
            float createdAt,
            float minimumActiveUntil)
        {
            Kind = candidate.Kind;
            Error = candidate.Error;
            Profile = profile;
            Significance = candidate.Significance;
            RequiredCorrectionDirection = candidate.RequiredCorrectionDirection;
            Source = candidate.Source;
            CreatedAt = createdAt;
            LastObservedAt = candidate.ObservedAt;
            MinimumActiveUntil = minimumActiveUntil;
        }
    }

    /// <summary>
    /// Immutable observation of a control result. It is emitted only after the
    /// established intervention path has decided success or failure.
    /// </summary>
    public struct AxiomControlInterventionOutcome
    {
        public AxiomControlInterventionOutcome(
            GameObject controlOwner,
            AxiomKind kind,
            AxiomErrorState error,
            float attemptedDirection,
            float requiredDirection,
            bool succeeded,
            long sequence,
            float timestamp)
        {
            ControlOwner = controlOwner;
            Kind = kind;
            Error = error;
            AttemptedDirection = attemptedDirection;
            RequiredDirection = requiredDirection;
            Succeeded = succeeded;
            Sequence = sequence;
            Timestamp = timestamp;
        }

        public GameObject ControlOwner { get; }
        public AxiomKind Kind { get; }
        public AxiomErrorState Error { get; }
        public float AttemptedDirection { get; }
        public float RequiredDirection { get; }
        public bool Succeeded { get; }
        public long Sequence { get; }
        public float Timestamp { get; }
    }

    /// <summary>
    /// Actor-local, event/query-driven control layer over authoritative
    /// continuous S/R/A trajectories. It owns at most one fresh opportunity
    /// per supported Axiom.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomControlState : MonoBehaviour
    {
        [SerializeField] private AxiomControlReferenceProfile[] profileOverrides = new AxiomControlReferenceProfile[0];

        private AxiomControlOpportunity? heat;
        private AxiomControlOpportunity? order;
        private AxiomControlOpportunity? flow;
        private AxiomControlOpportunity? mass;
        private AxiomControlOpportunity? compression;
        private AxiomControlOpportunity? potential;
        private AxiomControlOpportunity? resonance;
        private AxiomControlOpportunity? phase;
        private AxiomTemporalControlChannel heatTemporal;
        private AxiomTemporalControlChannel orderTemporal;
        private AxiomTemporalControlChannel flowTemporal;
        private AxiomTemporalControlChannel massTemporal;
        private AxiomTemporalControlChannel compressionTemporal;
        private AxiomTemporalControlChannel potentialTemporal;
        private AxiomTemporalControlChannel resonanceTemporal;
        private AxiomTemporalControlChannel phaseTemporal;
        private AxiomTemporalLockStage heatStage;
        private AxiomTemporalLockStage orderStage;
        private AxiomTemporalLockStage flowStage;
        private AxiomTemporalLockStage massStage;
        private AxiomTemporalLockStage compressionStage;
        private AxiomTemporalLockStage potentialStage;
        private AxiomTemporalLockStage resonanceStage;
        private AxiomTemporalLockStage phaseStage;
        private AxiomControlledEquilibriumTracker heatEquilibrium;
        private AxiomControlledEquilibriumTracker orderEquilibrium;
        private AxiomControlledEquilibriumTracker flowEquilibrium;
        private AxiomControlledEquilibriumTracker massEquilibrium;
        private AxiomControlledEquilibriumTracker compressionEquilibrium;
        private AxiomControlledEquilibriumTracker potentialEquilibrium;
        private AxiomControlledEquilibriumTracker resonanceEquilibrium;
        private AxiomControlledEquilibriumTracker phaseEquilibrium;
        private AxiomDynamicsState dynamics;
        private AxiomRuntimeState runtime;
        private AxiomPhenomenonThresholdState thresholdState;
        private long interventionResultSequence;

        public float LastInterventionQuality { get; private set; }
        public event Action<AxiomKind, AxiomErrorState, float, float> InterventionSucceeded;
        public event Action<AxiomControlInterventionOutcome> InterventionResolved;
        /// <summary>Raised only for a new or materially changed real error opportunity.</summary>
        public event Action<AxiomKind, AxiomErrorState, bool> OpportunityOpened;
        /// <summary>Coarse transition only; presentation never determines control state.</summary>
        public event Action<AxiomKind, AxiomTemporalLockStage, AxiomTemporalLockStage> TemporalStageChanged;
        public event Action<AxiomKind> TemporalActivityStarted;

        private void Awake()
        {
            dynamics = GetComponent<AxiomDynamicsState>();
            runtime = GetComponent<AxiomRuntimeState>();
            thresholdState = AxiomPhenomenonThresholdState.EnsureOn(gameObject);
            BuildTemporalChannels();
            BuildEquilibriumTrackers();
        }

        private void Update()
        {
            AxiomRuntimeState activeRuntime = GetRuntime();
            AxiomDynamicsState activeDynamics = GetDynamics();
            if (activeRuntime == null || activeDynamics == null)
            {
                AdvanceTemporalAll(Time.time, activeDynamics);
                return;
            }

            // Continuous evaluation is the natural-resolution path. It does not
            // create a competing controller; it simply re-evaluates the same
            // actor-local trajectories owned by the existing dynamics channel.
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Heat,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Order,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Flow,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Mass,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Compression,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Potential,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Resonance,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
            EvaluateAndRefresh(activeRuntime, activeDynamics, AxiomKind.Phase,
                AxiomOpportunityCandidateSource.TrajectoryDeviation, Time.time, gameObject, gameObject);
        }
        public bool TryGetActiveOpportunity(AxiomKind kind, float timestamp, out AxiomControlOpportunity opportunity)
        {
            AxiomControlOpportunity? current = GetOpportunity(kind);
            opportunity = current.GetValueOrDefault();
            return current.HasValue;
        }

        /// <summary>Observation-only controlled-equilibrium state for one phenomenon.</summary>
        public bool IsControlledEquilibrium(AxiomKind kind)
        {
            AxiomControlledEquilibriumTracker tracker = GetEquilibriumTracker(kind);
            return tracker != null && tracker.IsControlled;
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
            AxiomDynamicsState dynamicsState,
            AxiomKind kind,
            AxiomOpportunityCandidateSource candidateSource,
            float timestamp,
            GameObject source,
            GameObject target)
        {
            if (runtime == null || dynamicsState == null || !IsSupported(kind))
            {
                return;
            }

            AxiomControlReferenceProfile profile = ResolveProfile(kind);
            AdvanceTemporal(kind, timestamp, dynamicsState, profile);
            AxiomControlOpportunity current;
            bool hasCurrent = TryGetActiveOpportunity(kind, timestamp, out current);
            AxiomTrajectoryState actual;
            AxiomTrajectoryState desired;
            AxiomTrajectoryReference reference;
            if (!runtime.TryGetTrajectory(kind, timestamp, out actual)
                || !dynamicsState.TryGetControlReference(
                    kind, timestamp, runtime.Thresholds, profile.Reference, out desired, out reference))
            {
                if (hasCurrent)
                {
                    ClearOpportunity(kind);
                    runtime.RaiseFeedback(new AxiomFeedbackEvent(
                        kind,
                        AxiomFeedbackType.ControlOpportunityExpired,
                        current.Error.Magnitude,
                        source,
                        target,
                        timestamp));
                }
                return;
            }

            profile.Reference = reference;
            UpdateControlledEquilibrium(kind, actual, reference, dynamicsState, timestamp);

            AxiomOpportunityResolution resolution = AxiomOpportunityResolver.Resolve(
                actual,
                desired,
                profile,
                hasCurrent,
                current,
                candidateSource == AxiomOpportunityCandidateSource.PlayerDisturbance,
                EvaluateThreshold(kind, actual, timestamp),
                timestamp);

            if (resolution.Disposition == AxiomOpportunityResolutionDisposition.None)
            {
                return;
            }

            if (resolution.Disposition == AxiomOpportunityResolutionDisposition.Resolved)
            {
                ClearOpportunity(kind);
                runtime.RaiseFeedback(new AxiomFeedbackEvent(
                    kind,
                    AxiomFeedbackType.ControlOpportunityExpired,
                    current.Error.Magnitude,
                    source,
                    target,
                    timestamp));
                return;
            }

            AxiomControlOpportunity next = hasCurrent
                ? current.Refresh(resolution.Candidate)
                : new AxiomControlOpportunity(resolution.Candidate, profile, timestamp);
            SetOpportunity(kind, next);
            if (!resolution.Notify)
            {
                return;
            }

            bool refreshed = hasCurrent;
            OpportunityOpened?.Invoke(kind, next.Error, refreshed);
            runtime.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                refreshed ? AxiomFeedbackType.ControlOpportunityRefreshed : AxiomFeedbackType.ControlOpportunityOpened,
                next.Error.Magnitude,
                source,
                target,
                timestamp));
        }

        /// <summary>Called only after a qualifying successful elemental application.</summary>
        public void RecordSuccessfulApplication(
            AxiomDynamicsState dynamicsState,
            AxiomKind kind,
            float amount,
            float timestamp)
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
            channel.RecordSuccessfulApplication(
                amount,
                timestamp,
                BuildAuthoritativeReference(dynamicsState, kind, profile, timestamp));
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

            snapshot = AdvanceTemporal(kind, timestamp, GetDynamics(), ResolveProfile(kind));
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
            GameObject target,
            LawExpression? masteryExpression = null)
        {
            LastInterventionQuality = Mathf.Clamp01(quality);
            AxiomControlOpportunity opportunity;
            if (runtime == null || dynamics == null || !TryGetActiveOpportunity(kind, timestamp, out opportunity))
            {
                return false;
            }

            bool correctDirection = AxiomDynamicsControlResolver.IsCorrective(correctionDirection, opportunity.Error);
            if (!correctDirection || LastInterventionQuality < opportunity.Profile.MinimumControlQuality)
            {
                runtime.RaiseFeedback(new AxiomFeedbackEvent(
                    kind,
                    AxiomFeedbackType.ControlInterventionFailed,
                    LastInterventionQuality,
                    source,
                    target,
                    timestamp));
                RaiseInterventionOutcome(opportunity, correctionDirection, false, timestamp);
                return false;
            }

            float normalizedError = Mathf.Clamp01(opportunity.Error.Magnitude / (1f + opportunity.Error.Magnitude));
            float baseCorrection = opportunity.Profile.InterventionScale
                * (.5f + normalizedError)
                * LastInterventionQuality;
            float desirableCorrection = baseCorrection;
            if (opportunity.Error.ErrorKind == AxiomErrorKind.Rate)
            {
                desirableCorrection *= .75f;
            }
            else if (opportunity.Error.ErrorKind == AxiomErrorKind.Acceleration)
            {
                desirableCorrection *= .35f;
            }
            AxiomDynamicResponse response;
            dynamics.ApplyControlCorrection(
                kind,
                opportunity.Error.ErrorKind,
                opportunity.RequiredCorrectionDirection,
                desirableCorrection,
                timestamp,
                out response);
            AxiomTrajectoryState actual;
            AxiomTrajectoryReference reference = BuildAuthoritativeReference(
                dynamics, kind, opportunity.Profile, timestamp);
            bool enteredTolerance = runtime.TryGetTrajectory(kind, timestamp, out actual)
                && AxiomControlBandEvaluator.IsDimensionControlled(
                    actual, reference, opportunity.Error.ErrorKind);
            if (!enteredTolerance)
            {
                // A directionally valid intervention that does not enter its
                // band is not a success and grants no evidence. The live
                // opportunity remains available for a later legitimate episode.
                EvaluateAndRefresh(runtime, dynamics, kind,
                    AxiomOpportunityCandidateSource.PlayerDisturbance,
                    timestamp, source, target);
                runtime.RaiseFeedback(new AxiomFeedbackEvent(
                    kind,
                    AxiomFeedbackType.ControlInterventionFailed,
                    desirableCorrection,
                    source,
                    target,
                    timestamp));
                RaiseInterventionOutcome(opportunity, correctionDirection, false, timestamp);
                return false;
            }

            // Clearing before submission makes this opportunity single-use. A
            // repeated callback cannot pass the active-opportunity gate above.
            ClearOpportunity(kind);
            AxiomControlMasteryEvidenceBridge.TrySubmitSuccessfulControl(
                source, kind, opportunity.Error, LastInterventionQuality, masteryExpression);
            runtime.RaiseFeedback(new AxiomFeedbackEvent(
                kind,
                AxiomFeedbackType.ControlInterventionSucceeded,
                desirableCorrection,
                source,
                target,
                timestamp));
            InterventionSucceeded?.Invoke(kind, opportunity.Error, LastInterventionQuality, timestamp);
            RaiseInterventionOutcome(opportunity, correctionDirection, true, timestamp);
            return true;
        }

        private void RaiseInterventionOutcome(
            AxiomControlOpportunity opportunity,
            float attemptedDirection,
            bool succeeded,
            float timestamp)
        {
            InterventionResolved?.Invoke(new AxiomControlInterventionOutcome(
                gameObject,
                opportunity.Kind,
                opportunity.Error,
                attemptedDirection,
                opportunity.RequiredCorrectionDirection,
                succeeded,
                ++interventionResultSequence,
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
            compressionTemporal = new AxiomTemporalControlChannel(AxiomKind.Compression, ResolveProfile(AxiomKind.Compression).Temporal);
            potentialTemporal = new AxiomTemporalControlChannel(AxiomKind.Potential, ResolveProfile(AxiomKind.Potential).Temporal);
            resonanceTemporal = new AxiomTemporalControlChannel(AxiomKind.Resonance, ResolveProfile(AxiomKind.Resonance).Temporal);
            phaseTemporal = new AxiomTemporalControlChannel(AxiomKind.Phase, ResolveProfile(AxiomKind.Phase).Temporal);
        }

        private void BuildEquilibriumTrackers()
        {
            heatEquilibrium = new AxiomControlledEquilibriumTracker();
            orderEquilibrium = new AxiomControlledEquilibriumTracker();
            flowEquilibrium = new AxiomControlledEquilibriumTracker();
            massEquilibrium = new AxiomControlledEquilibriumTracker();
            compressionEquilibrium = new AxiomControlledEquilibriumTracker();
            potentialEquilibrium = new AxiomControlledEquilibriumTracker();
            resonanceEquilibrium = new AxiomControlledEquilibriumTracker();
            phaseEquilibrium = new AxiomControlledEquilibriumTracker();
        }

        private void AdvanceTemporalAll(float timestamp, AxiomDynamicsState dynamicsState)
        {
            AdvanceTemporal(AxiomKind.Heat, timestamp, dynamicsState, ResolveProfile(AxiomKind.Heat));
            AdvanceTemporal(AxiomKind.Order, timestamp, dynamicsState, ResolveProfile(AxiomKind.Order));
            AdvanceTemporal(AxiomKind.Flow, timestamp, dynamicsState, ResolveProfile(AxiomKind.Flow));
            AdvanceTemporal(AxiomKind.Mass, timestamp, dynamicsState, ResolveProfile(AxiomKind.Mass));
            AdvanceTemporal(AxiomKind.Compression, timestamp, dynamicsState, ResolveProfile(AxiomKind.Compression));
            AdvanceTemporal(AxiomKind.Potential, timestamp, dynamicsState, ResolveProfile(AxiomKind.Potential));
            AdvanceTemporal(AxiomKind.Resonance, timestamp, dynamicsState, ResolveProfile(AxiomKind.Resonance));
            AdvanceTemporal(AxiomKind.Phase, timestamp, dynamicsState, ResolveProfile(AxiomKind.Phase));
        }

        private AxiomTemporalControlSnapshot AdvanceTemporal(
            AxiomKind kind,
            float timestamp,
            AxiomDynamicsState dynamicsState,
            AxiomControlReferenceProfile profile)
        {
            AxiomTemporalControlChannel channel = GetTemporalChannel(kind);
            if (channel == null)
            {
                return default(AxiomTemporalControlSnapshot);
            }

            AxiomTemporalControlSnapshot snapshot = channel.AdvanceTo(
                timestamp,
                BuildAuthoritativeReference(dynamicsState, kind, profile, timestamp));
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
                case AxiomKind.Compression: return compressionTemporal;
                case AxiomKind.Potential: return potentialTemporal;
                case AxiomKind.Resonance: return resonanceTemporal;
                case AxiomKind.Phase: return phaseTemporal;
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
                case AxiomKind.Compression: return compressionStage;
                case AxiomKind.Potential: return potentialStage;
                case AxiomKind.Resonance: return resonanceStage;
                case AxiomKind.Phase: return phaseStage;
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
                case AxiomKind.Compression: compressionStage = stage; break;
                case AxiomKind.Potential: potentialStage = stage; break;
                case AxiomKind.Resonance: resonanceStage = stage; break;
                case AxiomKind.Phase: phaseStage = stage; break;
            }
        }

        private AxiomDynamicsState GetDynamics()
        {
            if (dynamics == null)
            {
                dynamics = GetComponent<AxiomDynamicsState>();
            }

            return dynamics;
        }

        private AxiomRuntimeState GetRuntime()
        {
            if (runtime == null)
            {
                runtime = GetComponent<AxiomRuntimeState>();
            }

            return runtime;
        }

        private AxiomThresholdOpportunityContext EvaluateThreshold(
            AxiomKind kind, AxiomTrajectoryState actual, float timestamp)
        {
            if (thresholdState == null)
            {
                thresholdState = AxiomPhenomenonThresholdState.EnsureOn(gameObject);
            }

            AxiomThresholdOpportunityContext context = default(AxiomThresholdOpportunityContext);
            if (thresholdState != null)
            {
                thresholdState.Evaluate(kind, actual, timestamp, out context);
            }
            return context;
        }

        private static AxiomTrajectoryReference BuildAuthoritativeReference(
            AxiomDynamicsState dynamicsState,
            AxiomKind kind,
            AxiomControlReferenceProfile profile,
            float timestamp)
        {
            AxiomTrajectoryState desired;
            AxiomTrajectoryReference reference;
            if (dynamicsState == null
                || !dynamicsState.TryGetControlReference(
                    kind,
                    timestamp,
                    AxiomTrajectoryThresholds.Default,
                    profile.Reference,
                    out desired,
                    out reference))
            {
                return default(AxiomTrajectoryReference);
            }

            return reference;
        }

        private void UpdateControlledEquilibrium(
            AxiomKind kind,
            AxiomTrajectoryState actual,
            AxiomTrajectoryReference reference,
            AxiomDynamicsState dynamicsState,
            float timestamp)
        {
            AxiomControlledEquilibriumTracker tracker = GetEquilibriumTracker(kind);
            if (tracker == null) return;
            AxiomDynamicsParameters parameters;
            float dwell = dynamicsState != null && dynamicsState.TryGetParameters(kind, out parameters)
                ? parameters.ControlledEquilibriumDwellSeconds : 0f;
            tracker.Advance(AxiomControlBandEvaluator.Evaluate(actual, reference), dwell, timestamp);
        }

        private AxiomControlledEquilibriumTracker GetEquilibriumTracker(AxiomKind kind)
        {
            if (heatEquilibrium == null) BuildEquilibriumTrackers();
            switch (kind)
            {
                case AxiomKind.Heat: return heatEquilibrium;
                case AxiomKind.Order: return orderEquilibrium;
                case AxiomKind.Flow: return flowEquilibrium;
                case AxiomKind.Mass: return massEquilibrium;
                case AxiomKind.Compression: return compressionEquilibrium;
                case AxiomKind.Potential: return potentialEquilibrium;
                case AxiomKind.Resonance: return resonanceEquilibrium;
                case AxiomKind.Phase: return phaseEquilibrium;
                default: return null;
            }
        }

        private AxiomControlOpportunity? GetOpportunity(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat: return heat;
                case AxiomKind.Order: return order;
                case AxiomKind.Flow: return flow;
                case AxiomKind.Mass: return mass;
                case AxiomKind.Compression: return compression;
                case AxiomKind.Potential: return potential;
                case AxiomKind.Resonance: return resonance;
                case AxiomKind.Phase: return phase;
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
                case AxiomKind.Compression: compression = value; break;
                case AxiomKind.Potential: potential = value; break;
                case AxiomKind.Resonance: resonance = value; break;
                case AxiomKind.Phase: phase = value; break;
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
                case AxiomKind.Compression: compression = null; break;
                case AxiomKind.Potential: potential = null; break;
                case AxiomKind.Resonance: resonance = null; break;
                case AxiomKind.Phase: phase = null; break;
            }
        }

        private static bool IsSupported(AxiomKind kind)
        {
            return kind == AxiomKind.Heat || kind == AxiomKind.Order || kind == AxiomKind.Flow || kind == AxiomKind.Mass
                || kind == AxiomKind.Compression || kind == AxiomKind.Potential || kind == AxiomKind.Resonance || kind == AxiomKind.Phase;
        }

    }
}
