using System;
using Cave.Axioms.Control;
using UnityEngine;

namespace Cave.Axioms.Thresholds
{
    public enum AxiomThresholdIdentity { Primary, Secondary, Tertiary }
    public enum AxiomThresholdCrossingDirection { None, Rising, Falling }
    public enum AxiomThresholdRegion { Unconfigured, Below, Above }
    public enum AxiomThresholdManifestation { None, Burn, Chill }

    /// <summary>Typed, actor-configurable landmark in one continuous phenomenon.</summary>
    [Serializable]
    public struct AxiomPhenomenonThresholdDefinition
    {
        public bool Enabled;
        public AxiomKind Kind;
        public AxiomThresholdIdentity Identity;
        public float Boundary;
        [Min(0f)] public float ProximityDistance;
        [Min(0f)] public float MinimumApproachRate;
        [Min(0f)] public float Hysteresis;
        [Min(1f)] public float SignificanceMultiplier;
        public AxiomThresholdManifestation AboveManifestation;

        public bool IsConfigured => Enabled && Finite(Boundary)
            && Finite(ProximityDistance) && ProximityDistance >= 0f
            && Finite(MinimumApproachRate) && MinimumApproachRate >= 0f
            && Finite(Hysteresis) && Hysteresis >= 0f;

        public float ResolvedSignificanceMultiplier
        {
            get
            {
                if (!Finite(SignificanceMultiplier) || SignificanceMultiplier < 1f) return 1f;
                return SignificanceMultiplier > 4f ? 4f : SignificanceMultiplier;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public struct AxiomThresholdSnapshot
    {
        public AxiomThresholdSnapshot(AxiomPhenomenonThresholdDefinition definition, AxiomThresholdRegion region,
            bool isApproaching, AxiomThresholdCrossingDirection crossing, float observedAt)
        {
            Definition = definition;
            Region = region;
            IsApproaching = isApproaching;
            LatestCrossing = crossing;
            ObservedAt = observedAt;
        }
        public AxiomPhenomenonThresholdDefinition Definition { get; }
        public AxiomThresholdRegion Region { get; }
        public bool IsApproaching { get; }
        public AxiomThresholdCrossingDirection LatestCrossing { get; }
        public float ObservedAt { get; }
    }

    public struct AxiomThresholdOpportunityContext
    {
        public AxiomThresholdOpportunityContext(AxiomOpportunityCandidateSource source, float multiplier)
        {
            Source = source;
            SignificanceMultiplier = multiplier;
        }
        public AxiomOpportunityCandidateSource Source { get; }
        public float SignificanceMultiplier { get; }
        public bool IsActive => Source == AxiomOpportunityCandidateSource.ThresholdProximity
            || Source == AxiomOpportunityCandidateSource.ThresholdCrossing;
    }

    public struct AxiomThresholdManifestationRequest
    {
        public AxiomThresholdManifestationRequest(AxiomKind kind, AxiomThresholdIdentity identity,
            AxiomThresholdManifestation manifestation, bool active, float timestamp)
        {
            Kind = kind; Identity = identity; Manifestation = manifestation; Active = active; Timestamp = timestamp;
        }
        public AxiomKind Kind { get; }
        public AxiomThresholdIdentity Identity { get; }
        public AxiomThresholdManifestation Manifestation { get; }
        public bool Active { get; }
        public float Timestamp { get; }
    }

    /// <summary>Bounded state for one threshold; it observes an existing trajectory and never simulates one.</summary>
    public sealed class AxiomPhenomenonThresholdEvaluator
    {
        private readonly AxiomPhenomenonThresholdDefinition definition;
        private bool initialized;
        private AxiomThresholdRegion region;
        private AxiomThresholdManifestation activeManifestation;

        public AxiomPhenomenonThresholdEvaluator(AxiomPhenomenonThresholdDefinition definition) { this.definition = definition; }

        public AxiomThresholdSnapshot Evaluate(AxiomTrajectoryState trajectory, float timestamp,
            out AxiomThresholdOpportunityContext opportunity, out AxiomThresholdManifestationRequest manifestation)
        {
            opportunity = default(AxiomThresholdOpportunityContext);
            manifestation = default(AxiomThresholdManifestationRequest);
            if (!definition.IsConfigured || trajectory.Kind != definition.Kind || !Finite(trajectory.CurrentValue))
                return new AxiomThresholdSnapshot(definition, AxiomThresholdRegion.Unconfigured, false, AxiomThresholdCrossingDirection.None, timestamp);

            AxiomThresholdRegion previous = region;
            if (!initialized)
            {
                initialized = true;
                region = trajectory.CurrentValue >= definition.Boundary ? AxiomThresholdRegion.Above : AxiomThresholdRegion.Below;
            }
            else if (region == AxiomThresholdRegion.Below && trajectory.CurrentValue >= definition.Boundary + definition.Hysteresis)
                region = AxiomThresholdRegion.Above;
            else if (region == AxiomThresholdRegion.Above && trajectory.CurrentValue <= definition.Boundary - definition.Hysteresis)
                region = AxiomThresholdRegion.Below;

            AxiomThresholdCrossingDirection crossing = previous == AxiomThresholdRegion.Below && region == AxiomThresholdRegion.Above
                ? AxiomThresholdCrossingDirection.Rising
                : previous == AxiomThresholdRegion.Above && region == AxiomThresholdRegion.Below
                    ? AxiomThresholdCrossingDirection.Falling : AxiomThresholdCrossingDirection.None;
            bool approaching = IsApproaching(trajectory);
            if (crossing != AxiomThresholdCrossingDirection.None)
                opportunity = new AxiomThresholdOpportunityContext(AxiomOpportunityCandidateSource.ThresholdCrossing, definition.ResolvedSignificanceMultiplier);
            else if (approaching)
                opportunity = new AxiomThresholdOpportunityContext(AxiomOpportunityCandidateSource.ThresholdProximity, definition.ResolvedSignificanceMultiplier);

            AxiomThresholdManifestation next = region == AxiomThresholdRegion.Above ? definition.AboveManifestation : AxiomThresholdManifestation.None;
            if (next != activeManifestation)
            {
                manifestation = new AxiomThresholdManifestationRequest(definition.Kind, definition.Identity, activeManifestation, false, timestamp);
                activeManifestation = next;
                // The caller receives one change request at a time. A non-None next state is the activation request.
                if (next != AxiomThresholdManifestation.None)
                    manifestation = new AxiomThresholdManifestationRequest(definition.Kind, definition.Identity, next, true, timestamp);
            }
            return new AxiomThresholdSnapshot(definition, region, approaching, crossing, timestamp);
        }

        private bool IsApproaching(AxiomTrajectoryState trajectory)
        {
            if (!trajectory.HasShortRate || definition.ProximityDistance <= 0f) return false;
            float distance = Abs(trajectory.CurrentValue - definition.Boundary);
            if (distance > definition.ProximityDistance) return false;
            float rate = trajectory.ShortRate;
            if (Abs(rate) < definition.MinimumApproachRate) return false;
            return trajectory.CurrentValue < definition.Boundary ? rate > 0f : rate < 0f;
        }
        private static float Abs(float value) => value < 0f ? -value : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Actor-local threshold owner. Definitions default empty: production Heat has no invented threshold values.</summary>
    [DisallowMultipleComponent]
    public sealed class AxiomPhenomenonThresholdState : MonoBehaviour
    {
        [SerializeField] private AxiomPhenomenonThresholdDefinition[] definitions = new AxiomPhenomenonThresholdDefinition[0];
        private AxiomPhenomenonThresholdEvaluator[] evaluators;
        private AxiomThresholdSnapshot[] snapshots;
        public event Action<AxiomThresholdManifestationRequest> ManifestationRequested;

        private void Awake() { Build(); }
        public static AxiomPhenomenonThresholdState EnsureOn(GameObject actor)
        {
            if (actor == null) return null;
            AxiomPhenomenonThresholdState state = actor.GetComponent<AxiomPhenomenonThresholdState>();
            return state != null ? state : actor.AddComponent<AxiomPhenomenonThresholdState>();
        }
        public bool Evaluate(AxiomKind kind, AxiomTrajectoryState trajectory, float timestamp, out AxiomThresholdOpportunityContext opportunity)
        {
            if (evaluators == null) Build();
            opportunity = default(AxiomThresholdOpportunityContext);
            bool found = false;
            for (int index = 0; index < evaluators.Length; index++)
            {
                AxiomPhenomenonThresholdDefinition definition = definitions[index];
                if (definition.Kind != kind) continue;
                AxiomThresholdManifestationRequest request;
                AxiomThresholdOpportunityContext candidate;
                snapshots[index] = evaluators[index].Evaluate(trajectory, timestamp, out candidate, out request);
                if (request.Manifestation != AxiomThresholdManifestation.None)
                    ManifestationRequested?.Invoke(request);
                if (candidate.IsActive && (!opportunity.IsActive || candidate.Source == AxiomOpportunityCandidateSource.ThresholdCrossing)) opportunity = candidate;
                found = true;
            }
            return found;
        }
        public bool TryGetSnapshot(AxiomKind kind, AxiomThresholdIdentity identity, out AxiomThresholdSnapshot snapshot)
        {
            if (snapshots != null) for (int index = 0; index < snapshots.Length; index++)
                if (definitions[index].Kind == kind && definitions[index].Identity == identity) { snapshot = snapshots[index]; return true; }
            snapshot = default(AxiomThresholdSnapshot); return false;
        }
        private void Build()
        {
            int count = definitions != null ? definitions.Length : 0;
            evaluators = new AxiomPhenomenonThresholdEvaluator[count]; snapshots = new AxiomThresholdSnapshot[count];
            for (int index = 0; index < count; index++) evaluators[index] = new AxiomPhenomenonThresholdEvaluator(definitions[index]);
        }
    }
}
