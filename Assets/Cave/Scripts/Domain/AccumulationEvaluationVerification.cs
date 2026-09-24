using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for pure many-to-one Accumulation.</summary>
    public static class AccumulationEvaluationVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyBasicAndSequentialHeat(out failure)
                && VerifySignedHeatAndPotential(out failure)
                && VerifyContributorIdentityAndLimits(out failure)
                && VerifyRequestValidation(out failure)
                && VerifyLawEligibility(out failure)
                && VerifyPhenomenonSpecificTransfer(out failure)
                && VerifyImmutabilityDeterminismConservationAndNoRecursion(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyBasicAndSequentialHeat(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot target = Carrier("T", LawPhenomenon.Heat, 0f);
            AccumulationEvaluationResult result = Evaluate(law, target, 1f, 3,
                Carrier("A", LawPhenomenon.Heat, 3f),
                Carrier("B", LawPhenomenon.Heat, 2f),
                Carrier("C", LawPhenomenon.Heat, 1f));

            return Expect(result.Succeeded
                && result.AcceptedContributorCount == 3
                && Approximately(result.ContributorResults[0].ContributorAfter.SemanticValue, 2f)
                && Approximately(result.ContributorResults[1].ContributorAfter.SemanticValue, 1f)
                && Approximately(result.ContributorResults[2].ContributorAfter.SemanticValue, 0f)
                && Approximately(result.ContributorResults[0].FocalBeforeThisContribution.SemanticValue, 0f)
                && Approximately(result.ContributorResults[0].FocalAfterThisContribution.SemanticValue, 1f)
                && Approximately(result.ContributorResults[1].FocalBeforeThisContribution.SemanticValue, 1f)
                && Approximately(result.ContributorResults[1].FocalAfterThisContribution.SemanticValue, 2f)
                && Approximately(result.ContributorResults[2].FocalBeforeThisContribution.SemanticValue, 2f)
                && Approximately(result.ContributorResults[2].FocalAfterThisContribution.SemanticValue, 3f)
                && Approximately(result.FinalFocalRecipient.SemanticValue, 3f)
                && Approximately(result.TotalSourceDelta, -3f)
                && Approximately(result.TotalFocalDelta, 3f)
                && result.IsConserved,
                "Accumulation did not resolve Heat contributors sequentially against the current focal proposal.", out failure);
        }

        private static bool VerifySignedHeatAndPotential(out string failure)
        {
            AccumulationEvaluationResult heat = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Heat),
                Carrier("T", LawPhenomenon.Heat, 0f), 1f, 2,
                Carrier("A", LawPhenomenon.Heat, -1f),
                Carrier("B", LawPhenomenon.Heat, 2f));
            AccumulationEvaluationResult potential = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Potential),
                Carrier("T", LawPhenomenon.Potential, 0f), 1f, 2,
                Carrier("A", LawPhenomenon.Potential, -2f),
                Carrier("B", LawPhenomenon.Potential, 2f));

            return Expect(heat.Succeeded
                && Approximately(heat.ContributorResults[0].ContributorAfter.SemanticValue, -2f)
                && Approximately(heat.ContributorResults[1].ContributorAfter.SemanticValue, 1f)
                && Approximately(heat.FinalFocalRecipient.SemanticValue, 2f)
                && heat.IsConserved
                && potential.Succeeded
                && Approximately(potential.ContributorResults[0].ContributorAfter.SemanticValue, -3f)
                && Approximately(potential.ContributorResults[1].ContributorAfter.SemanticValue, 1f)
                && Approximately(potential.FinalFocalRecipient.SemanticValue, 2f)
                && potential.IsConserved,
                "Accumulation did not preserve signed Transfer semantics and conservation.", out failure);
        }

        private static bool VerifyContributorIdentityAndLimits(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot target = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot a = Carrier("A", LawPhenomenon.Heat, 3f);
            AccumulationEvaluationResult identity = Evaluate(law, target, 1f, 4,
                a,
                Carrier("B", LawPhenomenon.Heat, 2f),
                a,
                target,
                Carrier("C", LawPhenomenon.Heat, 1f));
            AccumulationEvaluationResult capped = Evaluate(law, target, 1f, 2,
                Carrier("A", LawPhenomenon.Heat, 3f),
                Carrier("B", LawPhenomenon.Heat, 2f),
                Carrier("C", LawPhenomenon.Heat, 1f),
                Carrier("D", LawPhenomenon.Heat, 1f));
            AccumulationEvaluationResult zero = Evaluate(law, target, 1f, 0,
                Carrier("A", LawPhenomenon.Heat, 3f));

            return Expect(identity.Succeeded
                && identity.AcceptedContributorCount == 3
                && identity.ContributorResults[2].RejectionReason == AccumulationContributorRejectionReason.DuplicateContributor
                && identity.ContributorResults[3].RejectionReason == AccumulationContributorRejectionReason.FocalRecipientReintroduced
                && Approximately(identity.FinalFocalRecipient.SemanticValue, 3f)
                && capped.AcceptedContributorCount == 2
                && capped.ContributorResults[2].RejectionReason == AccumulationContributorRejectionReason.ContributorLimitReached
                && capped.ContributorResults[3].RejectionReason == AccumulationContributorRejectionReason.ContributorLimitReached
                && Approximately(capped.FinalFocalRecipient.SemanticValue, 2f)
                && zero.Succeeded
                && zero.AcceptedContributorCount == 0
                && zero.ContributorResults[0].RejectionReason == AccumulationContributorRejectionReason.ContributorLimitReached
                && Approximately(zero.FinalFocalRecipient.SemanticValue, 0f),
                "Accumulation contributor deduplication, focal exclusion, or bounded resolution failed.", out failure);
        }

        private static bool VerifyRequestValidation(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot target = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot source = Carrier("A", LawPhenomenon.Heat, 1f);
            LawExpressionContext context = new LawExpressionContext(true, LawExpression.Projectile, false);
            AccumulationEvaluationResult negativeLimit = Evaluate(law, target, PhenomenonOperationKind.Transfer, 1f, -1,
                context, source);
            AccumulationEvaluationResult negativeMagnitude = Evaluate(law, target, PhenomenonOperationKind.Transfer, -1f, 1,
                context, source);
            AccumulationEvaluationResult add = Evaluate(law, target, PhenomenonOperationKind.Add, 1f, 1, context, source);
            AccumulationEvaluationResult remove = Evaluate(law, target, PhenomenonOperationKind.Remove, 1f, 1, context, source);
            AccumulationEvaluationResult zeroTransfer = Evaluate(law, target, PhenomenonOperationKind.Transfer, 0f, 1, context, source);
            AccumulationEvaluationResult invalidFocal = AccumulationEvaluator.Evaluate(new AccumulationEvaluationRequest(
                law, new LawExpressionContext(true, LawExpression.Projectile, false),
                new PhenomenonConvergenceTopology(null, new[] { source }), PhenomenonOperationKind.Transfer, 1f, 1));
            AccumulationEvaluationResult mismatchContributor = Evaluate(law, target, 1f, 2,
                Carrier("P", LawPhenomenon.Potential, 1f), source);

            return Expect(!negativeLimit.Succeeded
                && negativeLimit.RejectionReason == AccumulationEvaluationRejectionReason.NegativeContributorLimit
                && !negativeMagnitude.Succeeded
                && negativeMagnitude.RejectionReason == AccumulationEvaluationRejectionReason.InvalidTransferMagnitude
                && !add.Succeeded
                && add.RejectionReason == AccumulationEvaluationRejectionReason.OperationNotSupportedByTerritory
                && !remove.Succeeded
                && remove.RejectionReason == AccumulationEvaluationRejectionReason.OperationNotSupportedByTerritory
                && zeroTransfer.Succeeded
                && zeroTransfer.AcceptedContributorCount == 1
                && Approximately(zeroTransfer.FinalFocalRecipient.SemanticValue, 0f)
                && !invalidFocal.Succeeded
                && invalidFocal.RejectionReason == AccumulationEvaluationRejectionReason.FocalRecipientUnavailable
                && mismatchContributor.Succeeded
                && mismatchContributor.ContributorResults[0].RejectionReason == AccumulationContributorRejectionReason.PhenomenonMismatch
                && mismatchContributor.ContributorResults[1].Succeeded,
                "Accumulation did not reject unsupported or structurally invalid requests explicitly.", out failure);
        }

        private static bool VerifyLawEligibility(out string failure)
        {
            PhenomenonCarrierSnapshot target = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot source = Carrier("A", LawPhenomenon.Heat, 1f);
            DomainLaw projectile = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            DomainLaw frenzy = Law(LawExpression.Frenzy, LawPhenomenon.Heat);
            AccumulationEvaluationResult projectileEligible = Evaluate(projectile, target, 1f, 1, source);
            AccumulationEvaluationResult mismatch = Evaluate(projectile, target, 1f, 1,
                new LawExpressionContext(true, LawExpression.Frenzy, true), source);
            AccumulationEvaluationResult frenzyActive = Evaluate(frenzy, target, 1f, 1,
                new LawExpressionContext(true, LawExpression.Frenzy, true), source);
            AccumulationEvaluationResult frenzyInactive = Evaluate(frenzy, target, 1f, 1,
                new LawExpressionContext(true, LawExpression.Frenzy, false), source);

            return Expect(projectileEligible.Succeeded && projectileEligible.Eligible
                && !mismatch.Succeeded
                && mismatch.RejectionReason == AccumulationEvaluationRejectionReason.ExpressionMismatch
                && frenzyActive.Succeeded && frenzyActive.Eligible
                && !frenzyInactive.Succeeded
                && frenzyInactive.RejectionReason == AccumulationEvaluationRejectionReason.FrenzyInactive,
                "Accumulation did not reuse existing expression eligibility rules.", out failure);
        }

        private static bool VerifyPhenomenonSpecificTransfer(out string failure)
        {
            AccumulationEvaluationResult mass = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Mass),
                Carrier("T", LawPhenomenon.Mass, 1f), .25f, 1,
                Carrier("A", LawPhenomenon.Mass, 1.5f));
            AccumulationEvaluationResult phase = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Phase),
                Carrier("T", LawPhenomenon.Phase, 2f), 1f, 1,
                Carrier("A", LawPhenomenon.Phase, 4f));
            AccumulationEvaluationResult resonance = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Resonance),
                Carrier("T", LawPhenomenon.Resonance, 0f), 1f, 1,
                Carrier("A", LawPhenomenon.Resonance, 3f));

            return Expect(mass.Succeeded
                && Approximately(mass.ContributorResults[0].ContributorAfter.SemanticValue, 1.25f)
                && Approximately(mass.FinalFocalRecipient.SemanticValue, 1.25f)
                && phase.Succeeded
                && Approximately(phase.ContributorResults[0].ContributorAfter.SemanticValue, 3f)
                && Approximately(phase.FinalFocalRecipient.SemanticValue, 3f)
                && resonance.Succeeded
                && Approximately(resonance.ContributorResults[0].ContributorAfter.SemanticValue, 2f)
                && Approximately(resonance.FinalFocalRecipient.SemanticValue, 1f),
                "Accumulation did not remain a generic semantic Transfer for Mass, Phase, and Resonance.", out failure);
        }

        private static bool VerifyImmutabilityDeterminismConservationAndNoRecursion(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot target = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot a = Carrier("A", LawPhenomenon.Heat, 3f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, -1f);
            AccumulationEvaluationRequest request = new AccumulationEvaluationRequest(
                law, new LawExpressionContext(true, LawExpression.Projectile, false),
                new PhenomenonConvergenceTopology(target, new[] { a, b }),
                PhenomenonOperationKind.Transfer, 1f, 2);
            AccumulationEvaluationResult first = AccumulationEvaluator.Evaluate(request);
            AccumulationEvaluationResult second = AccumulationEvaluator.Evaluate(request);

            return Expect(first.Succeeded
                && first.AcceptedContributorCount == 2
                && first.ContributorResults.Count == 2
                && Approximately(target.SemanticState.SemanticValue, 0f)
                && Approximately(a.SemanticState.SemanticValue, 3f)
                && Approximately(b.SemanticState.SemanticValue, -1f)
                && first.IsConserved
                && first.ContributorResults[0].ContributorId.Equals(second.ContributorResults[0].ContributorId)
                && first.ContributorResults[1].ContributorId.Equals(second.ContributorResults[1].ContributorId)
                && Approximately(first.FinalFocalRecipient.SemanticValue, second.FinalFocalRecipient.SemanticValue)
                && first.ContributorResults[0].BaseResult != second.ContributorResults[0].BaseResult,
                "Accumulation mutated inputs, violated conservation, lost deterministic order, or recursively expanded work.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string lawFailure;
            string semanticsFailure;
            string operationsFailure;
            string lawEvaluationFailure;
            string topologyFailure;
            string catalysisFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            bool operations = PhenomenonOperationsVerification.TryRunAll(out operationsFailure);
            bool lawEvaluation = DomainLawEvaluationVerification.TryRunAll(out lawEvaluationFailure);
            bool topology = PhenomenonTopologyVerification.TryRunAll(out topologyFailure);
            bool catalysis = CatalysisEvaluationVerification.TryRunAll(out catalysisFailure);
            return Expect(laws && semantics && operations && lawEvaluation && topology && catalysis,
                "Earlier Domain verification regressed: law=" + lawFailure
                + "; semantics=" + semanticsFailure
                + "; operations=" + operationsFailure
                + "; evaluation=" + lawEvaluationFailure
                + "; topology=" + topologyFailure
                + "; catalysis=" + catalysisFailure,
                out failure);
        }

        private static AccumulationEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot target,
            float magnitude,
            int maximumContributors,
            params PhenomenonCarrierSnapshot[] contributors)
        {
            return Evaluate(law, target, PhenomenonOperationKind.Transfer, magnitude, maximumContributors,
                new LawExpressionContext(true, law.Expression, law.Expression == LawExpression.Frenzy), contributors);
        }

        private static AccumulationEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot target,
            float magnitude,
            int maximumContributors,
            LawExpressionContext context,
            params PhenomenonCarrierSnapshot[] contributors)
        {
            return Evaluate(law, target, PhenomenonOperationKind.Transfer, magnitude, maximumContributors, context, contributors);
        }

        private static AccumulationEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot target,
            PhenomenonOperationKind operation,
            float magnitude,
            int maximumContributors,
            LawExpressionContext context,
            params PhenomenonCarrierSnapshot[] contributors)
        {
            return AccumulationEvaluator.Evaluate(new AccumulationEvaluationRequest(
                law, context, new PhenomenonConvergenceTopology(target, contributors), operation, magnitude, maximumContributors));
        }

        private static DomainLaw Law(LawExpression expression, LawPhenomenon phenomenon)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, LawTerritoryPrinciple.Accumulation, out law, out validation))
            {
                throw new InvalidOperationException("Verification could not create Law: " + validation.RejectionReason);
            }
            return law;
        }

        private static PhenomenonCarrierSnapshot Carrier(string id, LawPhenomenon phenomenon, float value)
        {
            return new PhenomenonCarrierSnapshot(new PhenomenonCarrierId(id), phenomenon, Snapshot(phenomenon, value));
        }

        private static PhenomenonSemanticSnapshot Snapshot(LawPhenomenon phenomenon, float value)
        {
            PhenomenonSemanticSnapshot snapshot;
            PhenomenonSemanticResult result;
            if (!PhenomenonSemanticClassifier.TryCreateSnapshot(phenomenon, value, out snapshot, out result))
            {
                throw new InvalidOperationException("Verification could not create semantic state: " + result.RejectionReason);
            }
            return snapshot;
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
