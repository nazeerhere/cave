using Cave.Axioms.Control;

namespace Cave.Axioms.Elemental
{
    /// <summary>
    /// Pure deterministic contract coverage for authoritative S/R/A control
    /// semantics. It owns no scene objects, mastery, or threshold effects.
    /// </summary>
    public static class AuthoritativeThreeStateControlVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            if (!VerifyAuthoritativeContinuousTrajectory(out failure)
                || !VerifyActorRelativeTargetsAndDirections(out failure)
                || !VerifyIndependentToleranceBands(out failure)
                || !VerifyMinimumDesiredState(out failure)
                || !VerifyNaturalOvershootRecovery(out failure)
                || !VerifyControlledEquilibriumDwellAndBreak(out failure)
                || !VerifyEquilibriumDoesNotFreezeDynamics(out failure)
                || !VerifyAllEightAndNumericalBounds(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyAuthoritativeContinuousTrajectory(out string failure)
        {
            ThreeStateAxiomDynamicsChannel channel = NewChannel(AxiomKind.Heat);
            channel.ApplyInput(1f, 0f);
            AxiomTrajectoryState early = channel.GetActualTrajectory(.1f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryState later = channel.GetActualTrajectory(.7f, AxiomTrajectoryThresholds.Default);
            bool valid = early.HasShortRate && early.HasShortAcceleration
                && later.HasShortRate && later.HasShortAcceleration
                && (!Near(early.CurrentValue, later.CurrentValue)
                    || !Near(early.ShortRate, later.ShortRate)
                    || !Near(early.ShortAcceleration, later.ShortAcceleration));
            failure = valid ? null : "Authoritative State, Rate, and Acceleration did not continue evolving after input ended.";
            return valid;
        }

        private static bool VerifyActorRelativeTargetsAndDirections(out string failure)
        {
            AxiomDynamicsParameters lower = AxiomDynamicsParameters.DefaultFor(AxiomKind.Heat);
            lower.DesiredState = 5f;
            AxiomDynamicsParameters higher = lower;
            higher.DesiredState = 8f;
            AxiomTrajectoryState actual = Static(6f, 0f, 0f);
            AxiomTrajectoryState lowTarget = new ThreeStateAxiomDynamicsChannel(AxiomKind.Heat, lower)
                .GetDesiredTrajectory(0f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryState highTarget = new ThreeStateAxiomDynamicsChannel(AxiomKind.Heat, higher)
                .GetDesiredTrajectory(0f, AxiomTrajectoryThresholds.Default);
            AxiomControlReferenceProfile profile = StateOnly(.1f);
            AxiomErrorState down = AxiomDynamicsControlResolver.Evaluate(actual, lowTarget, profile, 0f);
            AxiomErrorState up = AxiomDynamicsControlResolver.Evaluate(actual, highTarget, profile, 0f);
            bool valid = down.SignedDifference > 0f && up.SignedDifference < 0f
                && AxiomDynamicsControlResolver.RequiredCorrectionDirection(down) < 0f
                && AxiomDynamicsControlResolver.RequiredCorrectionDirection(up) > 0f;
            failure = valid ? null : "Identical State did not yield actor-relative opposite control directions.";
            return valid;
        }

        private static bool VerifyIndependentToleranceBands(out string failure)
        {
            AxiomTrajectoryReference reference = new AxiomTrajectoryReference
            {
                EvaluateState = true, State = 5f, StateTolerance = .4f,
                EvaluateRate = true, Rate = 2f, RateTolerance = .2f,
                EvaluateAcceleration = true, Acceleration = -1f, AccelerationTolerance = .05f
            };
            AxiomTrajectoryState edge = Static(5.4f, 2.2f, -.95f);
            AxiomTrajectoryState outsideState = Static(5.401f, 2f, -1f);
            AxiomTrajectoryState outsideRate = Static(5f, 2.201f, -1f);
            AxiomTrajectoryState outsideAcceleration = Static(5f, 2f, -.949f);
            AxiomControlBandStatus edgeBands = AxiomControlBandEvaluator.Evaluate(edge, reference);
            bool valid = edgeBands.AllControlled
                && !AxiomControlBandEvaluator.Evaluate(outsideState, reference).StateControlled
                && !AxiomControlBandEvaluator.Evaluate(outsideRate, reference).RateControlled
                && !AxiomControlBandEvaluator.Evaluate(outsideAcceleration, reference).AccelerationControlled;
            failure = valid ? null : "State, Rate, and Acceleration did not use independent inclusive tolerance bands.";
            return valid;
        }

        private static bool VerifyMinimumDesiredState(out string failure)
        {
            AxiomDynamicsParameters parameters = AxiomDynamicsParameters.DefaultFor(AxiomKind.Flow);
            parameters.DesiredState = 1f;
            parameters.EnforceMinimumDesiredState = true;
            parameters.MinimumDesiredState = 3f;
            AxiomTrajectoryState desired = new ThreeStateAxiomDynamicsChannel(AxiomKind.Flow, parameters)
                .GetDesiredTrajectory(0f, AxiomTrajectoryThresholds.Default);
            bool valid = desired.CurrentValue >= 3f;
            failure = valid ? null : "Resolved desired State fell below the enabled profile minimum.";
            return valid;
        }

        private static bool VerifyNaturalOvershootRecovery(out string failure)
        {
            AxiomDynamicsParameters parameters = AxiomDynamicsParameters.DefaultFor(AxiomKind.Mass);
            parameters.DesiredState = 1f;
            parameters.StateRestoration = 4f;
            ThreeStateAxiomDynamicsChannel channel = new ThreeStateAxiomDynamicsChannel(AxiomKind.Mass, parameters);
            channel.ApplyControlCorrection(AxiomErrorKind.State, 1f, 5f, 0f);
            AxiomTrajectoryState start = channel.GetActualTrajectory(0f, AxiomTrajectoryThresholds.Default);
            AxiomTrajectoryState later = channel.GetActualTrajectory(.5f, AxiomTrajectoryThresholds.Default);
            bool valid = start.CurrentValue > 1f
                && later.CurrentValue < start.CurrentValue
                && later.ShortRate < 0f;
            failure = valid ? null : "An above-band State did not naturally recover without a negative player input.";
            return valid;
        }

        private static bool VerifyControlledEquilibriumDwellAndBreak(out string failure)
        {
            AxiomControlledEquilibriumTracker tracker = new AxiomControlledEquilibriumTracker();
            AxiomControlBandStatus controlled = new AxiomControlBandStatus(true, true, true);
            bool before = tracker.Advance(controlled, .5f, 0f)
                || tracker.Advance(controlled, .5f, .49f);
            bool after = tracker.Advance(controlled, .5f, .5f);
            bool broken = !tracker.Advance(new AxiomControlBandStatus(false, true, true), .5f, .6f);
            bool valid = !before && after && broken;
            failure = valid ? null : "Controlled Equilibrium did not require dwell time or clear when a dimension left its band.";
            return valid;
        }

        private static bool VerifyEquilibriumDoesNotFreezeDynamics(out string failure)
        {
            ThreeStateAxiomDynamicsChannel channel = NewChannel(AxiomKind.Potential);
            channel.ApplyInput(1f, 0f);
            AxiomTrajectoryState before = channel.GetActualTrajectory(.1f, AxiomTrajectoryThresholds.Default);
            AxiomControlledEquilibriumTracker tracker = new AxiomControlledEquilibriumTracker();
            tracker.Advance(new AxiomControlBandStatus(true, true, true), 0f, .1f);
            AxiomTrajectoryState after = channel.GetActualTrajectory(.3f, AxiomTrajectoryThresholds.Default);
            bool valid = tracker.IsControlled
                && (!Near(before.CurrentValue, after.CurrentValue)
                    || !Near(before.ShortRate, after.ShortRate)
                    || !Near(before.ShortAcceleration, after.ShortAcceleration));
            failure = valid ? null : "Controlled Equilibrium mutated or froze the dynamics channel.";
            return valid;
        }

        private static bool VerifyAllEightAndNumericalBounds(out string failure)
        {
            AxiomKind[] kinds =
            {
                AxiomKind.Heat, AxiomKind.Flow, AxiomKind.Mass, AxiomKind.Compression,
                AxiomKind.Potential, AxiomKind.Resonance, AxiomKind.Phase, AxiomKind.Order
            };
            for (int index = 0; index < kinds.Length; index++)
            {
                AxiomDynamicsParameters parameters = AxiomDynamicsParameters.DefaultFor(kinds[index]);
                parameters.MaximumAdvanceSeconds = 2f;
                ThreeStateAxiomDynamicsChannel channel = new ThreeStateAxiomDynamicsChannel(kinds[index], parameters);
                channel.ApplyInput(100000f, 0f);
                AxiomTrajectoryState trajectory = channel.GetActualTrajectory(100000f, AxiomTrajectoryThresholds.Default);
                if (!Finite(trajectory.CurrentValue) || !Finite(trajectory.ShortRate) || !Finite(trajectory.ShortAcceleration)
                    || trajectory.CurrentValue < parameters.MinimumState || trajectory.CurrentValue > parameters.MaximumState
                    || Abs(trajectory.ShortRate) > parameters.MaximumAbsoluteRate
                    || Abs(trajectory.ShortAcceleration) > parameters.MaximumAbsoluteAcceleration)
                {
                    failure = "A bounded three-state channel failed for " + kinds[index] + ".";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static ThreeStateAxiomDynamicsChannel NewChannel(AxiomKind kind)
        {
            return new ThreeStateAxiomDynamicsChannel(kind, AxiomDynamicsParameters.DefaultFor(kind));
        }

        private static AxiomControlReferenceProfile StateOnly(float tolerance)
        {
            AxiomControlReferenceProfile profile = AxiomControlReferenceProfile.DefaultFor(AxiomKind.Heat);
            profile.Reference.EvaluateState = true;
            profile.Reference.StateTolerance = tolerance;
            profile.Reference.EvaluateRate = false;
            profile.Reference.EvaluateAcceleration = false;
            return profile;
        }

        private static AxiomTrajectoryState Static(float state, float rate, float acceleration)
        {
            return new AxiomTrajectoryState(AxiomKind.Heat, state, rate, rate, acceleration,
                true, true, true, AxiomTrajectoryDirection.Stable, AxiomRateIntensity.Slow,
                AxiomCurvature.StableRate, 0f);
        }

        private static bool Near(float left, float right) => Abs(left - right) <= Tolerance;
        private static float Abs(float value) => value < 0f ? -value : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
