using Cave.Axioms.Elemental;

namespace Cave.Axioms.Control
{
    /// <summary>
    /// Builds control errors from one actor's observed stack trajectory and that
    /// same actor's dynamics-derived desired trajectory. Policy supplies only
    /// evaluation terms and tolerances; it never supplies desired S/R/A values.
    /// </summary>
    public static class AxiomDynamicsControlResolver
    {
        public static AxiomErrorState Evaluate(
            AxiomTrajectoryState actual,
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile policy,
            float timestamp)
        {
            return AxiomErrorEvaluator.Evaluate(
                actual,
                BuildReference(desired, policy),
                timestamp);
        }

        public static AxiomTrajectoryReference BuildReference(
            AxiomTrajectoryState desired,
            AxiomControlReferenceProfile policy)
        {
            AxiomTrajectoryReference tuning = policy.Reference;
            return new AxiomTrajectoryReference
            {
                EvaluateState = tuning.EvaluateState,
                State = desired.CurrentValue,
                StateTolerance = NonNegativeFinite(tuning.StateTolerance),
                EvaluateRate = tuning.EvaluateRate && desired.HasShortRate,
                Rate = desired.ShortRate,
                RateTolerance = NonNegativeFinite(tuning.RateTolerance),
                EvaluateAcceleration = tuning.EvaluateAcceleration && desired.HasShortAcceleration,
                Acceleration = desired.ShortAcceleration,
                AccelerationTolerance = NonNegativeFinite(tuning.AccelerationTolerance)
            };
        }

        public static float RequiredCorrectionDirection(AxiomErrorState error)
        {
            if (!error.HasError || error.SignedDifference == 0f)
            {
                return 0f;
            }

            return error.SignedDifference > 0f ? -1f : 1f;
        }

        public static bool IsCorrective(float correctionDirection, AxiomErrorState error)
        {
            return correctionDirection != 0f
                && correctionDirection * error.SignedDifference < 0f;
        }

        private static float NonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f ? value : 0f;
        }
    }
}
