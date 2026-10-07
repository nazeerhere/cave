using System;
using Cave.Axioms.Thresholds;

namespace Cave.Axioms.Control
{
    public enum AxiomOpportunityCandidateSource
    {
        TrajectoryDeviation,
        PlayerDisturbance,
        // Reserved resolver inputs; threshold systems will compete here later.
        ThresholdProximity,
        ThresholdCrossing
    }

    public enum AxiomOpportunityResolutionDisposition
    {
        None,
        Opened,
        Kept,
        Updated,
        Replaced,
        Resolved
    }

    /// <summary>Read-only, presentation-neutral description of one qualifying S/R/A error.</summary>
    public struct AxiomOpportunityCandidate
    {
        public AxiomOpportunityCandidate(
            AxiomKind kind,
            AxiomErrorState error,
            float significance,
            AxiomOpportunityCandidateSource source,
            float observedAt)
        {
            Kind = kind;
            Error = error;
            Significance = significance;
            Source = source;
            ObservedAt = observedAt;
            RequiredCorrectionDirection = AxiomDynamicsControlResolver.RequiredCorrectionDirection(error);
        }

        public AxiomKind Kind { get; }
        public AxiomErrorState Error { get; }
        public float Significance { get; }
        public float RequiredCorrectionDirection { get; }
        public AxiomOpportunityCandidateSource Source { get; }
        public float ObservedAt { get; }
        public bool IsValid => Error.HasError
            && RequiredCorrectionDirection != 0f
            && IsFinite(Significance)
            && Significance > 0f;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Selection tuning only. It can affect eligibility and stability, never the
    /// desired trajectory or correction direction.
    /// </summary>
    [Serializable]
    public struct AxiomOpportunityResolverPolicy
    {
        public float MinimumSignificance;
        public float ReplacementSignificanceMargin;
        public float MinimumActiveLifetime;
        public float TieSignificanceTolerance;
        public float PlayerDisturbanceSignificanceDelta;

        public static AxiomOpportunityResolverPolicy DefaultFor(AxiomControlReferenceProfile profile)
        {
            return new AxiomOpportunityResolverPolicy
            {
                MinimumSignificance = 1f,
                ReplacementSignificanceMargin = .25f,
                MinimumActiveLifetime = profile.OpportunityLifetime,
                TieSignificanceTolerance = .0001f,
                PlayerDisturbanceSignificanceDelta = .25f
            };
        }

        public AxiomOpportunityResolverPolicy Sanitized(AxiomControlReferenceProfile profile)
        {
            AxiomOpportunityResolverPolicy fallback = DefaultFor(profile);
            AxiomOpportunityResolverPolicy value = this;
            value.MinimumSignificance = Positive(value.MinimumSignificance, fallback.MinimumSignificance);
            value.ReplacementSignificanceMargin = NonNegative(value.ReplacementSignificanceMargin, fallback.ReplacementSignificanceMargin);
            value.MinimumActiveLifetime = NonNegative(value.MinimumActiveLifetime, fallback.MinimumActiveLifetime);
            value.TieSignificanceTolerance = NonNegative(value.TieSignificanceTolerance, fallback.TieSignificanceTolerance);
            value.PlayerDisturbanceSignificanceDelta = NonNegative(value.PlayerDisturbanceSignificanceDelta, fallback.PlayerDisturbanceSignificanceDelta);
            return value;
        }

        private static float Positive(float value, float fallback)
        {
            return IsFinite(value) && value > 0f ? value : fallback;
        }

