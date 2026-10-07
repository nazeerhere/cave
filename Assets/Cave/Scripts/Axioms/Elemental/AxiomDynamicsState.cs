using UnityEngine;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Actor-local authoritative S/R/A owner. One Update advances all active
    /// phenomenon channels; no per-phenomenon MonoBehaviour is created.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomDynamicsState : MonoBehaviour
    {
        [SerializeField] private AxiomDynamicsProfileOverride[] profileOverrides = new AxiomDynamicsProfileOverride[0];

        private ThreeStateAxiomDynamicsChannel heat;
        private ThreeStateAxiomDynamicsChannel order;
        private ThreeStateAxiomDynamicsChannel flow;
        private ThreeStateAxiomDynamicsChannel mass;
        private ThreeStateAxiomDynamicsChannel compression;
        private ThreeStateAxiomDynamicsChannel potential;
        private ThreeStateAxiomDynamicsChannel resonance;
        private ThreeStateAxiomDynamicsChannel phase;

        private void Awake()
        {
            BuildChannels();
        }

        private void Update()
        {
            float timestamp = Time.time;
            AdvanceActive(heat, timestamp);
            AdvanceActive(order, timestamp);
            AdvanceActive(flow, timestamp);
            AdvanceActive(mass, timestamp);
            AdvanceActive(compression, timestamp);
            AdvanceActive(potential, timestamp);
            AdvanceActive(resonance, timestamp);
            AdvanceActive(phase, timestamp);
        }

        public static AxiomDynamicsState EnsureOn(GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            AxiomDynamicsState state = actor.GetComponent<AxiomDynamicsState>();
            return state != null ? state : actor.AddComponent<AxiomDynamicsState>();
        }

        public void ApplySuccessfulInput(
            AxiomKind kind,
            float stack,
            float amount,
            float timestamp,
            float counterFactor = 1f)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel != null)
            {
                // Runtime input already owns physical trajectory mutation. Keep
                // this legacy bridge entry point observational to avoid a second
                // application when older source paths still call it.
                channel.AdvanceTo(timestamp);
            }
        }

        public void ApplyInput(AxiomKind kind, float amount, float timestamp)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel != null) channel.ApplyInput(amount, timestamp);
        }

        public bool TryGetActualTrajectory(
            AxiomKind kind,
            float timestamp,
            AxiomTrajectoryThresholds thresholds,
            out AxiomTrajectoryState trajectory)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                trajectory = default(AxiomTrajectoryState);
                return false;
            }

            trajectory = channel.GetActualTrajectory(timestamp, thresholds);
            return true;
        }

        public bool TryGetResponse(AxiomKind kind, float timestamp, out AxiomDynamicResponse response)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                response = default(AxiomDynamicResponse);
                return false;
            }

            channel.AdvanceTo(timestamp);
            response = channel.Snapshot(timestamp);
            return true;
        }

        /// <summary>
        /// Returns the actor-local desired response trajectory for control and
        /// later threshold queries. It is sampled from the same channel that
        /// evolves the differential response law.
        /// </summary>
        public bool TryGetDesiredTrajectory(
            AxiomKind kind,
            float timestamp,
            AxiomTrajectoryThresholds thresholds,
            out AxiomTrajectoryState trajectory)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                trajectory = default(AxiomTrajectoryState);
                return false;
            }

            trajectory = channel.GetDesiredTrajectory(timestamp, thresholds);
            return true;
        }

        /// <summary>
        /// Returns the one control reference derived from this actor's live
        /// dynamics profile. The supplied legacy reference contributes only
        /// enable flags and unversioned-profile compatibility bands.
        /// </summary>
        public bool TryGetControlReference(
            AxiomKind kind,
            float timestamp,
            AxiomTrajectoryThresholds thresholds,
            AxiomTrajectoryReference legacy,
            out AxiomTrajectoryState desired,
            out AxiomTrajectoryReference reference)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                desired = default(AxiomTrajectoryState);
                reference = default(AxiomTrajectoryReference);
                return false;
            }

            desired = channel.GetDesiredTrajectory(timestamp, thresholds);
            reference = channel.BuildControlReference(legacy);
            return true;
        }

        public bool TryGetParameters(AxiomKind kind, out AxiomDynamicsParameters parameters)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                parameters = default(AxiomDynamicsParameters);
                return false;
            }

            parameters = channel.Parameters;
            return true;
        }

        public float GetHeatBurnPersistenceMultiplier(float timestamp)
        {
            AxiomDynamicResponse response;
            if (!TryGetResponse(AxiomKind.Heat, timestamp, out response))
            {
                return 1f;
            }

            // Deliberately modest: Thermal Load can lengthen a direct Fire refresh
            // by at most 35%; Dissipation reduces the effective response strength.
            return 1f + (.35f * response.ResponseStrength);
        }

        /// <summary>
        /// Conservative query seam for enemy locomotion/state-transition systems.
        /// Batch C deliberately does not force this into fragmented enemy AI paths.
        /// </summary>
        public float GetOrderTransitionFactor(float timestamp)
        {
            AxiomDynamicResponse response;
            if (!TryGetResponse(AxiomKind.Order, timestamp, out response))
            {
                return 1f;
            }

            return 1f - (.15f * response.ResponseStrength);
        }

        public bool ApplyControlCorrection(
            AxiomKind kind,
            AxiomErrorKind dimension,
            float requiredDirection,
            float correctionMagnitude,
            float timestamp,
            out AxiomDynamicResponse response)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                response = default(AxiomDynamicResponse);
                return false;
            }

            channel.ApplyControlCorrection(dimension, requiredDirection, correctionMagnitude, timestamp);
            response = channel.Snapshot(timestamp);
            return true;
        }

        public bool ApplyCounterBurden(
            AxiomKind kind,
            float counterDelta,
            float timestamp,
            out AxiomDynamicResponse response)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                response = default(AxiomDynamicResponse);
                return false;
            }

            channel.ApplyCounterBurden(counterDelta, timestamp);
            response = channel.Snapshot(timestamp);
            return true;
        }

        /// <summary>Typed Domain commit seam. It never routes through input pressure and never recalculates rate or acceleration.</summary>
        public bool TrySetDomainState(AxiomKind kind,float approvedState,float timestamp)
        {
            ThreeStateAxiomDynamicsChannel channel=GetChannel(kind);
            return channel!=null&&channel.TrySetStatePreservingDynamics(approvedState,timestamp);
        }

        /// <summary>Read-only bounds query used by the Domain semantic codec before an approved S write.</summary>
        public bool TryGetDomainStateBounds(AxiomKind kind, out float minimum, out float maximum)
        {
            ThreeStateAxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                minimum = 0f;
                maximum = 0f;
                return false;
            }

            AxiomDynamicsParameters parameters = channel.Parameters;
            minimum = parameters.MinimumState;
            maximum = parameters.MaximumState;
            return true;
        }

        private void BuildChannels()
        {
            heat = new ThreeStateAxiomDynamicsChannel(AxiomKind.Heat, ResolveParameters(AxiomKind.Heat));
            order = new ThreeStateAxiomDynamicsChannel(AxiomKind.Order, ResolveParameters(AxiomKind.Order));
            flow = new ThreeStateAxiomDynamicsChannel(AxiomKind.Flow, ResolveParameters(AxiomKind.Flow));
            mass = new ThreeStateAxiomDynamicsChannel(AxiomKind.Mass, ResolveParameters(AxiomKind.Mass));
            compression = new ThreeStateAxiomDynamicsChannel(AxiomKind.Compression, ResolveParameters(AxiomKind.Compression));
            potential = new ThreeStateAxiomDynamicsChannel(AxiomKind.Potential, ResolveParameters(AxiomKind.Potential));
            resonance = new ThreeStateAxiomDynamicsChannel(AxiomKind.Resonance, ResolveParameters(AxiomKind.Resonance));
            phase = new ThreeStateAxiomDynamicsChannel(AxiomKind.Phase, ResolveParameters(AxiomKind.Phase));
        }

        private ThreeStateAxiomDynamicsChannel GetChannel(AxiomKind kind)
        {
            if (heat == null)
            {
                BuildChannels();
            }

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

        private static void AdvanceActive(ThreeStateAxiomDynamicsChannel channel, float timestamp)
        {
            if (channel != null && channel.IsActive) channel.AdvanceTo(timestamp);
        }

        private AxiomDynamicsParameters ResolveParameters(AxiomKind kind)
        {
            return ResolveParameters(kind, profileOverrides);
        }

        /// <summary>Shared actor-profile resolution seam used by state and deterministic verification.</summary>
        public static AxiomDynamicsParameters ResolveParameters(
            AxiomKind kind,
            AxiomDynamicsProfileOverride[] overrides)
        {
            if (overrides != null)
            {
                for (int index = 0; index < overrides.Length; index++)
                {
                    AxiomDynamicsProfileOverride candidate = overrides[index];
                    if (candidate.Enabled && candidate.Kind == kind)
                    {
                        return candidate.Parameters.Sanitized();
                    }
                }
            }

            return AxiomDynamicsParameters.DefaultFor(kind);
        }
    }
}
