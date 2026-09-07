using UnityEngine;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Actor-local lazy elemental response state. It deliberately has no Update;
    /// callers advance it when an application or gameplay query occurs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AxiomDynamicsState : MonoBehaviour
    {
        [SerializeField] private AxiomDynamicsProfileOverride[] profileOverrides = new AxiomDynamicsProfileOverride[0];

        private AxiomDynamicsChannel heat;
        private AxiomDynamicsChannel order;
        private AxiomDynamicsChannel flow;
        private AxiomDynamicsChannel mass;

        private void Awake()
        {
            BuildChannels();
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

        public void ApplySuccessfulInput(AxiomKind kind, float stack, float amount, float timestamp)
        {
            AxiomDynamicsChannel channel = GetChannel(kind);
            if (channel != null)
            {
                channel.ApplyInput(stack, amount, timestamp);
            }
        }

        public bool TryGetResponse(AxiomKind kind, float timestamp, out AxiomDynamicResponse response)
        {
            AxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                response = default(AxiomDynamicResponse);
                return false;
            }

            response = channel.AdvanceTo(timestamp);
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
            float desirableDelta,
            float counterReduction,
            float timestamp,
            out AxiomDynamicResponse response)
        {
            AxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                response = default(AxiomDynamicResponse);
                return false;
            }

            response = channel.ApplyControlCorrection(desirableDelta, counterReduction, timestamp);
            return true;
        }

        public bool ApplyCounterBurden(
            AxiomKind kind,
            float counterDelta,
            float timestamp,
            out AxiomDynamicResponse response)
        {
            AxiomDynamicsChannel channel = GetChannel(kind);
            if (channel == null)
            {
                response = default(AxiomDynamicResponse);
                return false;
            }

            response = channel.ApplyCounterBurden(counterDelta, timestamp);
            return true;
        }

        private void BuildChannels()
        {
            heat = new AxiomDynamicsChannel(AxiomKind.Heat, ResolveParameters(AxiomKind.Heat));
            order = new AxiomDynamicsChannel(AxiomKind.Order, ResolveParameters(AxiomKind.Order));
            flow = new AxiomDynamicsChannel(AxiomKind.Flow, ResolveParameters(AxiomKind.Flow));
            mass = new AxiomDynamicsChannel(AxiomKind.Mass, ResolveParameters(AxiomKind.Mass));
        }

        private AxiomDynamicsChannel GetChannel(AxiomKind kind)
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
                default: return null;
            }
        }

        private AxiomDynamicsParameters ResolveParameters(AxiomKind kind)
        {
            if (profileOverrides != null)
            {
                for (int index = 0; index < profileOverrides.Length; index++)
                {
                    AxiomDynamicsProfileOverride candidate = profileOverrides[index];
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
