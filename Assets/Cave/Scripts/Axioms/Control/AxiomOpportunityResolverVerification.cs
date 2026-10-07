using Cave.Axioms.Elemental;

namespace Cave.Axioms.Control
{
    /// <summary>
    /// Pure, deterministic coverage for opportunity selection. It creates no
    /// GameObjects, applies no corrections, and submits no mastery evidence.
    /// </summary>
    public static class AxiomOpportunityResolverVerification
    {
        private const float Time = 10f;

        public static bool TryRunAll(out string failure)
        {
            if (!VerifyNoError(out failure)                         // A
                || !VerifySignedRateCandidate(out failure)          // B
                || !VerifyStrongestDimensionWins(out failure)       // C
                || !VerifyDeterministicTie(out failure)             // D
                || !VerifyMarginKeepsCurrent(out failure)           // E
                || !VerifyDominantReplacement(out failure)          // F
                || !VerifyNaturalResolution(out failure)            // G
                || !VerifyToleranceBoundaryResolves(out failure)    // H
                || !VerifyPlayerDisturbanceNoMastery(out failure)   // H
                || !VerifyPolicyCannotReverseDirection(out failure) // I
                || !VerifyActorRelativeDesiredTrajectory(out failure) // J
                || !VerifyOneCandidatePerPhenomenon(out failure)    // K
                || !VerifyIndependentPhenomena(out failure)         // L
                || !VerifyRepeatability(out failure))               // M
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyNoError(out string failure)
        {
            AxiomOpportunityResolution result = Resolve(Static(1f, 0f, 0f), Static(1f, 0f, 0f), StateOnly());
            return Require(result.Disposition == AxiomOpportunityResolutionDisposition.None,
                "A: a non-qualifying trajectory created an opportunity.", out failure);
        }

        private static bool VerifySignedRateCandidate(out string failure)
        {
            AxiomOpportunityResolution result = Resolve(Static(0f, 3f, 0f), Static(0f, 1f, 0f), RateOnly());
            return Require(result.Disposition == AxiomOpportunityResolutionDisposition.Opened
                    && result.Candidate.Error.ErrorKind == AxiomErrorKind.Rate
                    && result.Candidate.Error.SignedDifference > 0f
                    && result.Candidate.RequiredCorrectionDirection < 0f,
                "B: Rate candidate did not preserve signed error and authoritative correction direction.", out failure);
        }

        private static bool VerifyStrongestDimensionWins(out string failure)
        {
            AxiomControlReferenceProfile profile = AllDimensions();
            AxiomOpportunityResolution result = Resolve(Static(2f, 5f, 3f), Static(0f, 0f, 0f), profile);
            return Require(result.Candidate.Error.ErrorKind == AxiomErrorKind.Rate,
                "C: the largest normalized State/Rate/Acceleration error was not selected.", out failure);
        }

        private static bool VerifyDeterministicTie(out string failure)
        {
            AxiomControlReferenceProfile profile = AllDimensions();
            for (int index = 0; index < 16; index++)
            {
                AxiomOpportunityResolution result = Resolve(Static(2f, 2f, 0f), Static(0f, 0f, 0f), profile);
                if (result.Candidate.Error.ErrorKind != AxiomErrorKind.Rate)
                {
                    return Require(false, "D: equal significance did not deterministically preserve Rate over State.", out failure);
                }
            }

            failure = null;
            return true;
        }

        private static bool VerifyMarginKeepsCurrent(out string failure)
        {
            AxiomControlReferenceProfile profile = AllDimensions();
            AxiomControlOpportunity current = OpenCurrent(Static(0f, 2f, 0f), Static(0f, 0f, 0f), profile, 0f);
            AxiomOpportunityResolution result = Resolve(
                Static(2.1f, 2f, 0f), Static(0f, 0f, 0f), profile, true, current, false, Time);
            return Require(result.Disposition == AxiomOpportunityResolutionDisposition.Kept
                    && result.Candidate.Error.ErrorKind == AxiomErrorKind.Rate,
                "E: a below-margin challenger displaced the active opportunity.", out failure);
        }

        private static bool VerifyDominantReplacement(out string failure)
        {
            AxiomControlReferenceProfile profile = AllDimensions();
            AxiomControlOpportunity current = OpenCurrent(Static(0f, 2f, 0f), Static(0f, 0f, 0f), profile, 0f);
            AxiomOpportunityResolution result = Resolve(
                Static(3f, 2f, 0f), Static(0f, 0f, 0f), profile, true, current, false, Time);
            return Require(result.Disposition == AxiomOpportunityResolutionDisposition.Replaced
                    && result.Candidate.Error.ErrorKind == AxiomErrorKind.State,
                "F: a substantially stronger challenger did not replace after the minimum active lifetime.", out failure);
        }

        private static bool VerifyNaturalResolution(out string failure)
        {
            AxiomControlReferenceProfile profile = StateOnly();
            AxiomControlOpportunity current = OpenCurrent(Static(2f, 0f, 0f), Static(0f, 0f, 0f), profile, 0f);
            AxiomOpportunityResolution result = Resolve(
                Static(0f, 0f, 0f), Static(0f, 0f, 0f), profile, true, current, false, Time);
            return Require(result.Disposition == AxiomOpportunityResolutionDisposition.Resolved
                    && !result.HasActiveCandidate,
                "G: convergence did not naturally resolve the existing opportunity without an intervention path.", out failure);
        }

        private static bool VerifyPlayerDisturbanceNoMastery(out string failure)
        {
            AxiomOpportunityResolution result = Resolve(
                Static(2f, 0f, 0f), Static(0f, 0f, 0f), StateOnly(), false,
                default(AxiomControlOpportunity), true, Time);
            return Require(result.Disposition == AxiomOpportunityResolutionDisposition.Opened
                    && result.Candidate.Source == AxiomOpportunityCandidateSource.PlayerDisturbance,
                "H: a qualifying player disturbance was not classified without invoking an intervention/mastery API.", out failure);
        }

        private static bool VerifyToleranceBoundaryResolves(out string failure)
        {
            AxiomControlReferenceProfile profile = StateOnly();
            AxiomOpportunityResolution result = Resolve(
                Static(1f, 0f, 0f), Static(0f, 0f, 0f), profile);
            return Require(result.Disposition == AxiomOpportunityResolutionDisposition.None,
                "H: a value on the inclusive State tolerance boundary created an opportunity.", out failure);
        }

        private static bool VerifyPolicyCannotReverseDirection(out string failure)
        {
            AxiomControlReferenceProfile first = StateOnly();
            AxiomControlReferenceProfile second = StateOnly();
            second.OpportunityPolicy.MinimumSignificance = .5f;
            second.OpportunityPolicy.ReplacementSignificanceMargin = 100f;
            AxiomOpportunityResolution left = Resolve(Static(2f, 0f, 0f), Static(0f, 0f, 0f), first);
            AxiomOpportunityResolution right = Resolve(Static(2f, 0f, 0f), Static(0f, 0f, 0f), second);
            return Require(left.Candidate.RequiredCorrectionDirection == right.Candidate.RequiredCorrectionDirection
                    && left.Candidate.RequiredCorrectionDirection < 0f,
                "I: resolver policy altered the authoritative correction direction.", out failure);
        }

        private static bool VerifyActorRelativeDesiredTrajectory(out string failure)
        {
            AxiomControlReferenceProfile profile = StateOnly();
            // Both samples remain outside the inclusive one-unit State band.
            AxiomOpportunityResolution first = Resolve(Static(3f, 0f, 0f), Static(1f, 0f, 0f), profile);
            AxiomOpportunityResolution second = Resolve(Static(3f, 0f, 0f), Static(5f, 0f, 0f), profile);
            return Require(first.Candidate.RequiredCorrectionDirection < 0f
                    && second.Candidate.RequiredCorrectionDirection > 0f,
                "J: actor-relative desired trajectories did not independently determine selection direction.", out failure);
        }

        private static bool VerifyOneCandidatePerPhenomenon(out string failure)
        {
            AxiomOpportunityResolution result = Resolve(Static(3f, 4f, 5f), Static(0f, 0f, 0f), AllDimensions());
            return Require(result.HasActiveCandidate
                    && result.Candidate.Error.ErrorKind == AxiomErrorKind.Acceleration,
                "K: one phenomenon did not collapse multiple qualifying dimensions to one selected candidate.", out failure);
        }

        private static bool VerifyIndependentPhenomena(out string failure)
        {
            AxiomControlReferenceProfile heat = StateOnly(AxiomKind.Heat);
            AxiomControlReferenceProfile flow = StateOnly(AxiomKind.Flow);
            AxiomOpportunityResolution first = Resolve(Static(AxiomKind.Heat, 2f, 0f, 0f), Static(AxiomKind.Heat, 0f, 0f, 0f), heat);
            AxiomOpportunityResolution second = Resolve(Static(AxiomKind.Flow, 2f, 0f, 0f), Static(AxiomKind.Flow, 0f, 0f, 0f), flow);
            return Require(first.HasActiveCandidate && second.HasActiveCandidate
                    && first.Candidate.Kind == AxiomKind.Heat
                    && second.Candidate.Kind == AxiomKind.Flow,
                "L: independent phenomena could not each retain their own selected opportunity.", out failure);
        }

        private static bool VerifyRepeatability(out string failure)
        {
            AxiomControlReferenceProfile profile = AllDimensions();
            AxiomControlOpportunity current = OpenCurrent(Static(0f, 2f, 0f), Static(0f, 0f, 0f), profile, 0f);
            AxiomOpportunityResolution expected = Resolve(Static(1f, 2f, 0f), Static(0f, 0f, 0f), profile, true, current, false, Time);
            for (int index = 0; index < 32; index++)
            {
                AxiomOpportunityResolution repeated = Resolve(Static(1f, 2f, 0f), Static(0f, 0f, 0f), profile, true, current, false, Time);
                if (repeated.Disposition != expected.Disposition
                    || repeated.Candidate.Error.ErrorKind != expected.Candidate.Error.ErrorKind
                    || repeated.Candidate.RequiredCorrectionDirection != expected.Candidate.RequiredCorrectionDirection)
                {
                    return Require(false, "M: unchanged input produced nondeterministic selection or churn.", out failure);
                }
            }

            failure = null;
            return true;
        }

        private static AxiomControlOpportunity OpenCurrent(
            AxiomTrajectoryState actual,
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile profile,
            float timestamp)
        {
            AxiomOpportunityResolution opened = Resolve(actual, desired, profile, false, default(AxiomControlOpportunity), false, timestamp);
            return new AxiomControlOpportunity(opened.Candidate, profile, timestamp);
        }

        private static AxiomOpportunityResolution Resolve(
            AxiomTrajectoryState actual,
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile profile)
        {
            return Resolve(actual, desired, profile, false, default(AxiomControlOpportunity), false, Time);
        }

        private static AxiomOpportunityResolution Resolve(
            AxiomTrajectoryState actual,
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile profile,
            bool hasCurrent,
            AxiomControlOpportunity current,
            bool playerDisturbance,
            float timestamp)
        {
            return AxiomOpportunityResolver.Resolve(actual, desired, profile, hasCurrent, current, playerDisturbance, timestamp);
        }

        private static AxiomControlReferenceProfile StateOnly(AxiomKind kind = AxiomKind.Heat)
        {
            AxiomControlReferenceProfile profile = AxiomControlReferenceProfile.DefaultFor(kind);
            profile.Reference.EvaluateState = true;
            profile.Reference.StateTolerance = 1f;
            profile.Reference.EvaluateRate = false;
            profile.Reference.EvaluateAcceleration = false;
            return profile;
        }

        private static AxiomControlReferenceProfile RateOnly()
        {
            AxiomControlReferenceProfile profile = StateOnly();
            profile.Reference.EvaluateState = false;
            profile.Reference.EvaluateRate = true;
            profile.Reference.RateTolerance = 1f;
            return profile;
        }

        private static AxiomControlReferenceProfile AllDimensions()
        {
            AxiomControlReferenceProfile profile = StateOnly();
            profile.Reference.EvaluateRate = true;
            profile.Reference.RateTolerance = 1f;
            profile.Reference.EvaluateAcceleration = true;
            profile.Reference.AccelerationTolerance = 1f;
            return profile;
        }

        private static AxiomTrajectoryState Static(float state, float rate, float acceleration)
        {
            return Static(AxiomKind.Heat, state, rate, acceleration);
        }

        private static AxiomTrajectoryState Static(AxiomKind kind, float state, float rate, float acceleration)
        {
            return new AxiomTrajectoryState(
                kind, state, rate, rate, acceleration,
                true, true, true,
                AxiomTrajectoryDirection.Stable,
                AxiomRateIntensity.Slow,
                AxiomCurvature.StableRate,
                Time);
        }

        private static bool Require(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }
    }
}
