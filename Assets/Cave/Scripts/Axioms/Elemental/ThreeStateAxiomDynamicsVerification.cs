using Cave.Axioms.Control;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Focused deterministic contract checks for the production continuous S/R/A
    /// channel. The older B/C verifier remains historical compatibility coverage;
    /// this verifier is the replacement authority for active gameplay dynamics.
    /// </summary>
    public static class ThreeStateAxiomDynamicsVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyInputIsContinuousNotStackMutation(out failure)
                || !VerifyNaturalRelaxationAndBounds(out failure)
                || !VerifyTypedDimensionsAndCorrections(out failure)
                || !VerifyActorAndProfileIsolation(out failure)
                || !VerifyAllEightKindsAndDeterminism(out failure)
                || !VerifyThresholdAndReferenceSemantics(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyInputIsContinuousNotStackMutation(out string failure)
        {
            ThreeStateAxiomDynamicsChannel channel = NewChannel(AxiomKind.Heat);
            channel.ApplyInput(1f, 0f);
            AxiomTrajectoryState immediately = channel.GetActualTrajectory(0f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryState evolved = channel.GetActualTrajectory(.20f, AxiomTrajectoryThresholds.Default);
            bool valid = Approximately(immediately.CurrentValue, 0f)
                && evolved.CurrentValue > 0f
                && evolved.ShortRate > 0f
                && evolved.ShortAcceleration > 0f;
            failure = valid ? null : "Typed input did not evolve the authoritative S/R/A trajectory continuously.";
            return valid;
        }

        private static bool VerifyNaturalRelaxationAndBounds(out string failure)
        {
            AxiomDynamicsParameters bounded = AxiomDynamicsParameters.DefaultFor(AxiomKind.Flow);
            bounded.MaximumState = 2f;
            bounded.MaximumAbsoluteRate = 3f;
            bounded.MaximumAbsoluteAcceleration = 4f;
            bounded.MaximumAdvanceSeconds = 30f;
            ThreeStateAxiomDynamicsChannel channel = new ThreeStateAxiomDynamicsChannel(AxiomKind.Flow, bounded);
            channel.ApplyInput(100f, 0f);
            AxiomTrajectoryState driven = channel.GetActualTrajectory(.4f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryState settled = channel.GetActualTrajectory(20f, AxiomTrajectoryThresholds.Default);
            bool valid = driven.CurrentValue >= 0f && driven.CurrentValue <= 2f
                && Abs(driven.ShortRate) <= 3f && Abs(driven.ShortAcceleration) <= 4f
                && Abs(settled.CurrentValue) < .05f
                && Abs(settled.ShortRate) < .05f
                && Abs(settled.ShortAcceleration) < .05f;
            failure = valid ? null : "Continuous S/R/A dynamics did not remain bounded or relax naturally to the desired trajectory.";
            return valid;
        }

        private static bool VerifyTypedDimensionsAndCorrections(out string failure)
        {
            ThreeStateAxiomDynamicsChannel channel = NewChannel(AxiomKind.Mass);
            channel.ApplyInput(1f, 0f);
            AxiomTrajectoryState before = channel.GetActualTrajectory(.2f, AxiomTrajectoryThresholds.Default);
            channel.ApplyControlCorrection(AxiomErrorKind.Rate, -1f, .5f, .2f);
            AxiomTrajectoryState corrected = channel.GetActualTrajectory(.2f, AxiomTrajectoryThresholds.Default);
            bool valid = Approximately(before.CurrentValue, corrected.CurrentValue)
                && corrected.ShortRate < before.ShortRate
                && Approximately(before.ShortAcceleration, corrected.ShortAcceleration);
            failure = valid ? null : "A Rate control correction changed a dimension other than Rate or snapped the continuous trajectory.";
            return valid;
        }

        private static bool VerifyActorAndProfileIsolation(out string failure)
        {
            AxiomDynamicsParameters damped = AxiomDynamicsParameters.DefaultFor(AxiomKind.Order);
            damped.RateDamping = 12f;
            ThreeStateAxiomDynamicsChannel ordinary = NewChannel(AxiomKind.Order);
            ThreeStateAxiomDynamicsChannel highDamping = new ThreeStateAxiomDynamicsChannel(AxiomKind.Order, damped);
            ordinary.ApplyInput(1f, 0f);
            highDamping.ApplyInput(1f, 0f);
            AxiomTrajectoryState left = ordinary.GetActualTrajectory(.35f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryState right = highDamping.GetActualTrajectory(.35f, AxiomTrajectoryThresholds.Default);
            bool valid = !Approximately(left.ShortRate, right.ShortRate)
                && AxiomDynamicsParameters.DefaultFor(AxiomKind.Order).ThreeStateModelVersion >= 1;
            failure = valid ? null : "Actor-local profile coefficients did not independently affect the continuous trajectory.";
            return valid;
        }

        private static bool VerifyAllEightKindsAndDeterminism(out string failure)
        {
            AxiomKind[] kinds =
            {
                AxiomKind.Heat, AxiomKind.Flow, AxiomKind.Mass, AxiomKind.Compression,
                AxiomKind.Potential, AxiomKind.Resonance, AxiomKind.Phase, AxiomKind.Order
            };
            bool valid = true;
            for (int index = 0; index < kinds.Length; index++)
            {
                ThreeStateAxiomDynamicsChannel first = NewChannel(kinds[index]);
                ThreeStateAxiomDynamicsChannel second = NewChannel(kinds[index]);
                first.ApplyInput(1f, 0f);
                second.ApplyInput(1f, 0f);
                AxiomTrajectoryState left = first.GetActualTrajectory(.3f, AxiomTrajectoryThresholds.Default);
                AxiomTrajectoryState right = second.GetActualTrajectory(.3f, AxiomTrajectoryThresholds.Default);
                valid &= Finite(left.CurrentValue) && Finite(left.ShortRate) && Finite(left.ShortAcceleration)
                    && Approximately(left.CurrentValue, right.CurrentValue)
                    && Approximately(left.ShortRate, right.ShortRate)
                    && Approximately(left.ShortAcceleration, right.ShortAcceleration);
            }

            failure = valid ? null : "One of the eight canonical phenomena is non-deterministic or missing continuous S/R/A support.";
            return valid;
        }

        private static bool VerifyThresholdAndReferenceSemantics(out string failure)
        {
            ThreeStateAxiomDynamicsChannel channel = NewChannel(AxiomKind.Phase);
            channel.ApplyInput(1f, 0f);
            AxiomTrajectoryState actual = channel.GetActualTrajectory(.2f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryState desired = channel.GetDesiredTrajectory(.2f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryReference reference = new AxiomTrajectoryReference
            {
                EvaluateState = true,
                State = desired.CurrentValue,
                StateTolerance = .01f
            };
            AxiomErrorState error = AxiomErrorEvaluator.Evaluate(actual, reference, .2f);
            bool valid = error.HasError && error.ErrorKind == AxiomErrorKind.State
                && AxiomDynamicsControlResolver.RequiredCorrectionDirection(error) < 0f;
            failure = valid ? null : "Control/threshold evaluation did not read the actual S/R/A trajectory against the desired trajectory.";
            return valid;
        }

        private static ThreeStateAxiomDynamicsChannel NewChannel(AxiomKind kind)
        {
            return new ThreeStateAxiomDynamicsChannel(kind, AxiomDynamicsParameters.DefaultFor(kind));
        }

        private static bool Approximately(float left, float right)
        {
            return Abs(left - right) <= .0001f;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
