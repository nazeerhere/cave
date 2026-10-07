using Cave.Axioms.Elemental;

namespace Cave.Axioms.Control
{
    /// <summary>Read-only S/R/A tolerance classification for one trajectory sample.</summary>
    public struct AxiomControlBandStatus
    {
        public AxiomControlBandStatus(bool state, bool rate, bool acceleration)
        {
            StateControlled = state;
            RateControlled = rate;
            AccelerationControlled = acceleration;
        }

        public bool StateControlled { get; }
        public bool RateControlled { get; }
        public bool AccelerationControlled { get; }
        public bool AllControlled => StateControlled && RateControlled && AccelerationControlled;
    }

    /// <summary>
    /// Pure tolerance-band policy. Equality at a configured boundary is
    /// controlled; exact floating-point target equality is never required.
    /// </summary>
    public static class AxiomControlBandEvaluator
    {
        public static AxiomControlBandStatus Evaluate(
            AxiomTrajectoryState actual,
            AxiomTrajectoryReference reference)
        {
            return new AxiomControlBandStatus(
                !reference.EvaluateState || Within(actual.CurrentValue, reference.State, reference.StateTolerance),
                !reference.EvaluateRate || (actual.HasShortRate && Within(actual.ShortRate, reference.Rate, reference.RateTolerance)),
                !reference.EvaluateAcceleration || (actual.HasShortAcceleration
                    && Within(actual.ShortAcceleration, reference.Acceleration, reference.AccelerationTolerance)));
        }

        public static bool IsDimensionControlled(
            AxiomTrajectoryState actual,
            AxiomTrajectoryReference reference,
            AxiomErrorKind dimension)
        {
            switch (dimension)
            {
                case AxiomErrorKind.State:
                    return reference.EvaluateState && Within(actual.CurrentValue, reference.State, reference.StateTolerance);
                case AxiomErrorKind.Rate:
                    return reference.EvaluateRate && actual.HasShortRate
                        && Within(actual.ShortRate, reference.Rate, reference.RateTolerance);
                case AxiomErrorKind.Acceleration:
                    return reference.EvaluateAcceleration && actual.HasShortAcceleration
                        && Within(actual.ShortAcceleration, reference.Acceleration, reference.AccelerationTolerance);
                default:
                    return false;
            }
        }

        private static bool Within(float actual, float desired, float tolerance)
        {
            return Finite(actual) && Finite(desired) && Finite(tolerance)
                && tolerance >= 0f && Abs(actual - desired) <= tolerance + 0.00001f;
        }

        private static float Abs(float value) => value < 0f ? -value : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Observation-only dwell tracker. It never mutates State, Rate, or
    /// Acceleration; it merely reports whether all three have remained inside
    /// their bands long enough to call the trajectory controlled.
    /// </summary>
    public sealed class AxiomControlledEquilibriumTracker
    {
        private float enteredAt = -1f;
        private bool controlled;

        public bool IsControlled => controlled;

        public bool Advance(AxiomControlBandStatus bands, float dwellSeconds, float timestamp)
        {
            if (!bands.AllControlled)
            {
                enteredAt = -1f;
                controlled = false;
                return false;
            }

            if (enteredAt < 0f)
            {
                enteredAt = timestamp;
            }

            if (timestamp - enteredAt >= NonNegativeFinite(dwellSeconds))
            {
                controlled = true;
            }

            return controlled;
        }

        private static float NonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f ? value : 0f;
        }
    }
}
