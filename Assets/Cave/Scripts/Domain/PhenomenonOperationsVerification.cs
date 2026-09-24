using System;

namespace Cave.Domain
{
    /// <summary>Runtime-independent deterministic coverage for pure base phenomenon operations.</summary>
    public static class PhenomenonOperationsVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            return VerifyAdd(out failure)
                && VerifyRemove(out failure)
                && VerifySignedCrossings(out failure)
                && VerifyTransfer(out failure)
                && VerifyImmutability(out failure)
                && VerifyInvalidInput(out failure)
                && VerifyMass(out failure)
                && VerifyPhaseAndResonance(out failure)
                && VerifyRegression(out failure);
        }

        private static bool VerifyAdd(out string failure)
        {
            PhenomenonResolutionResult heatHigh = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Heat, 2f, 0f);
            PhenomenonResolutionResult heatRegular = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, -2f);
            PhenomenonResolutionResult orderHigh = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Order, 4f, -2f);
            return Expect(SucceededAt(heatHigh, 2f, PhenomenonSemanticRegion.High)
                && SucceededAt(heatRegular, -1f, PhenomenonSemanticRegion.Regular)
                && SucceededAt(orderHigh, 2f, PhenomenonSemanticRegion.High),
                "Add did not produce the expected signed semantic results.", out failure);
        }

        private static bool VerifyRemove(out string failure)
        {
            PhenomenonResolutionResult heatLow = Resolve(
                PhenomenonOperationKind.Remove, LawPhenomenon.Heat, 2f, 0f);
            PhenomenonResolutionResult heatRegular = Resolve(
                PhenomenonOperationKind.Remove, LawPhenomenon.Heat, 1f, 2f);
            PhenomenonResolutionResult orderLow = Resolve(
                PhenomenonOperationKind.Remove, LawPhenomenon.Order, 4f, 2f);
            return Expect(SucceededAt(heatLow, -2f, PhenomenonSemanticRegion.Low)
                && SucceededAt(heatRegular, 1f, PhenomenonSemanticRegion.Regular)
                && SucceededAt(orderLow, -2f, PhenomenonSemanticRegion.Low),
                "Remove did not preserve signed arithmetic across baseline.", out failure);
        }

        private static bool VerifySignedCrossings(out string failure)
        {
            PhenomenonResolutionResult lowToHigh = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Heat, 6f, -3f);
            PhenomenonResolutionResult highToLow = Resolve(
                PhenomenonOperationKind.Remove, LawPhenomenon.Heat, 6f, 3f);
            return Expect(SucceededAt(lowToHigh, 3f, PhenomenonSemanticRegion.High)
                && lowToHigh.TargetRegionChanged
                && lowToHigh.TargetBefore.Region == PhenomenonSemanticRegion.Low
                && SucceededAt(highToLow, -3f, PhenomenonSemanticRegion.Low)
                && highToLow.TargetRegionChanged
                && highToLow.TargetBefore.Region == PhenomenonSemanticRegion.High,
                "Signed base arithmetic did not report direct Low/High transitions.", out failure);
        }

        private static bool VerifyTransfer(out string failure)
        {
            PhenomenonSemanticSnapshot heatSource = Snapshot(LawPhenomenon.Heat, 3f);
            PhenomenonSemanticSnapshot heatTarget = Snapshot(LawPhenomenon.Heat, 0f);
            PhenomenonResolutionResult heat = PhenomenonOperationResolver.ResolveTransfer(
                new PhenomenonTransferRequest(LawPhenomenon.Heat, 2f, heatSource, heatTarget));

            PhenomenonSemanticSnapshot potentialSource = Snapshot(LawPhenomenon.Potential, -2f);
            PhenomenonSemanticSnapshot potentialTarget = Snapshot(LawPhenomenon.Potential, 1f);
            PhenomenonResolutionResult potential = PhenomenonOperationResolver.ResolveTransfer(
                new PhenomenonTransferRequest(LawPhenomenon.Potential, 1f, potentialSource, potentialTarget));

            PhenomenonResolutionResult mismatch = PhenomenonOperationResolver.ResolveTransfer(
                new PhenomenonTransferRequest(LawPhenomenon.Heat, 1f, heatSource, potentialTarget));

            return Expect(heat.Succeeded
                && Approximately(heat.SourceAfter.SemanticValue, 1f)
                && heat.SourceAfter.Region == PhenomenonSemanticRegion.Regular
                && Approximately(heat.TargetAfter.SemanticValue, 2f)
                && heat.TargetAfter.Region == PhenomenonSemanticRegion.High
                && Approximately(heat.SourceDelta.Amount + heat.TargetDelta.Amount, 0f)
                && potential.Succeeded
                && Approximately(potential.SourceAfter.SemanticValue, -3f)
                && Approximately(potential.TargetAfter.SemanticValue, 2f)
                && Approximately(potential.SourceDelta.Amount + potential.TargetDelta.Amount, 0f)
                && !mismatch.Succeeded
                && mismatch.RejectionReason == PhenomenonResolutionRejectionReason.PhenomenonMismatch,
                "Transfer did not conserve signed values or reject cross-phenomenon input.", out failure);
        }

        private static bool VerifyImmutability(out string failure)
        {
            PhenomenonSemanticSnapshot source = Snapshot(LawPhenomenon.Heat, 3f);
            PhenomenonSemanticSnapshot target = Snapshot(LawPhenomenon.Heat, 0f);
            float sourceBefore = source.SemanticValue;
            float targetBefore = target.SemanticValue;
            PhenomenonResolutionResult result = PhenomenonOperationResolver.ResolveTransfer(
                new PhenomenonTransferRequest(LawPhenomenon.Heat, 2f, source, target));
            return Expect(result.Succeeded
                && Approximately(source.SemanticValue, sourceBefore)
                && Approximately(target.SemanticValue, targetBefore)
                && source.Region == PhenomenonSemanticRegion.High
                && target.Region == PhenomenonSemanticRegion.Regular,
                "Pure transfer mutated one of its input snapshots.", out failure);
        }

        private static bool VerifyInvalidInput(out string failure)
        {
            PhenomenonSemanticSnapshot heat = Snapshot(LawPhenomenon.Heat, 0f);
            PhenomenonResolutionResult negativeAdd = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Heat, -1f, 0f);
            PhenomenonResolutionResult negativeRemove = Resolve(
                PhenomenonOperationKind.Remove, LawPhenomenon.Heat, -1f, 0f);
            PhenomenonResolutionResult negativeTransfer = PhenomenonOperationResolver.ResolveTransfer(
                new PhenomenonTransferRequest(LawPhenomenon.Heat, -1f, heat, heat));
            PhenomenonResolutionResult invalidOperation = Resolve(
                (PhenomenonOperationKind)999, LawPhenomenon.Heat, 1f, 0f);
            PhenomenonResolutionResult invalidPhenomenon = PhenomenonOperationResolver.Resolve(
                new PhenomenonOperationRequest(
                    PhenomenonOperationKind.Add,
                    (LawPhenomenon)999,
                    1f,
                    heat));
            PhenomenonResolutionResult unavailableTarget = PhenomenonOperationResolver.Resolve(
                new PhenomenonOperationRequest(PhenomenonOperationKind.Add, LawPhenomenon.Heat, 1f, null));
            PhenomenonResolutionResult unavailableSource = PhenomenonOperationResolver.ResolveTransfer(
                new PhenomenonTransferRequest(LawPhenomenon.Heat, 1f, null, heat));
            PhenomenonSemanticSnapshot failedMass;
            PhenomenonSemanticResult failedMassResult;
            bool fabricatedMass = PhenomenonSemanticAdapters.TryFromMassRelativeToBaseline(
                1.4f, 0f, out failedMass, out failedMassResult);
            PhenomenonResolutionResult zero = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Heat, 0f, 0f);

            return Expect(IsRejected(negativeAdd, PhenomenonResolutionRejectionReason.NegativeMagnitude)
                && IsRejected(negativeRemove, PhenomenonResolutionRejectionReason.NegativeMagnitude)
                && IsRejected(negativeTransfer, PhenomenonResolutionRejectionReason.NegativeMagnitude)
                && IsRejected(invalidOperation, PhenomenonResolutionRejectionReason.InvalidOperation)
                && IsRejected(invalidPhenomenon, PhenomenonResolutionRejectionReason.InvalidPhenomenon)
                && IsRejected(unavailableTarget, PhenomenonResolutionRejectionReason.TargetStateUnavailable)
                && IsRejected(unavailableSource, PhenomenonResolutionRejectionReason.SourceStateUnavailable)
                && !fabricatedMass
                && failedMass == null
                && failedMassResult.RejectionReason == PhenomenonSemanticRejectionReason.InvalidNaturalMassBaseline
                && SucceededAt(zero, 0f, PhenomenonSemanticRegion.Regular)
                && Approximately(zero.TargetDelta.Amount, 0f),
                "Invalid operation input did not reject explicitly or zero was not an explicit no-op.", out failure);
        }

        private static bool VerifyMass(out string failure)
        {
            PhenomenonSemanticSnapshot mass = Snapshot(LawPhenomenon.Mass, 1.4f);
            PhenomenonResolutionResult result = PhenomenonOperationResolver.Resolve(
                new PhenomenonOperationRequest(PhenomenonOperationKind.Remove, LawPhenomenon.Mass, .3f, mass));
            return Expect(SucceededAt(result, 1.1f, PhenomenonSemanticRegion.Regular),
                "Normalized Mass did not resolve without inventing a baseline.", out failure);
        }

        private static bool VerifyPhaseAndResonance(out string failure)
        {
            PhenomenonResolutionResult phase = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Phase, 1f, 3f);
            PhenomenonResolutionResult resonance = Resolve(
                PhenomenonOperationKind.Add, LawPhenomenon.Resonance, 1f, 2f);
            return Expect(SucceededAt(phase, 4f, PhenomenonSemanticRegion.High)
                && SucceededAt(resonance, 3f, PhenomenonSemanticRegion.High),
                "Phase or Resonance base arithmetic did not stay independent from specialized effects.", out failure);
        }

        private static bool VerifyRegression(out string failure)
        {
            string lawFailure;
            string semanticsFailure;
            bool laws = DomainLawVerification.TryRunAll(out lawFailure);
            bool semantics = PhenomenonSemanticsVerification.TryRunAll(out semanticsFailure);
            return Expect(laws && semantics,
                "Sprint 1 or Sprint 2 verification regressed: law=" + lawFailure + "; semantics=" + semanticsFailure,
                out failure);
        }

        private static PhenomenonResolutionResult Resolve(
            PhenomenonOperationKind operation,
            LawPhenomenon phenomenon,
            float magnitude,
            float value)
        {
            return PhenomenonOperationResolver.Resolve(
                new PhenomenonOperationRequest(operation, phenomenon, magnitude, Snapshot(phenomenon, value)));
        }

        private static PhenomenonSemanticSnapshot Snapshot(LawPhenomenon phenomenon, float value)
        {
            PhenomenonSemanticSnapshot snapshot;
            PhenomenonSemanticResult result;
            if (!PhenomenonSemanticClassifier.TryCreateSnapshot(phenomenon, value, out snapshot, out result))
            {
                throw new InvalidOperationException("Verification could not prepare semantic state: " + result.RejectionReason);
            }

            return snapshot;
        }

        private static bool SucceededAt(
            PhenomenonResolutionResult result,
            float expectedValue,
            PhenomenonSemanticRegion expectedRegion)
        {
            return result != null
                && result.Succeeded
                && result.TargetAfter != null
                && Approximately(result.TargetAfter.SemanticValue, expectedValue)
                && result.TargetAfter.Region == expectedRegion;
        }

        private static bool IsRejected(
            PhenomenonResolutionResult result,
            PhenomenonResolutionRejectionReason expected)
        {
            return result != null && !result.Succeeded && result.RejectionReason == expected;
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
