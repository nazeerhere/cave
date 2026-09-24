using System;
using System.Collections.Generic;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for bounded Propagation topology.</summary>
    public static class PhenomenonTopologyVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyAddAndRemove(out failure)
                && VerifySourceAndDuplicateExclusion(out failure)
                && VerifyRecipientBounds(out failure)
                && VerifyRecipientMismatchSkips(out failure)
                && VerifyEligibilityAndTransferRejection(out failure)
                && VerifyImmutabilityAndDeterminism(out failure)
                && VerifyInvalidInputs(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyAddAndRemove(out string failure)
        {
            DomainLaw law = PropagationLaw(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("source", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot recipientA = Carrier("A", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot recipientB = Carrier("B", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot recipientC = Carrier("C", LawPhenomenon.Heat, -3f);
            PhenomenonCarrierTopology topology = Topology(source, recipientA, recipientB, recipientC);
            LawExpressionContext context = new LawExpressionContext(true, LawExpression.Projectile, false);

            PropagationEvaluationResult add = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                law, Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 99f), context, topology, 3));
            PropagationEvaluationResult remove = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                law, Operation(PhenomenonOperationKind.Remove, LawPhenomenon.Heat, 1f, 99f), context, topology, 3));

            return Expect(add.Succeeded
                && add.ResolvedRecipientCount == 3
                && add.SourceCarrierId.Equals(source.CarrierId)
                && add.RecipientResults[0].CarrierId.Equals(recipientA.CarrierId)
                && add.RecipientResults[0].After.Region == PhenomenonSemanticRegion.Regular
                && Approximately(add.RecipientResults[0].After.SemanticValue, 1f)
                && Approximately(add.RecipientResults[1].After.SemanticValue, 2f)
                && add.RecipientResults[1].After.Region == PhenomenonSemanticRegion.High
                && Approximately(add.RecipientResults[2].After.SemanticValue, -2f)
                && add.RecipientResults[2].After.Region == PhenomenonSemanticRegion.Low
                && remove.Succeeded
                && Approximately(remove.RecipientResults[0].After.SemanticValue, -1f)
                && Approximately(remove.RecipientResults[1].After.SemanticValue, 0f)
                && Approximately(remove.RecipientResults[2].After.SemanticValue, -4f),
                "Propagation did not repeat Add/Remove independently against each recipient state.", out failure);
        }

        private static bool VerifySourceAndDuplicateExclusion(out string failure)
        {
            DomainLaw law = PropagationLaw(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("source", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot firstB = Carrier("B", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot firstC = Carrier("C", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot duplicateB = Carrier("B", LawPhenomenon.Heat, 10f);
            PhenomenonCarrierSnapshot firstD = Carrier("D", LawPhenomenon.Heat, 0f);
            PropagationEvaluationResult result = Evaluate(law, source, 4, source, firstB, firstC, duplicateB, firstD);

            return Expect(result.Succeeded
                && result.ResolvedRecipientCount == 3
                && result.SkippedRecipientCount == 2
                && result.RecipientResults.Count == 5
                && result.RecipientResults[0].RejectionReason == PropagationRecipientRejectionReason.SourceReintroduced
                && result.RecipientResults[1].Succeeded
                && result.RecipientResults[2].Succeeded
                && result.RecipientResults[3].RejectionReason == PropagationRecipientRejectionReason.DuplicateRecipient
                && result.RecipientResults[4].Succeeded
                && result.RecipientResults[1].CarrierId.Value == "B"
                && result.RecipientResults[2].CarrierId.Value == "C"
                && result.RecipientResults[4].CarrierId.Value == "D"
                && Approximately(result.RecipientResults[1].After.SemanticValue, 1f)
                && result.RecipientResults[3].BaseResult == null,
                "Propagation did not exclude the source and duplicate downstream identities in stable order.", out failure);
        }

        private static bool VerifyRecipientBounds(out string failure)
        {
            DomainLaw law = PropagationLaw(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("source", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot a = Carrier("A", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot d = Carrier("D", LawPhenomenon.Heat, 0f);
            PropagationEvaluationResult capped = Evaluate(law, source, 2, a, b, c, d);
            PropagationEvaluationResult zero = Evaluate(law, source, 0, a, b);
            PropagationEvaluationResult negative = Evaluate(law, source, -1, a);

            return Expect(capped.Succeeded
                && capped.ResolvedRecipientCount == 2
                && capped.RecipientResults[0].Succeeded
                && capped.RecipientResults[1].Succeeded
                && capped.RecipientResults[2].RejectionReason == PropagationRecipientRejectionReason.RecipientLimitReached
                && capped.RecipientResults[3].RejectionReason == PropagationRecipientRejectionReason.RecipientLimitReached
                && zero.Succeeded
                && zero.ResolvedRecipientCount == 0
                && zero.SkippedRecipientCount == 2
                && zero.RecipientResults[0].RejectionReason == PropagationRecipientRejectionReason.RecipientLimitReached
                && !negative.Succeeded
                && negative.RejectionReason == PropagationEvaluationRejectionReason.NegativeRecipientLimit,
                "Propagation recipient bounds did not reject negatives or deterministically limit recipients.", out failure);
        }

        private static bool VerifyRecipientMismatchSkips(out string failure)
        {
            DomainLaw law = PropagationLaw(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("source", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot validA = Carrier("A", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot mismatch = Carrier("B", LawPhenomenon.Potential, 0f);
            PhenomenonCarrierSnapshot validC = Carrier("C", LawPhenomenon.Heat, 0f);
            PropagationEvaluationResult result = Evaluate(law, source, 3, validA, mismatch, validC);

            return Expect(result.Succeeded
                && result.ResolvedRecipientCount == 2
                && result.SkippedRecipientCount == 1
                && result.RecipientResults[0].Succeeded
                && result.RecipientResults[1].RejectionReason == PropagationRecipientRejectionReason.PhenomenonMismatch
                && result.RecipientResults[2].Succeeded
                && Approximately(result.RecipientResults[2].After.SemanticValue, 1f),
                "A mismatched recipient did not skip independently while valid recipients resolved.", out failure);
        }

        private static bool VerifyEligibilityAndTransferRejection(out string failure)
        {
            DomainLaw projectileLaw = PropagationLaw(LawExpression.Projectile, LawPhenomenon.Heat);
            DomainLaw frenzyLaw = PropagationLaw(LawExpression.Frenzy, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("source", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot recipient = Carrier("A", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierTopology topology = Topology(source, recipient);
            PropagationEvaluationResult mismatch = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                projectileLaw,
                Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f),
                new LawExpressionContext(true, LawExpression.Frenzy, true), topology, 1));
            PropagationEvaluationResult frenzyInactive = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                frenzyLaw,
                Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f),
                new LawExpressionContext(true, LawExpression.Frenzy, false), topology, 1));
            PropagationEvaluationResult transfer = PropagationEvaluator.EvaluateTransfer(new PropagationTransferEvaluationRequest(
                projectileLaw,
                new PhenomenonTransferRequest(LawPhenomenon.Heat, 1f,
                    Snapshot(LawPhenomenon.Heat, 1f), Snapshot(LawPhenomenon.Heat, 0f)),
                new LawExpressionContext(true, LawExpression.Projectile, false), topology, 1));

            return Expect(!mismatch.Succeeded
                && mismatch.RejectionReason == PropagationEvaluationRejectionReason.ExpressionMismatch
                && mismatch.RecipientResults.Count == 0
                && !frenzyInactive.Succeeded
                && frenzyInactive.RejectionReason == PropagationEvaluationRejectionReason.FrenzyInactive
                && !transfer.Succeeded
                && transfer.RejectionReason == PropagationEvaluationRejectionReason.OperationNotSupportedByTerritory,
                "Propagation did not reuse Law eligibility or explicitly reject Transfer.", out failure);
        }

        private static bool VerifyImmutabilityAndDeterminism(out string failure)
        {
            DomainLaw law = PropagationLaw(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("source", LawPhenomenon.Heat, 4f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, -1f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 2f);
            PhenomenonCarrierTopology topology = Topology(source, b, c);
            PropagationEvaluationRequest request = new PropagationEvaluationRequest(
                law, Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 500f),
                new LawExpressionContext(true, LawExpression.Projectile, false), topology, 2);
            PropagationEvaluationResult first = PropagationEvaluator.Evaluate(request);
            PropagationEvaluationResult second = PropagationEvaluator.Evaluate(request);

            return Expect(first.Succeeded
                && second.Succeeded
                && Approximately(source.SemanticState.SemanticValue, 4f)
                && Approximately(b.SemanticState.SemanticValue, -1f)
                && Approximately(c.SemanticState.SemanticValue, 2f)
                && first.RecipientResults.Count == 2
                && first.RecipientResults[0].CarrierId.Equals(second.RecipientResults[0].CarrierId)
                && first.RecipientResults[1].CarrierId.Equals(second.RecipientResults[1].CarrierId)
                && Approximately(first.RecipientResults[0].After.SemanticValue, second.RecipientResults[0].After.SemanticValue)
                && Approximately(first.RecipientResults[1].After.SemanticValue, second.RecipientResults[1].After.SemanticValue)
                && first.RecipientResults[0].BaseResult != second.RecipientResults[0].BaseResult,
                "Propagation mutated snapshots, reused proposals, or produced non-deterministic ordered results.", out failure);
        }

        private static bool VerifyInvalidInputs(out string failure)
        {
            DomainLaw law = PropagationLaw(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("source", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot recipient = Carrier("A", LawPhenomenon.Heat, 0f);
            LawExpressionContext context = new LawExpressionContext(true, LawExpression.Projectile, false);
            PropagationEvaluationResult missingSource = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                law, Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f), context,
                new PhenomenonCarrierTopology(null, new[] { recipient }), 1));
            PropagationEvaluationResult sourceMismatch = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                law, Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f), context,
                Topology(Carrier("source", LawPhenomenon.Potential, 0f), recipient), 1));
            PropagationEvaluationResult invalidBase = PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                law, new PhenomenonOperationRequest(PhenomenonOperationKind.Add, LawPhenomenon.Heat, -1f,
                    Snapshot(LawPhenomenon.Heat, 0f)), context, Topology(source, recipient), 1));

            return Expect(!missingSource.Succeeded
                && missingSource.RejectionReason == PropagationEvaluationRejectionReason.SourceCarrierUnavailable
                && !sourceMismatch.Succeeded
                && sourceMismatch.RejectionReason == PropagationEvaluationRejectionReason.SourcePhenomenonMismatch
                && !invalidBase.Succeeded
                && invalidBase.RejectionReason == PropagationEvaluationRejectionReason.InvalidBaseRequest,
                "Propagation did not reject invalid transaction-level inputs explicitly.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string lawFailure;
            string semanticsFailure;
            string operationsFailure;
            string evaluationFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            bool operations = PhenomenonOperationsVerification.TryRunAll(out operationsFailure);
            bool evaluation = DomainLawEvaluationVerification.TryRunAll(out evaluationFailure);
            bool passed = laws && semantics && operations && evaluation;
            return Expect(passed,
                "Earlier Domain verification regressed: law=" + lawFailure
                + "; semantics=" + semanticsFailure
                + "; operations=" + operationsFailure
                + "; evaluation=" + evaluationFailure,
                out failure);
        }

        private static PropagationEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot source,
            int maximumRecipients,
            params PhenomenonCarrierSnapshot[] recipients)
        {
            return PropagationEvaluator.Evaluate(new PropagationEvaluationRequest(
                law,
                Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f),
                new LawExpressionContext(true, law.Expression, law.Expression == LawExpression.Frenzy),
                Topology(source, recipients),
                maximumRecipients));
        }

        private static DomainLaw PropagationLaw(LawExpression expression, LawPhenomenon phenomenon)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, LawTerritoryPrinciple.Propagation, out law, out validation))
            {
                throw new InvalidOperationException("Verification could not create Law: " + validation.RejectionReason);
            }
            return law;
        }

        private static PhenomenonCarrierTopology Topology(
            PhenomenonCarrierSnapshot source,
            params PhenomenonCarrierSnapshot[] recipients)
        {
            return new PhenomenonCarrierTopology(source, recipients);
        }

        private static PhenomenonCarrierSnapshot Carrier(string id, LawPhenomenon phenomenon, float value)
        {
            return new PhenomenonCarrierSnapshot(new PhenomenonCarrierId(id), phenomenon, Snapshot(phenomenon, value));
        }

        private static PhenomenonOperationRequest Operation(
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            float targetValue)
        {
            return new PhenomenonOperationRequest(operation, phenomenon, magnitude, Snapshot(phenomenon, targetValue));
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
