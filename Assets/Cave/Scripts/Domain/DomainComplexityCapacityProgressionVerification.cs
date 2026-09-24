using System;
using Cave.Axioms.Mastery;

namespace Cave.Domain
{
    /// <summary>Deterministic verification for permanent Complexity Capacity progression.</summary>
    public static class DomainComplexityCapacityProgressionVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyNoSeedAndBaseCapacity(out failure)
                && VerifyTierQualificationAndBreadth(out failure)
                && VerifyMonotonicCommitAndRunReset(out failure)
                && VerifyAuthoringBoundaries(out failure)
                && VerifyDeterminismAndBounds(out failure);
        }

        private static bool VerifyNoSeedAndBaseCapacity(out string failure)
        {
            DomainComplexityCapacityEvaluation noSeed = DomainComplexityCapacityProgression.Evaluate(
                false, Mastery(4, true, true), PlayerMasteryPolicy.Default, DomainComplexityCapacityTier.TierIV);
            DomainComplexityCapacityEvaluation seed = DomainComplexityCapacityProgression.Evaluate(
                true, PlayerMasteryEvidenceState.Empty, PlayerMasteryPolicy.Default, DomainComplexityCapacityTier.None);
            DomainComplexityReport minimum = DomainComplexityCalculator.Calculate(
                Composition(Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation)),
                DomainComplexityPolicy.Default);
            return Expect(!noSeed.HasDomainSeed
                && noSeed.Capacity == 0f
                && noSeed.CurrentTier == DomainComplexityCapacityTier.None
                && seed.CurrentTier == DomainComplexityCapacityTier.Seed
                && Approximately(seed.Capacity, minimum.TotalComplexity)
                && seed.Capacity > 0f,
                "Seed capacity did not remain the minimum legal one-Law Complexity.", out failure);
        }

        private static bool VerifyTierQualificationAndBreadth(out string failure)
        {
            PlayerMasteryPolicy policy = PlayerMasteryPolicy.Default;
            DomainComplexityCapacityEvaluation below = DomainComplexityCapacityProgression.Evaluate(
                true, Mastery(1, true, false), policy, DomainComplexityCapacityTier.Seed);
            DomainComplexityCapacityEvaluation exactTierTwo = DomainComplexityCapacityProgression.Evaluate(
                true, Mastery(2, true, false), policy, DomainComplexityCapacityTier.Seed);
            DomainComplexityCapacityEvaluation singlePhenomenon = DomainComplexityCapacityProgression.Evaluate(
                true, Mastery(1, true, true), policy, DomainComplexityCapacityTier.Seed);
            return Expect(!below.IsAdvancementNewlyEarned
                && exactTierTwo.QualifiedTier == DomainComplexityCapacityTier.TierII
                && exactTierTwo.IsAdvancementNewlyEarned
                && singlePhenomenon.QualifiedTier == DomainComplexityCapacityTier.Seed
                && exactTierTwo.UnsatisfiedRequirements.Count == 0,
                "Tier II did not require real breadth plus existing Projectile evidence.", out failure);
        }

        private static bool VerifyMonotonicCommitAndRunReset(out string failure)
        {
            PlayerMasteryPolicy policy = PlayerMasteryPolicy.Default;
            DomainComplexityCapacityEvaluation earned = DomainComplexityCapacityProgression.Evaluate(
                true, Mastery(2, true, false), policy, DomainComplexityCapacityTier.Seed);
            DomainComplexityCapacityTier committed = DomainComplexityCapacityProgression.Commit(
                DomainComplexityCapacityTier.Seed, earned);
            DomainComplexityCapacityEvaluation resetRun = DomainComplexityCapacityProgression.Evaluate(
                true, PlayerMasteryEvidenceState.Empty, policy, committed);
            DomainComplexityCapacityTier notDowngraded = DomainComplexityCapacityProgression.Commit(committed, resetRun);
            return Expect(committed == DomainComplexityCapacityTier.TierII
                && resetRun.CurrentTier == DomainComplexityCapacityTier.TierII
                && resetRun.Capacity > 0f
                && notDowngraded == DomainComplexityCapacityTier.TierII,
                "Complexity Capacity commit was not monotonic across a run-local mastery reset.", out failure);
        }

        private static bool VerifyAuthoringBoundaries(out string failure)
        {
            PlayerMasteryPolicy policy = PlayerMasteryPolicy.Default;
            DomainLaw heatPropagation = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            DomainLaw flowCatalysis = Law(LawExpression.Projectile, LawPhenomenon.Flow, LawTerritoryPrinciple.Catalysis);
            DomainLaw flowPropagation = Law(LawExpression.Projectile, LawPhenomenon.Flow, LawTerritoryPrinciple.Propagation);
            DomainLaw massPropagation = Law(LawExpression.Projectile, LawPhenomenon.Mass, LawTerritoryPrinciple.Propagation);
            float tierTwoCapacity = DomainComplexityCapacityProgression.GetCapacity(
                DomainComplexityCapacityTier.TierII, DomainComplexityPolicy.Default);
            DomainAuthoringContext emptyRunContext = new DomainAuthoringContext(
                true, PlayerMasteryEvidenceState.Empty, tierTwoCapacity, policy, DomainComplexityPolicy.Default);
            DomainAuthoringEligibilityResult emptyRun = DomainAuthoringEligibility.Evaluate(
                DomainComposition.Empty, heatPropagation, emptyRunContext);

            PlayerMasteryEvidenceState mastery = Mastery(2, true, false);
            DomainAuthoringContext eligibleContext = new DomainAuthoringContext(
                true, mastery, tierTwoCapacity, policy, DomainComplexityPolicy.Default);
            DomainComposition oneLaw = Composition(heatPropagation);
            DomainAuthoringEligibilityResult exact = DomainAuthoringEligibility.Evaluate(oneLaw, flowCatalysis, eligibleContext);
            DomainComposition twoLawBreadth = Composition(heatPropagation, flowPropagation);
            DomainAuthoringEligibilityResult above = DomainAuthoringEligibility.Evaluate(
                twoLawBreadth,
                massPropagation,
                new DomainAuthoringContext(true, Mastery(3, true, false), 8f, policy, DomainComplexityPolicy.Default));
            DomainComplexityReport threeLawReport = DomainComplexityCalculator.Calculate(
                DomainCompositionEditor.Add(twoLawBreadth, massPropagation).Resulting,
                DomainComplexityPolicy.Default);

            return Expect(emptyRun.RejectionReason == DomainAuthoringRejectionReason.PhenomenonMasteryIncomplete
                && exact.IsEligible
                && Approximately(exact.ProposedComplexity.TotalComplexity, tierTwoCapacity)
                && !above.IsEligible
                && above.RejectionReason == DomainAuthoringRejectionReason.ComplexityCapacityExceeded
                && Approximately(threeLawReport.TotalComplexity, 9f),
                "Authoring eligibility did not preserve run mastery and exact/over-capacity boundaries.", out failure);
        }

        private static bool VerifyDeterminismAndBounds(out string failure)
        {
            PlayerMasteryEvidenceState mastery = Mastery(4, true, true);
            DomainComplexityCapacityEvaluation first = DomainComplexityCapacityProgression.Evaluate(
                true, mastery, PlayerMasteryPolicy.Default, DomainComplexityCapacityTier.TierII);
            DomainComplexityCapacityEvaluation second = DomainComplexityCapacityProgression.Evaluate(
                true, mastery, PlayerMasteryPolicy.Default, DomainComplexityCapacityTier.TierII);
            float maximum = DomainComplexityCapacityProgression.GetCapacity(
                (DomainComplexityCapacityTier)999, DomainComplexityPolicy.Default);
            return Expect(first.CurrentTier == second.CurrentTier
                && first.QualifiedTier == second.QualifiedTier
                && Approximately(first.Capacity, second.Capacity)
                && first.QualifiedTier == DomainComplexityCapacityTier.TierIV
                && maximum == DomainComplexityCapacityProgression.GetCapacity(
                    DomainComplexityCapacityProgression.HighestTier, DomainComplexityPolicy.Default)
                && maximum >= 0f,
                "Complexity Capacity evaluation was non-deterministic or escaped its finite tier bounds.", out failure);
        }

        private static PlayerMasteryEvidenceState Mastery(int phenomenonCount, bool projectile, bool frenzy)
        {
            PlayerMasteryPolicy policy = PlayerMasteryPolicy.Default;
            PlayerMasteryEvidenceState state = PlayerMasteryEvidenceState.Empty;
            LawPhenomenon[] phenomena =
            {
                LawPhenomenon.Heat,
                LawPhenomenon.Flow,
                LawPhenomenon.Mass,
                LawPhenomenon.Compression
            };
            for (int index = 0; index < phenomenonCount && index < phenomena.Length; index++)
            {
                foreach (MasteryEvidenceDimension dimension in (MasteryEvidenceDimension[])Enum.GetValues(typeof(MasteryEvidenceDimension)))
                {
                    state = state.Submit(new PhenomenonMasteryEvidenceSubmission(
                        phenomena[index], dimension, 1f, true,
                        projectile ? LawExpression.Projectile : (LawExpression?)null), policy);
                }
            }

            return frenzy
                ? state.SubmitFrenzy(new FrenzyMasterySample(5f, 2, 0), policy)
                : state;
        }

        private static DomainComposition Composition(params DomainLaw[] laws)
        {
            DomainComposition composition = DomainComposition.Empty;
            for (int index = 0; index < laws.Length; index++)
            {
                composition = DomainCompositionEditor.Add(composition, laws[index]).Resulting;
            }

            return composition;
        }

        private static DomainLaw Law(LawExpression expression, LawPhenomenon phenomenon, LawTerritoryPrinciple territory)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, territory, out law, out validation))
            {
                throw new InvalidOperationException("Verification could not create Law: " + validation.RejectionReason);
            }

            return law;
        }

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) <= Tolerance;
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }
    }
}
