using System;
using Cave.Axioms.Mastery;

namespace Cave.Domain
{
    /// <summary>
    /// Session-only test affordance for the authored Domain page. It supplies a
    /// complete testing context to UI authoring calls without mutating PlayerPrefs,
    /// real mastery evidence, Domain law validation, or unavailable expressions.
    /// </summary>
    public static class DomainTestOverride
    {
        private const float TestCapacity = 100000f;
        private static bool enabled;

        public static event Action Changed;
        public static bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                Changed?.Invoke();
            }
        }

        public static bool HasDomainAccess => enabled || DomainProgression.HasDomainSeed;

        public static PlayerMasteryEvidenceState ResolveEvidence(
            PlayerMasteryEvidenceState realEvidence,
            PlayerMasteryPolicy policy)
        {
            return enabled ? BuildCompleteEvidence(policy ?? PlayerMasteryPolicy.Default)
                : realEvidence ?? PlayerMasteryEvidenceState.Empty;
        }

        public static float ResolveCapacity(DomainComplexityPolicy complexityPolicy)
        {
            return enabled ? TestCapacity : DomainProgression.CurrentComplexityCapacity;
        }

        public static DomainAuthoringContext CreateAuthoringContext(
            PlayerMasteryEvidenceState realEvidence,
            PlayerMasteryPolicy masteryPolicy,
            DomainComplexityPolicy complexityPolicy)
        {
            PlayerMasteryPolicy policy = masteryPolicy ?? PlayerMasteryPolicy.Default;
            DomainComplexityPolicy complexity = complexityPolicy ?? DomainComplexityPolicy.Default;
            if (!enabled)
            {
                return DomainProgression.CreateAuthoringContext(realEvidence, policy, complexity);
            }

            return new DomainAuthoringContext(
                true,
                BuildCompleteEvidence(policy),
                TestCapacity,
                policy,
                complexity);
        }

        private static PlayerMasteryEvidenceState BuildCompleteEvidence(PlayerMasteryPolicy policy)
        {
            PlayerMasteryEvidenceState result = PlayerMasteryEvidenceState.Empty;
            Array phenomena = Enum.GetValues(typeof(LawPhenomenon));
            for (int index = 0; index < phenomena.Length; index++)
            {
                LawPhenomenon phenomenon = (LawPhenomenon)phenomena.GetValue(index);
                result = Submit(result, phenomenon, MasteryEvidenceDimension.State, policy.StateThreshold, policy);
                result = Submit(result, phenomenon, MasteryEvidenceDimension.Rate, policy.RateThreshold, policy);
                result = Submit(result, phenomenon, MasteryEvidenceDimension.Acceleration, policy.AccelerationThreshold, policy);
                result = Submit(result, phenomenon, MasteryEvidenceDimension.State, policy.StateThreshold, policy, LawExpression.Trap);
                result = Submit(result, phenomenon, MasteryEvidenceDimension.Rate, policy.RateThreshold, policy, LawExpression.Trap);
                result = Submit(result, phenomenon, MasteryEvidenceDimension.Acceleration, policy.AccelerationThreshold, policy, LawExpression.Trap);
            }

            // One deliberately oversized session-only sample satisfies the
            // configured Frenzy threshold without persisting or awarding real
            // evidence. It is never submitted to the runtime evidence owner.
            result = result.SubmitFrenzy(new FrenzyMasterySample(100000f, 100000, 100000), policy);
            return result;
        }

        private static PlayerMasteryEvidenceState Submit(
            PlayerMasteryEvidenceState state,
            LawPhenomenon phenomenon,
            MasteryEvidenceDimension dimension,
            float threshold,
            PlayerMasteryPolicy policy, LawExpression expression = LawExpression.Projectile)
        {
            return state.Submit(new PhenomenonMasteryEvidenceSubmission(
                phenomenon,
                dimension,
                Math.Max(1f, threshold),
                true,
                expression), policy);
        }
    }
}
