using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for pure single-Law evaluation.</summary>
    public static class DomainLawEvaluationVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyProjectileReversal(out failure)
                && VerifyOrderReversal(out failure)
                && VerifyExpressionEligibility(out failure)
                && VerifyFrenzyEligibility(out failure)
                && VerifyPhenomenonMismatch(out failure)
                && VerifyUnimplementedTerritory(out failure)
                && VerifyTransferReversal(out failure)
                && VerifyImmutability(out failure)
                && VerifyInvalidInputs(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyProjectileReversal(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal);
            LawExpressionContext context = new LawExpressionContext(true, LawExpression.Projectile, false);
            LawEvaluationResult add = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law, Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 2f, 0f), context));
            LawEvaluationResult remove = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law, Operation(PhenomenonOperationKind.Remove, LawPhenomenon.Heat, 1f, 2f), context));
            return Expect(add.Succeeded
                && add.Eligible
                && add.TerritoryTransformed
                && add.EffectiveOperation == PhenomenonOperationKind.Remove
                && Approximately(add.BaseResult.TargetAfter.SemanticValue, -2f)
                && add.BaseResult.TargetAfter.Region == PhenomenonSemanticRegion.Low
                && remove.Succeeded
                && remove.EffectiveOperation == PhenomenonOperationKind.Add
                && Approximately(remove.BaseResult.TargetAfter.SemanticValue, 3f)
                && remove.BaseResult.TargetAfter.Region == PhenomenonSemanticRegion.High,
                "Projectile Reversal did not invert Add/Remove before base resolution.", out failure);
        }

        private static bool VerifyOrderReversal(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Order, LawTerritoryPrinciple.Reversal);
            LawEvaluationResult result = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                Operation(PhenomenonOperationKind.Add, LawPhenomenon.Order, 2f, -1f),
                new LawExpressionContext(true, LawExpression.Projectile, false)));
            return Expect(result.Succeeded
                && result.EffectiveOperation == PhenomenonOperationKind.Remove
                && Approximately(result.BaseResult.TargetAfter.SemanticValue, -3f)
                && result.BaseResult.TargetAfter.Region == PhenomenonSemanticRegion.Low,
                "Order Reversal did not preserve signed base semantics.", out failure);
        }

        private static bool VerifyExpressionEligibility(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal);
            LawEvaluationResult mismatch = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f),
                new LawExpressionContext(true, LawExpression.Frenzy, true)));
            return Expect(!mismatch.Eligible
                && !mismatch.Succeeded
                && mismatch.RejectionReason == LawEvaluationRejectionReason.ExpressionMismatch,
                "A Law accepted expression evidence for a different expression.", out failure);
        }

        private static bool VerifyFrenzyEligibility(out string failure)
        {
            DomainLaw law = Law(LawExpression.Frenzy, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal);
            PhenomenonOperationRequest original = Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f);
            LawEvaluationResult active = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law, original, new LawExpressionContext(true, LawExpression.Frenzy, true)));
            LawEvaluationResult inactive = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law, original, new LawExpressionContext(true, LawExpression.Frenzy, false)));
            return Expect(active.Succeeded
                && active.Eligible
                && inactive.Eligible == false
                && !inactive.Succeeded
                && inactive.RejectionReason == LawEvaluationRejectionReason.FrenzyInactive
                && original.Operation == PhenomenonOperationKind.Add,
                "Frenzy eligibility did not require active Frenzy or mutated the base intent.", out failure);
        }

        private static bool VerifyPhenomenonMismatch(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal);
            LawEvaluationResult result = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                Operation(PhenomenonOperationKind.Add, LawPhenomenon.Potential, 1f, 0f),
                new LawExpressionContext(true, LawExpression.Projectile, false)));
            return Expect(!result.Eligible
                && result.RejectionReason == LawEvaluationRejectionReason.PhenomenonMismatch,
                "Law evaluation crossed phenomena implicitly.", out failure);
        }

        private static bool VerifyUnimplementedTerritory(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Propagation);
            LawEvaluationResult result = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f),
                new LawExpressionContext(true, LawExpression.Projectile, false)));
            return Expect(result.Eligible
                && !result.Succeeded
                && result.RejectionReason == LawEvaluationRejectionReason.TerritoryBehaviorNotImplemented
                && !result.HasEffectiveOperation,
                "Unimplemented Territory behavior was silently treated as resolved.", out failure);
        }

        private static bool VerifyTransferReversal(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal);
            PhenomenonTransferRequest transfer = new PhenomenonTransferRequest(
                LawPhenomenon.Heat,
                1f,
                Snapshot(LawPhenomenon.Heat, 2f),
                Snapshot(LawPhenomenon.Heat, 0f));
            LawEvaluationResult result = DomainLawEvaluator.EvaluateTransfer(new LawTransferEvaluationRequest(
                law,
                transfer,
                new LawExpressionContext(true, LawExpression.Projectile, false)));
            return Expect(result.Eligible
                && !result.Succeeded
                && result.RejectionReason == LawEvaluationRejectionReason.NoDefinedInverse
                && !result.HasEffectiveOperation,
                "Reversal invented an unsupported Transfer inverse.", out failure);
        }

        private static bool VerifyImmutability(out string failure)
        {
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal);
            PhenomenonOperationRequest original = Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 2f, 0f);
            PhenomenonResolutionResult originalBase = PhenomenonOperationResolver.Resolve(original);
            LawEvaluationResult evaluated = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                original,
                new LawExpressionContext(true, LawExpression.Projectile, false)));
            return Expect(evaluated.Succeeded
                && original.Operation == PhenomenonOperationKind.Add
                && original.Phenomenon == LawPhenomenon.Heat
                && Approximately(original.Magnitude, 2f)
                && law.Expression == LawExpression.Projectile
                && law.Phenomenon == LawPhenomenon.Heat
                && law.TerritoryPrinciple == LawTerritoryPrinciple.Reversal
                && originalBase.Succeeded
                && Approximately(originalBase.TargetAfter.SemanticValue, 2f)
                && evaluated.BaseResult != originalBase
                && Approximately(evaluated.BaseResult.TargetAfter.SemanticValue, -2f),
                "Law evaluation mutated the original request, Law, or base proposal.", out failure);
        }

        private static bool VerifyInvalidInputs(out string failure)
        {
            PhenomenonOperationRequest operation = Operation(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, 0f);
            LawEvaluationResult nullLaw = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                null,
                operation,
                new LawExpressionContext(true, LawExpression.Projectile, false)));
            DomainLaw law = Law(LawExpression.Projectile, LawPhenomenon.Heat, LawTerritoryPrinciple.Reversal);
            LawEvaluationResult missingExpression = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                operation,
                new LawExpressionContext(false, LawExpression.Projectile, false)));
            LawEvaluationResult invalidExpression = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                operation,
                new LawExpressionContext(true, (LawExpression)999, false)));
            LawEvaluationResult missingBaseRequest = DomainLawEvaluator.Evaluate(new LawEvaluationRequest(
                law,
                null,
                new LawExpressionContext(true, LawExpression.Projectile, false)));
            return Expect(nullLaw.RejectionReason == LawEvaluationRejectionReason.InvalidLaw
                && missingExpression.RejectionReason == LawEvaluationRejectionReason.ExpressionUnavailable
                && invalidExpression.RejectionReason == LawEvaluationRejectionReason.InvalidExpressionContext
                && missingBaseRequest.RejectionReason == LawEvaluationRejectionReason.InvalidBaseRequest,
                "Invalid Law or expression context did not reject deterministically.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string lawFailure;
            string semanticsFailure;
            string operationsFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            bool operations = PhenomenonOperationsVerification.TryRunAll(out operationsFailure);
            return Expect(laws && semantics && operations,
                "Earlier Domain verification regressed: law=" + lawFailure
                + "; semantics=" + semanticsFailure
                + "; operations=" + operationsFailure,
                out failure);
        }

        private static DomainLaw Law(
            LawExpression expression,
            LawPhenomenon phenomenon,
            LawTerritoryPrinciple principle)
        {
            DomainLaw law;
            LawValidationResult validation;
            if (!DomainLaw.TryCreate(expression, phenomenon, principle, out law, out validation))
            {
                throw new InvalidOperationException("Verification could not create Law: " + validation.RejectionReason);
            }

            return law;
        }

        private static PhenomenonOperationRequest Operation(
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            float value)
        {
            return new PhenomenonOperationRequest(operation, phenomenon, magnitude, Snapshot(phenomenon, value));
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
