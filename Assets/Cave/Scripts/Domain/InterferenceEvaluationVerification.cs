using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for pure constructive Interference.</summary>
    public static class InterferenceEvaluationVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyBasicHeatAndCompatibility(out failure)
                && VerifyDeduplicationFocalAndLimits(out failure)
                && VerifyRemoveAndMagnitudeValidation(out failure)
                && VerifyLawEligibilityAndTransferBoundary(out failure)
                && VerifyPhenomenonSpecificBehavior(out failure)
                && VerifyImmutabilityDeterminismAndNoChaining(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyBasicHeatAndCompatibility(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot reference = Carrier("A", LawPhenomenon.Heat, 2f);
            PhenomenonCarrierSnapshot focal = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 3f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 4f);
            PhenomenonCarrierSnapshot regular = Carrier("D", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot mismatch = Carrier("P", LawPhenomenon.Potential, 2f);
            InterferenceEvaluationResult result = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Add, 1f, 5, b, c, regular, mismatch);

            return Expect(result.Succeeded
                && result.AcceptedContributorCount == 3
                && result.SkippedContributorCount == 2
                && result.ContributorResults[0].IsReference
                && result.ContributorResults[0].Accepted
                && result.ContributorResults[1].Accepted
                && result.ContributorResults[2].Accepted
                && result.ContributorResults[3].RejectionReason == InterferenceContributorRejectionReason.RegionMismatch
                && result.ContributorResults[4].RejectionReason == InterferenceContributorRejectionReason.PhenomenonMismatch
                && Approximately(result.EffectiveMagnitude, 3f)
                && Approximately(result.FocalAfter.SemanticValue, 3f)
                && result.FocalAfter.Region == PhenomenonSemanticRegion.High
                && Approximately(reference.SemanticState.SemanticValue, 2f)
                && Approximately(b.SemanticState.SemanticValue, 3f)
                && Approximately(c.SemanticState.SemanticValue, 4f),
                "Interference did not linearly combine same-region Heat contributors without mutating them.", out failure);
        }

        private static bool VerifyDeduplicationFocalAndLimits(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot reference = Carrier("A", LawPhenomenon.Heat, 2f);
            PhenomenonCarrierSnapshot focal = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 3f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 4f);
            InterferenceEvaluationResult dedup = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Add, 1f, 5, reference, b, c, b, focal);
            InterferenceEvaluationResult capped = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Add, 2f, 2, b, c, Carrier("D", LawPhenomenon.Heat, 5f));
            InterferenceEvaluationResult zeroCap = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Add, 1f, 0, b);
            InterferenceEvaluationResult negativeCap = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Add, 1f, -1, b);

            return Expect(dedup.Succeeded
                && dedup.AcceptedContributorCount == 3
                && dedup.ContributorResults[1].RejectionReason == InterferenceContributorRejectionReason.ReferenceReintroduced
                && dedup.ContributorResults[4].RejectionReason == InterferenceContributorRejectionReason.DuplicateContributor
                && dedup.ContributorResults[5].RejectionReason == InterferenceContributorRejectionReason.FocalRecipientReintroduced
                && Approximately(dedup.EffectiveMagnitude, 3f)
                && capped.Succeeded
                && capped.AcceptedContributorCount == 2
                && Approximately(capped.EffectiveMagnitude, 4f)
                && capped.ContributorResults[2].RejectionReason == InterferenceContributorRejectionReason.ContributorLimitReached
                && capped.ContributorResults[3].RejectionReason == InterferenceContributorRejectionReason.ContributorLimitReached
                && !zeroCap.Succeeded
                && zeroCap.RejectionReason == InterferenceEvaluationRejectionReason.InvalidContributorLimit
                && !negativeCap.Succeeded
                && negativeCap.RejectionReason == InterferenceEvaluationRejectionReason.InvalidContributorLimit,
                "Interference did not count the reference once or apply deterministic contributor deduplication/caps.", out failure);
        }

        private static bool VerifyRemoveAndMagnitudeValidation(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot reference = Carrier("A", LawPhenomenon.Heat, 2f);
            PhenomenonCarrierSnapshot focal = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 3f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 4f);
            InterferenceEvaluationResult remove = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Remove, 2f, 3, b, c);
            InterferenceEvaluationResult zero = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Add, 0f, 3, b, c);
            InterferenceEvaluationResult negative = Evaluate(law, reference, focal,
                PhenomenonOperationKind.Add, -1f, 3, b);

            return Expect(remove.Succeeded
                && remove.AcceptedContributorCount == 3
                && Approximately(remove.EffectiveMagnitude, 6f)
                && Approximately(remove.FocalAfter.SemanticValue, -6f)
                && zero.Succeeded
                && zero.AcceptedContributorCount == 3
                && Approximately(zero.EffectiveMagnitude, 0f)
                && Approximately(zero.FocalAfter.SemanticValue, 0f)
                && !negative.Succeeded
                && negative.RejectionReason == InterferenceEvaluationRejectionReason.InvalidBaseRequest,
                "Interference did not constructively combine Remove or follow zero/negative base semantics.", out failure);
        }

        private static bool VerifyLawEligibilityAndTransferBoundary(out string failure)
        {
            PhenomenonCarrierSnapshot reference = Carrier("A", LawPhenomenon.Heat, 2f);
            PhenomenonCarrierSnapshot focal = Carrier("T", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 3f);
            DomainLaw projectile = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            DomainLaw frenzy = Law(LawExpression.Frenzy, LawPhenomenon.Heat);
            InterferenceEvaluationResult projectileEligible = Evaluate(projectile, reference, focal,
                PhenomenonOperationKind.Add, 1f, 2, b);
            InterferenceEvaluationResult mismatch = Evaluate(projectile, reference, focal,
                PhenomenonOperationKind.Add, 1f, 2, new LawExpressionContext(true, LawExpression.Frenzy, true), b);
            InterferenceEvaluationResult frenzyActive = Evaluate(frenzy, reference, focal,
                PhenomenonOperationKind.Add, 1f, 2, new LawExpressionContext(true, LawExpression.Frenzy, true), b);
            InterferenceEvaluationResult frenzyInactive = Evaluate(frenzy, reference, focal,
                PhenomenonOperationKind.Add, 1f, 2, new LawExpressionContext(true, LawExpression.Frenzy, false), b);
            InterferenceEvaluationResult transfer = Evaluate(projectile, reference, focal,
                PhenomenonOperationKind.Transfer, 1f, 2, b);

            return Expect(projectileEligible.Succeeded && projectileEligible.Eligible
                && !mismatch.Succeeded
                && mismatch.RejectionReason == InterferenceEvaluationRejectionReason.ExpressionMismatch
                && frenzyActive.Succeeded
                && !frenzyInactive.Succeeded
                && frenzyInactive.RejectionReason == InterferenceEvaluationRejectionReason.FrenzyInactive
                && !transfer.Succeeded
                && transfer.RejectionReason == InterferenceEvaluationRejectionReason.OperationNotSupportedByTerritory,
                "Interference did not reuse Law eligibility or explicitly reject Transfer.", out failure);
        }

        private static bool VerifyPhenomenonSpecificBehavior(out string failure)
        {
            InterferenceEvaluationResult potential = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Potential),
                Carrier("A", LawPhenomenon.Potential, -2f), Carrier("T", LawPhenomenon.Potential, 0f),
                PhenomenonOperationKind.Remove, 1f, 2, Carrier("B", LawPhenomenon.Potential, -3f));
            InterferenceEvaluationResult order = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Order),
                Carrier("A", LawPhenomenon.Order, -2f), Carrier("T", LawPhenomenon.Order, 0f),
                PhenomenonOperationKind.Add, 1f, 2, Carrier("B", LawPhenomenon.Order, -3f));
            InterferenceEvaluationResult mass = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Mass),
                Carrier("A", LawPhenomenon.Mass, 1.25f), Carrier("T", LawPhenomenon.Mass, 1f),
                PhenomenonOperationKind.Add, .25f, 2, Carrier("B", LawPhenomenon.Mass, 1.5f));
            InterferenceEvaluationResult resonance = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Resonance),
                Carrier("A", LawPhenomenon.Resonance, 3f), Carrier("T", LawPhenomenon.Resonance, 0f),
                PhenomenonOperationKind.Add, 1f, 2, Carrier("B", LawPhenomenon.Resonance, 4f));
            InterferenceEvaluationResult phase = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Phase),
                Carrier("A", LawPhenomenon.Phase, 4f), Carrier("T", LawPhenomenon.Phase, 2f),
                PhenomenonOperationKind.Add, 1f, 2, Carrier("B", LawPhenomenon.Phase, 5f));

            return Expect(potential.Succeeded && Approximately(potential.FocalAfter.SemanticValue, -2f)
                && order.Succeeded && Approximately(order.FocalAfter.SemanticValue, 2f)
                && mass.Succeeded && Approximately(mass.FocalAfter.SemanticValue, 1.5f)
                && resonance.Succeeded && Approximately(resonance.FocalAfter.SemanticValue, 2f)
                && phase.Succeeded && Approximately(phase.FocalAfter.SemanticValue, 4f),
                "Interference did not remain generic region-compatible semantic arithmetic across phenomena.", out failure);
        }

        private static bool VerifyImmutabilityDeterminismAndNoChaining(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot reference = Carrier("A", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot focal = Carrier("T", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 2f);
            InterferenceEvaluationRequest request = Request(law, reference, focal,
                PhenomenonOperationKind.Add, 1f, 3, new LawExpressionContext(true, LawExpression.Projectile, false), b, c);
            InterferenceEvaluationResult first = InterferenceEvaluator.Evaluate(request);
            InterferenceEvaluationResult second = InterferenceEvaluator.Evaluate(request);

            return Expect(first.Succeeded
                && first.AcceptedContributorCount == 2
                && first.SkippedContributorCount == 1
                && first.ContributorResults[2].RejectionReason == InterferenceContributorRejectionReason.RegionMismatch
                && !first.HasConservationRequirement
                && !first.CanTriggerAdditionalTerritoryEvaluation
                && first.FocalAfter.Region == PhenomenonSemanticRegion.High
                && Approximately(reference.SemanticState.SemanticValue, 1f)
                && Approximately(b.SemanticState.SemanticValue, 0f)
                && Approximately(c.SemanticState.SemanticValue, 2f)
                && Approximately(focal.SemanticState.SemanticValue, 1f)
                && first.ContributorResults[0].CarrierId.Equals(second.ContributorResults[0].CarrierId)
                && first.ContributorResults[1].CarrierId.Equals(second.ContributorResults[1].CarrierId)
                && Approximately(first.EffectiveMagnitude, second.EffectiveMagnitude)
                && first.BaseResult != second.BaseResult,
                "Interference mutated contributors, made a conservation claim, or automatically chained another Territory evaluation.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string lawFailure;
            string semanticsFailure;
            string operationsFailure;
            string lawEvaluationFailure;
            string topologyFailure;
            string catalysisFailure;
            string accumulationFailure;
            string relationshipFailure;
            string synchronizationFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            bool operations = PhenomenonOperationsVerification.TryRunAll(out operationsFailure);
            bool lawEvaluation = DomainLawEvaluationVerification.TryRunAll(out lawEvaluationFailure);
            bool topology = PhenomenonTopologyVerification.TryRunAll(out topologyFailure);
            bool catalysis = CatalysisEvaluationVerification.TryRunAll(out catalysisFailure);
            bool accumulation = AccumulationEvaluationVerification.TryRunAll(out accumulationFailure);
            bool relationship = PhenomenonRelationshipGroupVerification.TryRunAll(out relationshipFailure);
            bool synchronization = SynchronizationEvaluationVerification.TryRunAll(out synchronizationFailure);
            return Expect(laws && semantics && operations && lawEvaluation && topology && catalysis && accumulation && relationship && synchronization,
                "Earlier Domain verification regressed: law=" + lawFailure
                + "; semantics=" + semanticsFailure
                + "; operations=" + operationsFailure
                + "; evaluation=" + lawEvaluationFailure
                + "; topology=" + topologyFailure
                + "; catalysis=" + catalysisFailure
                + "; accumulation=" + accumulationFailure
                + "; relationship=" + relationshipFailure
                + "; synchronization=" + synchronizationFailure,
                out failure);
        }

        private static InterferenceEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot reference,
            PhenomenonCarrierSnapshot focal,
            PhenomenonOperationKind operation,
            float magnitude,
            int maximumContributors,
            params PhenomenonCarrierSnapshot[] contributors)
        {
            return Evaluate(law, reference, focal, operation, magnitude, maximumContributors,
                new LawExpressionContext(true, law.Expression, law.Expression == LawExpression.Frenzy), contributors);
        }

        private static InterferenceEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot reference,
            PhenomenonCarrierSnapshot focal,
            PhenomenonOperationKind operation,
            float magnitude,
            int maximumContributors,
            LawExpressionContext context,
            params PhenomenonCarrierSnapshot[] contributors)
        {
            return InterferenceEvaluator.Evaluate(Request(
                law, reference, focal, operation, magnitude, maximumContributors, context, contributors));
        }

        private static InterferenceEvaluationRequest Request(
            DomainLaw law,
            PhenomenonCarrierSnapshot reference,
            PhenomenonCarrierSnapshot focal,
            PhenomenonOperationKind operation,
            float magnitude,
            int maximumContributors,
            LawExpressionContext context,
            params PhenomenonCarrierSnapshot[] contributors)
        {
            return new InterferenceEvaluationRequest(
                law, context, reference, contributors, focal,
                new PhenomenonOperationRequest(operation, law.Phenomenon, magnitude, focal.SemanticState),
                maximumContributors);
        }

        private static DomainLaw Law(LawExpression expression, LawPhenomenon phenomenon)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, LawTerritoryPrinciple.Interference, out law, out validation))
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