        private static float NonNegative(float value, float fallback)
        {
            return IsFinite(value) && value >= 0f ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public struct AxiomOpportunityResolution
    {
        public AxiomOpportunityResolution(
            AxiomOpportunityResolutionDisposition disposition,
            AxiomOpportunityCandidate candidate,
            bool notify)
        {
            Disposition = disposition;
            Candidate = candidate;
            Notify = notify;
        }

        public AxiomOpportunityResolutionDisposition Disposition { get; }
        public AxiomOpportunityCandidate Candidate { get; }
        public bool Notify { get; }
        public bool HasActiveCandidate => Disposition != AxiomOpportunityResolutionDisposition.None
            && Disposition != AxiomOpportunityResolutionDisposition.Resolved
            && Candidate.IsValid;
    }

    /// <summary>
    /// Pure, deterministic chooser for one phenomenon's authoritative S/R/A
    /// errors. Tie priority intentionally preserves the prior evaluator order:
    /// Acceleration, then Rate, then State.
    /// </summary>
    public static class AxiomOpportunityResolver
    {
        public static AxiomOpportunityResolution Resolve(
            AxiomTrajectoryState actual,
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile profile,
            bool hasCurrent,
            AxiomControlOpportunity current,
            bool playerDisturbance,
            float timestamp)
        {
            return Resolve(actual, desired, profile, hasCurrent, current, playerDisturbance,
                default(AxiomThresholdOpportunityContext), timestamp);
        }

        /// <summary>
        /// Threshold context can emphasize a real authoritative discrepancy, but
        /// never supplies an error dimension or correction direction of its own.
        /// </summary>
        public static AxiomOpportunityResolution Resolve(
            AxiomTrajectoryState actual,
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile profile,
            bool hasCurrent,
            AxiomControlOpportunity current,
            bool playerDisturbance,
            AxiomThresholdOpportunityContext thresholdContext,
            float timestamp)
        {
            AxiomOpportunityResolverPolicy policy = profile.OpportunityPolicy.Sanitized(profile);
            AxiomOpportunityCandidateSource source = ResolveRequestedSource(playerDisturbance, thresholdContext);
            float thresholdMultiplier = thresholdContext.IsActive ? thresholdContext.SignificanceMultiplier : 1f;
            AxiomOpportunityCandidate state = BuildCandidate(actual, desired, profile, policy, AxiomErrorKind.State, source, thresholdMultiplier, timestamp);
            AxiomOpportunityCandidate rate = BuildCandidate(actual, desired, profile, policy, AxiomErrorKind.Rate, source, thresholdMultiplier, timestamp);
            AxiomOpportunityCandidate acceleration = BuildCandidate(actual, desired, profile, policy, AxiomErrorKind.Acceleration, source, thresholdMultiplier, timestamp);
            AxiomOpportunityCandidate dominant = ChooseDominant(state, rate, acceleration, policy.TieSignificanceTolerance);

            if (!dominant.IsValid)
            {
                return new AxiomOpportunityResolution(
                    hasCurrent ? AxiomOpportunityResolutionDisposition.Resolved : AxiomOpportunityResolutionDisposition.None,
                    default(AxiomOpportunityCandidate),
                    hasCurrent);
            }

            AxiomOpportunityCandidate activeDimension = FindDimension(current.Error.ErrorKind, state, rate, acceleration);
            if (!hasCurrent)
            {
                return new AxiomOpportunityResolution(
                    AxiomOpportunityResolutionDisposition.Opened,
                    dominant,
                    true);
            }

            if (!activeDimension.IsValid)
            {
                return new AxiomOpportunityResolution(
                    AxiomOpportunityResolutionDisposition.Replaced,
                    WithSource(dominant, ResolveSource(dominant, current, source, policy)),
                    true);
            }

            if (activeDimension.Error.SignedDifference * current.Error.SignedDifference < 0f)
            {
                return new AxiomOpportunityResolution(
                    AxiomOpportunityResolutionDisposition.Updated,
                    WithSource(activeDimension, ResolveSource(activeDimension, current, source, policy)),
                    true);
            }

            if (dominant.Error.ErrorKind == current.Error.ErrorKind)
            {
                return new AxiomOpportunityResolution(
                    AxiomOpportunityResolutionDisposition.Updated,
                    WithSource(activeDimension, ResolveSource(activeDimension, current, source, policy)),
                    IsMaterialChange(activeDimension, current, policy));
            }

            bool pastMinimumLifetime = timestamp >= current.MinimumActiveUntil;
            bool meaningfullyDominant = dominant.Significance
                > activeDimension.Significance + policy.ReplacementSignificanceMargin;
            if (pastMinimumLifetime && meaningfullyDominant)
            {
                return new AxiomOpportunityResolution(
                    AxiomOpportunityResolutionDisposition.Replaced,
                    WithSource(dominant, ResolveSource(dominant, current, source, policy)),
                    true);
            }

            return new AxiomOpportunityResolution(
                AxiomOpportunityResolutionDisposition.Kept,
                WithSource(activeDimension, ResolveSource(activeDimension, current, source, policy)),
                false);
        }

        private static AxiomOpportunityCandidate BuildCandidate(
            AxiomTrajectoryState actual,
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile profile,
            AxiomOpportunityResolverPolicy policy,
            AxiomErrorKind dimension,
            AxiomOpportunityCandidateSource source,
            float significanceMultiplier,
            float timestamp)
        {
            bool enabled;
            bool available;
            float actualValue;
            float desiredValue;
            float tolerance;
            switch (dimension)
            {
                case AxiomErrorKind.State:
                    enabled = profile.Reference.EvaluateState;
                    available = true;
                    actualValue = actual.CurrentValue;
                    desiredValue = desired.CurrentValue;
                    tolerance = profile.Reference.StateTolerance;
                    break;
                case AxiomErrorKind.Rate:
                    enabled = profile.Reference.EvaluateRate;
                    available = actual.HasShortRate && desired.HasShortRate;
                    actualValue = actual.ShortRate;
                    desiredValue = desired.ShortRate;
                    tolerance = profile.Reference.RateTolerance;
                    break;
                case AxiomErrorKind.Acceleration:
                    enabled = profile.Reference.EvaluateAcceleration;
                    available = actual.HasShortAcceleration && desired.HasShortAcceleration;
                    actualValue = actual.ShortAcceleration;
                    desiredValue = desired.ShortAcceleration;
                    tolerance = profile.Reference.AccelerationTolerance;
                    break;
                default:
                    return default(AxiomOpportunityCandidate);
            }

            if (!enabled || !available || !Finite(actualValue) || !Finite(desiredValue))
            {
                return default(AxiomOpportunityCandidate);
            }

            float signedError = actualValue - desiredValue;
            float magnitude = Abs(signedError);
            float meaningfulTolerance = PositiveFinite(tolerance, .0001f);
            // Tolerance boundaries are controlled inclusively. An opportunity
            // exists only outside the configured band; no exact target equality
            // is ever required.
            if (magnitude <= meaningfulTolerance)
            {
                return default(AxiomOpportunityCandidate);
            }
            float baseSignificance = magnitude / meaningfulTolerance;
            if (!Finite(baseSignificance)
                || magnitude < NonNegativeFinite(profile.MinimumErrorMagnitude)
                || baseSignificance < policy.MinimumSignificance)
            {
                return default(AxiomOpportunityCandidate);
            }
            float significance = baseSignificance * PositiveFinite(significanceMultiplier, 1f);
            if (!Finite(significance)) return default(AxiomOpportunityCandidate);

            return new AxiomOpportunityCandidate(
                actual.Kind,
                new AxiomErrorState(actual.Kind, dimension, signedError, timestamp),
                significance,
                source,
                timestamp);
        }

        private static AxiomOpportunityCandidate ChooseDominant(
            AxiomOpportunityCandidate state,
            AxiomOpportunityCandidate rate,
            AxiomOpportunityCandidate acceleration,
            float tieTolerance)
        {
            AxiomOpportunityCandidate winner = default(AxiomOpportunityCandidate);
            Choose(ref winner, state, tieTolerance);
            Choose(ref winner, rate, tieTolerance);
            Choose(ref winner, acceleration, tieTolerance);
            return winner;
        }

        private static void Choose(ref AxiomOpportunityCandidate winner, AxiomOpportunityCandidate challenger, float tieTolerance)
        {
            if (!challenger.IsValid)
            {
                return;
            }

            if (!winner.IsValid
                || challenger.Significance > winner.Significance + tieTolerance
                || (Abs(challenger.Significance - winner.Significance) <= tieTolerance
                    && Priority(challenger.Error.ErrorKind) > Priority(winner.Error.ErrorKind)))
            {
                winner = challenger;
            }
        }

        private static AxiomOpportunityCandidate FindDimension(
            AxiomErrorKind dimension,
            AxiomOpportunityCandidate state,
            AxiomOpportunityCandidate rate,
            AxiomOpportunityCandidate acceleration)
        {
            switch (dimension)
            {
                case AxiomErrorKind.State: return state;
                case AxiomErrorKind.Rate: return rate;
                case AxiomErrorKind.Acceleration: return acceleration;
                default: return default(AxiomOpportunityCandidate);
            }
        }

        private static AxiomOpportunityCandidateSource ResolveSource(
            AxiomOpportunityCandidate candidate,
            AxiomControlOpportunity current,
            AxiomOpportunityCandidateSource requestedSource,
            AxiomOpportunityResolverPolicy policy)
        {
            if ((requestedSource == AxiomOpportunityCandidateSource.ThresholdProximity
                    || requestedSource == AxiomOpportunityCandidateSource.ThresholdCrossing)
                && IsMaterialChange(candidate, current, policy)) return requestedSource;
            return requestedSource == AxiomOpportunityCandidateSource.PlayerDisturbance
                    && IsMaterialChange(candidate, current, policy)
                ? AxiomOpportunityCandidateSource.PlayerDisturbance : AxiomOpportunityCandidateSource.TrajectoryDeviation;
        }

        private static AxiomOpportunityCandidateSource ResolveRequestedSource(
            bool playerDisturbance, AxiomThresholdOpportunityContext thresholdContext)
        {
            if (thresholdContext.IsActive) return thresholdContext.Source;
            return playerDisturbance ? AxiomOpportunityCandidateSource.PlayerDisturbance
                : AxiomOpportunityCandidateSource.TrajectoryDeviation;
        }

        private static bool IsMaterialChange(
            AxiomOpportunityCandidate candidate,
            AxiomControlOpportunity current,
            AxiomOpportunityResolverPolicy policy)
        {
            return candidate.Error.ErrorKind != current.Error.ErrorKind
                || candidate.Error.SignedDifference * current.Error.SignedDifference < 0f
                || Abs(candidate.Significance - current.Significance) >= policy.PlayerDisturbanceSignificanceDelta;
        }

        private static AxiomOpportunityCandidate WithSource(
            AxiomOpportunityCandidate candidate,
            AxiomOpportunityCandidateSource source)
        {
            return new AxiomOpportunityCandidate(
                candidate.Kind,
                candidate.Error,
                candidate.Significance,
                source,
                candidate.ObservedAt);
        }

        private static int Priority(AxiomErrorKind kind)
        {
            switch (kind)
            {
                case AxiomErrorKind.Acceleration: return 3;
                case AxiomErrorKind.Rate: return 2;
                case AxiomErrorKind.State: return 1;
                default: return 0;
            }
        }

        private static float Abs(float value) => value < 0f ? -value : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float PositiveFinite(float value, float fallback) => Finite(value) && value > 0f ? value : fallback;
        private static float NonNegativeFinite(float value) => Finite(value) && value > 0f ? value : 0f;
    }
}
