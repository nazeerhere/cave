using Cave.Axioms.Mastery;

namespace Cave.Domain
{
    /// <summary>Pure/session-scope guard for the pause-menu Domain test override.</summary>
    public static class DomainTestOverrideVerification
    {
        public static bool TryRunAll(out string failure)
        {
            bool previous = DomainTestOverride.Enabled;
            try
            {
                DomainTestOverride.Enabled = true;
                DomainLaw candidate;
                LawValidationResult validation;
                bool created = DomainLaw.TryCreate(
                    LawExpression.Projectile,
                    LawPhenomenon.Heat,
                    LawTerritoryPrinciple.Accumulation,
                    out candidate,
                    out validation);
                DomainAuthoringContext context = DomainTestOverride.CreateAuthoringContext(
                    PlayerMasteryEvidenceState.Empty,
                    PlayerMasteryPolicy.Default,
                    DomainComplexityPolicy.Default);
                DomainAuthoringEligibilityResult eligible = DomainAuthoringEligibility.Evaluate(
                    DomainComposition.Empty,
                    candidate,
                    context);
                bool valid = created
                    && DomainTestOverride.HasDomainAccess
                    && eligible.IsEligible
                    && context.HasDomainSeed
                    && context.ComplexityCapacity > 1000f;
                failure = valid ? null : "Domain Test Override did not produce an eligible testing context for supported authored Laws.";
                return valid;
            }
            finally
            {
                DomainTestOverride.Enabled = previous;
            }
        }
    }
}
