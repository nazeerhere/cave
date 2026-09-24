using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for pure Catalysis resolution.</summary>
    public static class CatalysisEvaluationVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyHeatHigh(out failure)
                && VerifyHeatLow(out failure)
                && VerifyNoExactValueCopying(out failure)
                && VerifyNonTriggers(out failure)
                && VerifyPhenomenonEntryBoundaries(out failure)
                && VerifyRecipientEligibilityAndProtection(out failure)
                && VerifyLawEligibility(out failure)
                && VerifyImmutabilityRecursionAndDeterminism(out failure)
                && VerifyInvalidSource(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyHeatHigh(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("A", LawPhenomenon.Heat, 1f);
            CatalysisEvaluationResult result = Evaluate(law, source, 2f,
                Carrier("B", LawPhenomenon.Heat, 0f),
                Carrier("C", LawPhenomenon.Heat, 1f),
                Carrier("D", LawPhenomenon.Heat, -3f),
                Carrier("E", LawPhenomenon.Heat, 3f));

            return Expect(result.Succeeded
                && result.TriggerDetected
                && result.TriggerDestinationRegion == PhenomenonSemanticRegion.High
                && result.EligibleRecipientCount == 2
                && result.ModifiedRecipientCount == 2
                && result.RecipientResults[0].GeneratedOperation == PhenomenonOperationKind.Add
                && Approximately(result.RecipientResults[0].GeneratedMagnitude, 2f)
                && Approximately(result.RecipientResults[0].After.SemanticValue, 2f)
                && Approximately(result.RecipientResults[1].GeneratedMagnitude, 1f)
                && Approximately(result.RecipientResults[1].After.SemanticValue, 2f)
                && result.RecipientResults[2].RejectionReason == CatalysisRecipientRejectionReason.AlreadyLow
                && result.RecipientResults[3].RejectionReason == CatalysisRecipientRejectionReason.AlreadyHigh,
                "Heat Regular-to-High Catalysis did not use the High entry boundary.", out failure);
        }

        private static bool VerifyHeatLow(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("A", LawPhenomenon.Heat, -1f);
            CatalysisEvaluationResult result = Evaluate(law, source, -2f,
                Carrier("B", LawPhenomenon.Heat, 0f),
                Carrier("C", LawPhenomenon.Heat, 1f),
                Carrier("D", LawPhenomenon.Heat, -3f),
                Carrier("E", LawPhenomenon.Heat, 3f));

            return Expect(result.Succeeded
                && result.TriggerDetected
                && result.TriggerDestinationRegion == PhenomenonSemanticRegion.Low
                && result.ModifiedRecipientCount == 2
                && result.RecipientResults[0].GeneratedOperation == PhenomenonOperationKind.Remove
                && Approximately(result.RecipientResults[0].GeneratedMagnitude, 2f)
                && Approximately(result.RecipientResults[0].After.SemanticValue, -2f)
                && Approximately(result.RecipientResults[1].GeneratedMagnitude, 3f)
                && Approximately(result.RecipientResults[1].After.SemanticValue, -2f)
                && result.RecipientResults[2].RejectionReason == CatalysisRecipientRejectionReason.AlreadyLow
                && result.RecipientResults[3].RejectionReason == CatalysisRecipientRejectionReason.AlreadyHigh,
                "Heat Regular-to-Low Catalysis did not use the Low entry boundary.", out failure);
        }

        private static bool VerifyNoExactValueCopying(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            CatalysisEvaluationResult high = Evaluate(law, Carrier("A", LawPhenomenon.Heat, 1f), 4f,
                Carrier("B", LawPhenomenon.Heat, 0f));
            CatalysisEvaluationResult low = Evaluate(law, Carrier("A", LawPhenomenon.Heat, -1f), -4f,
                Carrier("B", LawPhenomenon.Heat, 1f));

            return Expect(high.Succeeded
                && Approximately(high.SourceAfter.SemanticValue, 4f)
                && Approximately(high.RecipientResults[0].After.SemanticValue, 2f)
                && low.Succeeded
                && Approximately(low.SourceAfter.SemanticValue, -4f)
                && Approximately(low.RecipientResults[0].After.SemanticValue, -2f),
                "Catalysis copied the source scalar instead of entering the destination region boundary.", out failure);
        }

        private static bool VerifyNonTriggers(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot recipient = Carrier("B", LawPhenomenon.Heat, 0f);
            CatalysisEvaluationResult lowHigh = Evaluate(law, Carrier("A", LawPhenomenon.Heat, -3f), 3f, recipient);
            CatalysisEvaluationResult highLow = Evaluate(law, Carrier("A", LawPhenomenon.Heat, 3f), -3f, recipient);
            CatalysisEvaluationResult regularRegular = Evaluate(law, Carrier("A", LawPhenomenon.Heat, 0f), 1f, recipient);
            CatalysisEvaluationResult lowRegular = Evaluate(law, Carrier("A", LawPhenomenon.Heat, -2f), 0f, recipient);
            CatalysisEvaluationResult highRegular = Evaluate(law, Carrier("A", LawPhenomenon.Heat, 2f), 0f, recipient);

            return Expect(AllValidNoTrigger(lowHigh, highLow, regularRegular, lowRegular, highRegular),
                "Catalysis triggered for a crossing other than Regular-to-High or Regular-to-Low.", out failure);
        }

        private static bool VerifyPhenomenonEntryBoundaries(out string failure)
        {
            CatalysisEvaluationResult flow = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Flow),
                Carrier("A", LawPhenomenon.Flow, 2f), 4f, Carrier("B", LawPhenomenon.Flow, 2f));
            CatalysisEvaluationResult phase = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Phase),
                Carrier("A", LawPhenomenon.Phase, 3f), 4f, Carrier("B", LawPhenomenon.Phase, 2f));
            CatalysisEvaluationResult resonance = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Resonance),
                Carrier("A", LawPhenomenon.Resonance, 2f), 4f, Carrier("B", LawPhenomenon.Resonance, 2f));
            CatalysisEvaluationResult mass = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Mass),
                Carrier("A", LawPhenomenon.Mass, 1.1f), 1.3f, Carrier("B", LawPhenomenon.Mass, 1f));
            CatalysisEvaluationResult potential = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Potential),
                Carrier("A", LawPhenomenon.Potential, -1f), -3f, Carrier("B", LawPhenomenon.Potential, 0f));
            CatalysisEvaluationResult order = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Order),
                Carrier("A", LawPhenomenon.Order, 1f), -2f, Carrier("B", LawPhenomenon.Order, 1f));

            return Expect(AfterValue(flow, 3f)
                && AfterValue(phase, 4f)
                && AfterValue(resonance, 3f)
                && AfterValue(mass, 1.25f)
                && AfterValue(potential, -2f)
                && AfterValue(order, -2f),
                "Catalysis did not obtain a phenomenon's canonical entry boundary from semantic profiles.", out failure);
        }

        private static bool VerifyRecipientEligibilityAndProtection(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("A", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 0f);
            CatalysisEvaluationResult result = Evaluate(law, source, 2f,
                b,
                Carrier("C", LawPhenomenon.Potential, 0f),
                source,
                Carrier("B", LawPhenomenon.Heat, 1f),
                Carrier("D", LawPhenomenon.Heat, -3f),
                Carrier("E", LawPhenomenon.Heat, 3f));

            CatalysisEvaluationResult lowProtection = Evaluate(law,
                Carrier("A", LawPhenomenon.Heat, -1f), -2f,
                Carrier("B", LawPhenomenon.Heat, 3f));

            return Expect(result.Succeeded
                && result.ModifiedRecipientCount == 1
                && result.RecipientResults[0].Succeeded
                && result.RecipientResults[1].RejectionReason == CatalysisRecipientRejectionReason.PhenomenonMismatch
                && result.RecipientResults[2].RejectionReason == CatalysisRecipientRejectionReason.SourceReintroduced
                && result.RecipientResults[3].RejectionReason == CatalysisRecipientRejectionReason.DuplicateRecipient
                && result.RecipientResults[4].RejectionReason == CatalysisRecipientRejectionReason.AlreadyLow
                && result.RecipientResults[5].RejectionReason == CatalysisRecipientRejectionReason.AlreadyHigh
                && lowProtection.RecipientResults[0].RejectionReason == CatalysisRecipientRejectionReason.AlreadyHigh,
                "Catalysis did not preserve recipient identity, phenomenon, and protected-region eligibility rules.", out failure);
        }

        private static bool VerifyLawEligibility(out string failure)
        {
            PhenomenonCarrierSnapshot source = Carrier("A", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot recipient = Carrier("B", LawPhenomenon.Heat, 0f);
            DomainLaw frenzy = Law(LawExpression.Frenzy, LawPhenomenon.Heat);
            CatalysisEvaluationResult active = Evaluate(frenzy, source, 2f,
                new LawExpressionContext(true, LawExpression.Frenzy, true), recipient);
            CatalysisEvaluationResult inactive = Evaluate(frenzy, source, 2f,
                new LawExpressionContext(true, LawExpression.Frenzy, false), recipient);
            CatalysisEvaluationResult mismatch = Evaluate(Law(LawExpression.Projectile, LawPhenomenon.Heat), source, 2f,
                new LawExpressionContext(true, LawExpression.Frenzy, true), recipient);

            return Expect(active.Succeeded && active.TriggerDetected
                && !inactive.Succeeded
                && inactive.RejectionReason == CatalysisEvaluationRejectionReason.FrenzyInactive
                && !mismatch.Succeeded
                && mismatch.RejectionReason == CatalysisEvaluationRejectionReason.ExpressionMismatch,
                "Catalysis did not reuse Law expression eligibility exactly.", out failure);
        }

        private static bool VerifyImmutabilityRecursionAndDeterminism(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot source = Carrier("A", LawPhenomenon.Heat, 1f);
            PhenomenonCarrierSnapshot b = Carrier("B", LawPhenomenon.Heat, 0f);
            PhenomenonCarrierSnapshot c = Carrier("C", LawPhenomenon.Heat, 1f);
            CatalysisEvaluationRequest request = new CatalysisEvaluationRequest(
                law, new LawExpressionContext(true, LawExpression.Projectile, false),
                new PhenomenonCarrierTopology(source, new[] { b, c }), source.SemanticState,
                Snapshot(LawPhenomenon.Heat, 2f));
            CatalysisEvaluationResult first = CatalysisEvaluator.Evaluate(request);
            CatalysisEvaluationResult second = CatalysisEvaluator.Evaluate(request);

            return Expect(first.Succeeded
                && first.RecipientResults.Count == 2
                && first.ModifiedRecipientCount == 2
                && Approximately(source.SemanticState.SemanticValue, 1f)
                && Approximately(b.SemanticState.SemanticValue, 0f)
                && Approximately(c.SemanticState.SemanticValue, 1f)
                && first.RecipientResults[0].CarrierId.Equals(second.RecipientResults[0].CarrierId)
                && first.RecipientResults[1].CarrierId.Equals(second.RecipientResults[1].CarrierId)
                && Approximately(first.RecipientResults[0].After.SemanticValue, second.RecipientResults[0].After.SemanticValue)
                && first.RecipientResults[0].BaseResult != second.RecipientResults[0].BaseResult,
                "Catalysis mutated input snapshots, produced non-deterministic proposals, or recursively expanded recipients.", out failure);
        }

        private static bool VerifyInvalidSource(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat);
            PhenomenonCarrierSnapshot recipient = Carrier("B", LawPhenomenon.Heat, 0f);
            CatalysisEvaluationResult missingSource = CatalysisEvaluator.Evaluate(new CatalysisEvaluationRequest(
                law, new LawExpressionContext(true, LawExpression.Projectile, false),
                new PhenomenonCarrierTopology(null, new[] { recipient }),
                Snapshot(LawPhenomenon.Heat, 1f), Snapshot(LawPhenomenon.Heat, 2f)));
            CatalysisEvaluationResult mismatch = CatalysisEvaluator.Evaluate(new CatalysisEvaluationRequest(
                law, new LawExpressionContext(true, LawExpression.Projectile, false),
                new PhenomenonCarrierTopology(Carrier("A", LawPhenomenon.Heat, 0f), new[] { recipient }),
                Snapshot(LawPhenomenon.Heat, 1f), Snapshot(LawPhenomenon.Heat, 2f)));

            return Expect(!missingSource.Succeeded
                && missingSource.RejectionReason == CatalysisEvaluationRejectionReason.SourceCarrierUnavailable
                && !mismatch.Succeeded
                && mismatch.RejectionReason == CatalysisEvaluationRejectionReason.SourceSnapshotMismatch,
                "Catalysis did not reject structurally invalid source evidence deterministically.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string lawFailure;
            string semanticsFailure;
            string operationsFailure;
            string lawEvaluationFailure;
            string topologyFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            bool operations = PhenomenonOperationsVerification.TryRunAll(out operationsFailure);
            bool lawEvaluation = DomainLawEvaluationVerification.TryRunAll(out lawEvaluationFailure);
            bool topology = PhenomenonTopologyVerification.TryRunAll(out topologyFailure);
            return Expect(laws && semantics && operations && lawEvaluation && topology,
                "Earlier Domain verification regressed: law=" + lawFailure
                + "; semantics=" + semanticsFailure
                + "; operations=" + operationsFailure
                + "; evaluation=" + lawEvaluationFailure
                + "; topology=" + topologyFailure,
                out failure);
        }

        private static bool AllValidNoTrigger(params CatalysisEvaluationResult[] results)
        {
            for (int index = 0; index < results.Length; index++)
            {
                if (!results[index].Succeeded || results[index].TriggerDetected || results[index].RecipientResults.Count != 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AfterValue(CatalysisEvaluationResult result, float expected)
        {
            return result.Succeeded
                && result.TriggerDetected
                && result.ModifiedRecipientCount == 1
                && result.RecipientResults[0].After != null
                && Approximately(result.RecipientResults[0].After.SemanticValue, expected);
        }

        private static CatalysisEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot source,
            float sourceAfterValue,
            params PhenomenonCarrierSnapshot[] recipients)
        {
            return Evaluate(law, source, sourceAfterValue,
                new LawExpressionContext(true, law.Expression, law.Expression == LawExpression.Frenzy), recipients);
        }

        private static CatalysisEvaluationResult Evaluate(
            DomainLaw law,
            PhenomenonCarrierSnapshot source,
            float sourceAfterValue,
            LawExpressionContext context,
            params PhenomenonCarrierSnapshot[] recipients)
        {
            return CatalysisEvaluator.Evaluate(new CatalysisEvaluationRequest(
                law, context, new PhenomenonCarrierTopology(source, recipients), source.SemanticState,
                Snapshot(law.Phenomenon, sourceAfterValue)));
        }

        private static DomainLaw Law(LawExpression expression, LawPhenomenon phenomenon)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, LawTerritoryPrinciple.Catalysis, out law, out validation))
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
