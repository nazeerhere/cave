using Cave.Axioms;
using Cave.Axioms.Control;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Deterministic coverage for reactive cell selection and feedback timing.</summary>
    public static class AxiomReactiveStatusPresentationVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyNormalAndDirections(out failure)
                || !VerifySuccessAndFailureFeedback(out failure)
                || !VerifyStaleFeedbackAndNaturalResolution(out failure)
                || !VerifySemanticIsolationAndPhaseIdentity(out failure)
                || !VerifyHeatBurnConsolidation(out failure)
                || !VerifyAllEightGenericIdentities(out failure)
                || !VerifyFixedFootprintAndDeterminism(out failure)
                || !VerifyUniversalFootprintCountsAndAssets(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyNormalAndDirections(out string failure)
        {
            AxiomReactiveStatusPresentation model = new AxiomReactiveStatusPresentation();
            AxiomReactiveStatusVisual normal = model.Resolve(false, default(AxiomControlOpportunity), 0f);
            bool valid = !normal.HasOverlay
                && IsVisual(model, AxiomErrorKind.State, -1f, AxiomReactiveMarker.Up)
                && IsVisual(model, AxiomErrorKind.State, 1f, AxiomReactiveMarker.Down)
                && IsVisual(model, AxiomErrorKind.Rate, -1f, AxiomReactiveMarker.Up)
                && IsVisual(model, AxiomErrorKind.Rate, 1f, AxiomReactiveMarker.Down)
                && IsVisual(model, AxiomErrorKind.Acceleration, -1f, AxiomReactiveMarker.Up)
                && IsVisual(model, AxiomErrorKind.Acceleration, 1f, AxiomReactiveMarker.Down);
            failure = valid ? null : "Reactive Axiom cells did not map authoritative S/R/A opportunities to one frame and direction.";
            return valid;
        }

        private static bool VerifySuccessAndFailureFeedback(out string failure)
        {
            AxiomReactiveStatusPresentation model = new AxiomReactiveStatusPresentation();
            AxiomControlOpportunity rateDown = Opportunity(AxiomErrorKind.Rate, 1f);
            AxiomControlInterventionOutcome success = Outcome(1, true, AxiomErrorKind.Rate, 1f, 1f);
            bool firstSuccess = model.Observe(success, .4f);
            bool duplicateSuccess = model.Observe(success, .4f);
            AxiomReactiveStatusVisual successVisual = model.Resolve(true, rateDown, 1.1f);
            AxiomReactiveStatusVisual afterSuccess = model.Resolve(false, default(AxiomControlOpportunity), 1.5f);

            AxiomControlInterventionOutcome failureOutcome = Outcome(2, false, AxiomErrorKind.Rate, 1f, 2f);
            bool firstFailure = model.Observe(failureOutcome, .4f);
            AxiomReactiveStatusVisual failureVisual = model.Resolve(true, rateDown, 2.1f);
            AxiomReactiveStatusVisual restored = model.Resolve(true, rateDown, 2.5f);
            bool valid = firstSuccess
                && !duplicateSuccess
                && successVisual.Dimension == AxiomErrorKind.Rate
                && successVisual.Marker == AxiomReactiveMarker.Success
                && !afterSuccess.HasOverlay
                && firstFailure
                && failureVisual.Marker == AxiomReactiveMarker.Failure
                && restored.Marker == AxiomReactiveMarker.Down;
            failure = valid ? null : "Reactive success/failure feedback was not bounded, exactly-once, or correctly restored.";
            return valid;
        }

        private static bool VerifyStaleFeedbackAndNaturalResolution(out string failure)
        {
            AxiomReactiveStatusPresentation model = new AxiomReactiveStatusPresentation();
            model.Observe(Outcome(4, false, AxiomErrorKind.Rate, 1f, 4f), .4f);
            AxiomControlOpportunity replacement = Opportunity(AxiomErrorKind.Acceleration, -1f);
            AxiomReactiveStatusVisual duringFeedback = model.Resolve(true, replacement, 4.1f);
            AxiomReactiveStatusVisual afterFeedback = model.Resolve(true, replacement, 4.5f);
            AxiomReactiveStatusVisual naturallyResolved = model.Resolve(false, default(AxiomControlOpportunity), 4.6f);
            bool valid = duringFeedback.Dimension == AxiomErrorKind.Rate
                && duringFeedback.Marker == AxiomReactiveMarker.Failure
                && afterFeedback.Dimension == AxiomErrorKind.Acceleration
                && afterFeedback.Marker == AxiomReactiveMarker.Up
                && !naturallyResolved.HasOverlay;
            failure = valid ? null : "Reactive feedback did not re-query current authority after expiry or natural resolution.";
            return valid;
        }

        private static bool VerifySemanticIsolationAndPhaseIdentity(out string failure)
        {
            bool valid = MobStatusPresentationLayout.IsAxiomPhenomenon(MobStatusIconKind.AxiomHeat)
                && MobStatusPresentationLayout.IsAxiomPhenomenon(MobStatusIconKind.AxiomResonance)
                && !MobStatusPresentationLayout.IsAxiomPhenomenon(MobStatusIconKind.Burn)
                && !MobStatusPresentationLayout.IsAxiomPhenomenon(MobStatusIconKind.Frenzied)
                && !MobStatusPresentationLayout.IsAxiomPhenomenon(MobStatusIconKind.ElementallyBuffed)
                && MobStatusIconKind.AxiomPhase != MobStatusIconKind.Imaginary;
            failure = valid ? null : "Ordinary/corrupt status isolation or the explicit Phase presentation identity changed.";
            return valid;
        }

        private static bool VerifyAllEightGenericIdentities(out string failure)
        {
            MobStatusIconKind[] kinds =
            {
                MobStatusIconKind.AxiomHeat,
                MobStatusIconKind.AxiomFlow,
                MobStatusIconKind.AxiomMass,
                MobStatusIconKind.AxiomCompression,
                MobStatusIconKind.AxiomPotential,
                MobStatusIconKind.AxiomResonance,
                MobStatusIconKind.AxiomPhase,
                MobStatusIconKind.AxiomOrder
            };
            bool valid = true;
            for (int index = 0; index < kinds.Length; index++)
            {
                valid &= MobStatusPresentationLayout.IsAxiomPhenomenon(kinds[index]);
            }

            failure = valid ? null : "One or more canonical Axiom identities bypasses the generic reactive presentation path.";
            return valid;
        }

        private static bool VerifyHeatBurnConsolidation(out string failure)
        {
            bool valid = MobStatusPresentationLayout.ShouldSuppressLegacyBurn(true)
                && !MobStatusPresentationLayout.ShouldSuppressLegacyBurn(false);
            failure = valid ? null : "Axiom Heat no longer deterministically consolidates the redundant legacy Burn world icon.";
            return valid;
        }

        private static bool VerifyFixedFootprintAndDeterminism(out string failure)
        {
            float first = MobStatusPresentationLayout.CellCenterX(0, 2, .16f, .05f);
            float second = MobStatusPresentationLayout.CellCenterX(1, 2, .16f, .05f);
            AxiomReactiveStatusPresentation left = new AxiomReactiveStatusPresentation();
            AxiomReactiveStatusPresentation right = new AxiomReactiveStatusPresentation();
            AxiomReactiveStatusVisual leftVisual = left.Resolve(true, Opportunity(AxiomErrorKind.Rate, 1f), 0f);
            AxiomReactiveStatusVisual rightVisual = right.Resolve(true, Opportunity(AxiomErrorKind.State, -1f), 0f);
            bool valid = first == -second
                && leftVisual.Marker == AxiomReactiveMarker.Down
                && rightVisual.Marker == AxiomReactiveMarker.Up;
            failure = valid ? null : "Reactive state changed fixed row geometry or coupled separate phenomenon cells.";
            return valid;
        }

        private static bool VerifyUniversalFootprintCountsAndAssets(out string failure)
        {
            MobStatusPresentationEntry five = new MobStatusPresentationEntry(MobStatusIconKind.AxiomHeat, 5);
            MobStatusPresentationEntry twelve = new MobStatusPresentationEntry(MobStatusIconKind.AxiomPhase, 12);
            AxiomControlOverlayRegistry overlays = Resources.Load<AxiomControlOverlayRegistry>("AxiomControlOverlayRegistry");
            bool assetsValid = overlays != null
                && overlays.GetFrame(AxiomErrorKind.State) != null
                && overlays.GetFrame(AxiomErrorKind.Rate) != null
                && overlays.GetFrame(AxiomErrorKind.Acceleration) != null
                && overlays.GetBadge(AxiomErrorKind.State) != null
                && overlays.GetBadge(AxiomErrorKind.Rate) != null
                && overlays.GetBadge(AxiomErrorKind.Acceleration) != null
                && overlays.GetMarker(AxiomReactiveMarker.Up) != null
                && overlays.GetMarker(AxiomReactiveMarker.Down) != null
                && overlays.GetMarker(AxiomReactiveMarker.Success) != null
                && overlays.GetMarker(AxiomReactiveMarker.Failure) != null;
            bool valid = five.StackCount == 5
                && twelve.StackCount == 12
                && EnemyWorldStatusIndicators.UniversalIconSize == .20f
                && assetsValid;
            failure = valid ? null : "Reactive status cells lost universal 0.20-unit geometry, stack readability, or S/R/A frame-and-badge bindings.";
            return valid;
        }

        private static bool IsVisual(
            AxiomReactiveStatusPresentation model,
            AxiomErrorKind errorKind,
            float signedDifference,
            AxiomReactiveMarker expectedMarker)
        {
            AxiomReactiveStatusVisual visual = model.Resolve(true, Opportunity(errorKind, signedDifference), 0f);
            return visual.Dimension == errorKind && visual.Marker == expectedMarker;
        }

        private static AxiomControlOpportunity Opportunity(AxiomErrorKind errorKind, float signedDifference)
        {
            AxiomErrorState error = new AxiomErrorState(AxiomKind.Heat, errorKind, signedDifference, 0f);
            AxiomOpportunityCandidate candidate = new AxiomOpportunityCandidate(
                AxiomKind.Heat,
                error,
                1f,
                AxiomOpportunityCandidateSource.TrajectoryDeviation,
                0f);
            return new AxiomControlOpportunity(candidate, AxiomControlReferenceProfile.DefaultFor(AxiomKind.Heat), 0f);
        }

        private static AxiomControlInterventionOutcome Outcome(
            long sequence,
            bool succeeded,
            AxiomErrorKind errorKind,
            float signedDifference,
            float timestamp)
        {
            AxiomErrorState error = new AxiomErrorState(AxiomKind.Heat, errorKind, signedDifference, timestamp);
            return new AxiomControlInterventionOutcome(
                null,
                AxiomKind.Heat,
                error,
                signedDifference < 0f ? 1f : -1f,
                signedDifference < 0f ? 1f : -1f,
                succeeded,
                sequence,
                timestamp);
        }
    }
}
