using System;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Centralized, normalized coefficients for the shared elemental response law.
    /// Values are internal numerical safeguards, not player-facing stack caps.
    /// </summary>
    [Serializable]
    public struct AxiomDynamicsParameters
    {
        public float InputGain;
        public float StackGain;
        public float CounterCoupling;
        public float CounterGain;
        public float CounterDecay;
        public float InputPulseDuration;
        public float MaximumResponse;
        public float MaximumAdvanceSeconds;
        public float MaximumSubstepSeconds;
        public int MaximumSubsteps;

        public AxiomDynamicsParameters(
            float inputGain,
            float stackGain,
            float counterCoupling,
            float counterGain,
            float counterDecay,
            float inputPulseDuration,
            float maximumResponse,
            float maximumAdvanceSeconds,
            float maximumSubstepSeconds,
            int maximumSubsteps)
        {
            InputGain = inputGain;
            StackGain = stackGain;
            CounterCoupling = counterCoupling;
            CounterGain = counterGain;
            CounterDecay = counterDecay;
            InputPulseDuration = inputPulseDuration;
            MaximumResponse = maximumResponse;
            MaximumAdvanceSeconds = maximumAdvanceSeconds;
            MaximumSubstepSeconds = maximumSubstepSeconds;
            MaximumSubsteps = maximumSubsteps;
        }

        public static AxiomDynamicsParameters DefaultFor(AxiomKind kind)
        {
            switch (kind)
            {
                case AxiomKind.Heat:
                    return new AxiomDynamicsParameters(.24f, .055f, .18f, .40f, .26f, .45f, 1f, 20f, .05f, 128);
                case AxiomKind.Order:
                    return new AxiomDynamicsParameters(.19f, .048f, .16f, .35f, .23f, .45f, 1f, 20f, .05f, 128);
                case AxiomKind.Flow:
                    return new AxiomDynamicsParameters(.25f, .040f, .14f, .30f, .25f, .40f, 1f, 20f, .05f, 128);
                case AxiomKind.Mass:
                    return new AxiomDynamicsParameters(.21f, .052f, .15f, .33f, .20f, .45f, 1f, 20f, .05f, 128);
                default:
                    return new AxiomDynamicsParameters(.20f, .050f, .16f, .32f, .24f, .45f, 1f, 20f, .05f, 128);
            }
        }

        public AxiomDynamicsParameters Sanitized()
        {
            AxiomDynamicsParameters result = this;
            result.InputGain = Positive(result.InputGain, .20f);
            result.StackGain = Positive(result.StackGain, .05f);
            result.CounterCoupling = Positive(result.CounterCoupling, .16f);
            result.CounterGain = Positive(result.CounterGain, .32f);
            result.CounterDecay = Positive(result.CounterDecay, .24f);
            result.InputPulseDuration = Positive(result.InputPulseDuration, .45f);
            result.MaximumResponse = Positive(result.MaximumResponse, 1f);
            result.MaximumAdvanceSeconds = Positive(result.MaximumAdvanceSeconds, 20f);
            result.MaximumSubstepSeconds = Positive(result.MaximumSubstepSeconds, .05f);
            result.MaximumSubsteps = result.MaximumSubsteps < 1 ? 1 : result.MaximumSubsteps;
            return result;
        }

        private static float Positive(float value, float fallback)
        {
            return value > 0f && IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>Optional actor-local coefficient seam; no archetype table is populated in Batch B.</summary>
    [Serializable]
    public struct AxiomDynamicsProfileOverride
    {
        public bool Enabled;
        public AxiomKind Kind;
        public AxiomDynamicsParameters Parameters;
    }
}
