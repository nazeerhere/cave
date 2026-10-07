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

        // Three-state model fields are appended so legacy serialized response
        // profiles retain their data and receive deterministic zero-equilibrium
        // defaults instead of being reinterpreted as new coefficients.
        public int ThreeStateModelVersion;
        public float DesiredState;
        public float DesiredRate;
        public float DesiredAcceleration;
        public float ThreeStateInputGain;
        public float StateRestoration;
        public float RateDamping;
        public float AccelerationDamping;
        public float MinimumState;
        public float MaximumState;
        public float MaximumAbsoluteRate;
        public float MaximumAbsoluteAcceleration;
        public float ActiveEpsilon;

        // Versioned control semantics are appended after the already-shipped
        // three-state fields. A zero version deliberately keeps older serialized
        // profile overrides on their existing control-reference tolerances.
        public int ControlSemanticsVersion;
        public bool EnforceMinimumDesiredState;
        public float MinimumDesiredState;
        public float StateTolerance;
        public float RateTolerance;
        public float AccelerationTolerance;
        public float ControlledEquilibriumDwellSeconds;

        // Explicit assignments keep the legacy constructor serialization-safe
        // after the S/R/A fields were appended.
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
            ThreeStateModelVersion = 0;
            DesiredState = 0f;
            DesiredRate = 0f;
            DesiredAcceleration = 0f;
            ThreeStateInputGain = 0f;
            StateRestoration = 0f;
            RateDamping = 0f;
            AccelerationDamping = 0f;
            MinimumState = 0f;
            MaximumState = 0f;
            MaximumAbsoluteRate = 0f;
            MaximumAbsoluteAcceleration = 0f;
            ActiveEpsilon = 0f;
            ControlSemanticsVersion = 0;
            EnforceMinimumDesiredState = false;
            MinimumDesiredState = 0f;
            StateTolerance = 0f;
            RateTolerance = 0f;
            AccelerationTolerance = 0f;
            ControlledEquilibriumDwellSeconds = 0f;
        }

        public static AxiomDynamicsParameters DefaultFor(AxiomKind kind)
        {
            AxiomDynamicsParameters result;
            switch (kind)
            {
                case AxiomKind.Heat:
                    result = new AxiomDynamicsParameters(.24f, .055f, .18f, .40f, .26f, .45f, 1f, 20f, .05f, 128);
                    break;
                case AxiomKind.Order:
                    result = new AxiomDynamicsParameters(.19f, .048f, .16f, .35f, .23f, .45f, 1f, 20f, .05f, 128);
                    break;
                case AxiomKind.Flow:
                    result = new AxiomDynamicsParameters(.25f, .040f, .14f, .30f, .25f, .40f, 1f, 20f, .05f, 128);
                    break;
                case AxiomKind.Mass:
                    result = new AxiomDynamicsParameters(.21f, .052f, .15f, .33f, .20f, .45f, 1f, 20f, .05f, 128);
                    break;
                default:
                    result = new AxiomDynamicsParameters(.20f, .050f, .16f, .32f, .24f, .45f, 1f, 20f, .05f, 128);
                    break;
            }

            result.ThreeStateModelVersion = 1;
            result.DesiredState = 0f;
            result.DesiredRate = 0f;
            result.DesiredAcceleration = 0f;
            result.ThreeStateInputGain = 60f;
            result.StateRestoration = 1.4f;
            result.RateDamping = 3.8f;
            result.AccelerationDamping = 4f;
            result.MinimumState = 0f;
            result.MaximumState = 100f;
            result.MaximumAbsoluteRate = 100f;
            result.MaximumAbsoluteAcceleration = 500f;
            result.ActiveEpsilon = .001f;
            if (kind == AxiomKind.Mass)
            {
                result.DesiredState = 1f;
                result.MinimumState = .01f;
            }
            else if (kind == AxiomKind.Heat || kind == AxiomKind.Order)
            {
                result.MinimumState = -100f;
            }
            // These are the existing default control tolerances, now owned by
            // the actor/phenomenon dynamics profile. No new balance values are
            // invented here. Minimum State remains opt-in until a production
            // profile deliberately supplies its usable runway.
            result.ControlSemanticsVersion = 1;
            result.EnforceMinimumDesiredState = false;
            result.MinimumDesiredState = 0f;
            result.StateTolerance = 1f;
            result.RateTolerance = .75f;
            result.AccelerationTolerance = 3f;
            result.ControlledEquilibriumDwellSeconds = 0.5f;
            return result;
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
            result.ThreeStateModelVersion = result.ThreeStateModelVersion < 1 ? 1 : result.ThreeStateModelVersion;
            result.DesiredState = FiniteOr(result.DesiredState, 0f);
            result.DesiredRate = FiniteOr(result.DesiredRate, 0f);
            result.DesiredAcceleration = FiniteOr(result.DesiredAcceleration, 0f);
            result.ThreeStateInputGain = Positive(result.ThreeStateInputGain, 60f);
            result.StateRestoration = Positive(result.StateRestoration, 1.4f);
            result.RateDamping = Positive(result.RateDamping, 3.8f);
            result.AccelerationDamping = Positive(result.AccelerationDamping, 4f);
            result.MinimumState = FiniteOr(result.MinimumState, 0f);
            result.MaximumState = Positive(result.MaximumState, 100f);
            if (result.MaximumState < result.MinimumState) result.MaximumState = result.MinimumState;
            result.MaximumAbsoluteRate = Positive(result.MaximumAbsoluteRate, 100f);
            result.MaximumAbsoluteAcceleration = Positive(result.MaximumAbsoluteAcceleration, 500f);
            result.ActiveEpsilon = Positive(result.ActiveEpsilon, .001f);
            result.ControlSemanticsVersion = result.ControlSemanticsVersion < 0 ? 0 : result.ControlSemanticsVersion;
            result.MinimumDesiredState = FiniteOr(result.MinimumDesiredState, 0f);
            if (result.EnforceMinimumDesiredState && result.MaximumState < result.MinimumDesiredState)
            {
                result.MaximumState = result.MinimumDesiredState;
            }
            result.StateTolerance = Positive(result.StateTolerance, 1f);
            result.RateTolerance = Positive(result.RateTolerance, .75f);
            result.AccelerationTolerance = Positive(result.AccelerationTolerance, 3f);
            result.ControlledEquilibriumDwellSeconds = NonNegativeFinite(
                result.ControlledEquilibriumDwellSeconds, .5f);
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

        private static float FiniteOr(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static float NonNegativeFinite(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f ? value : fallback;
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
