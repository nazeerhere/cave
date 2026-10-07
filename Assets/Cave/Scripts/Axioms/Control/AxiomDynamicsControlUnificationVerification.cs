using Cave.Axioms.Elemental;

namespace Cave.Axioms.Control
{
    /// <summary>
    /// Compatibility entry point for the single actor-dynamics -> desired S/R/A
    /// -> signed-control path. Legacy helper cases remain below for historical
    /// diagnosis, while active regression now delegates authority coverage to
    /// the production ThreeStateAxiomDynamicsChannel verifier.
    /// </summary>
    public static class AxiomDynamicsControlUnificationVerification
    {
        private const float Tolerance = .0001f;

        public static bool TryRunAll(out string failure)
        {
            if (!AuthoritativeThreeStateControlVerification.TryRunAll(out failure)
                || !VerifySignCorrectness(out failure)
                || !VerifyPolicyDoesNotRedefineDesiredTrajectory(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifySameInputDifferentActorProfiles(out string failure)
        {
            AxiomDynamicsChannel first = NewActorChannel(CreateSlowHeatProfile());
            AxiomDynamicsChannel second = NewActorChannel(CreateFastHeatProfile());
            AxiomTrajectoryState firstTrajectory = ApplyHeatSequence(first);
            AxiomTrajectoryState secondTrajectory = ApplyHeatSequence(second);

            bool valid = firstTrajectory.CurrentValue < secondTrajectory.CurrentValue - Tolerance
                && firstTrajectory.HasShortRate
                && secondTrajectory.HasShortRate;
            failure = valid ? null : "Identical Heat input did not produce distinct actor-profile trajectories.";
            return valid;
        }

        private static bool VerifyRepeatability(out string failure)
        {
            AxiomDynamicsChannel first = NewActorChannel(CreateFastHeatProfile());
            AxiomDynamicsChannel second = NewActorChannel(CreateFastHeatProfile());
            AxiomTrajectoryState left = ApplyHeatSequence(first);
            AxiomTrajectoryState right = ApplyHeatSequence(second);
            bool valid = Near(left.CurrentValue, right.CurrentValue)
                && Near(left.ShortRate, right.ShortRate)
                && Near(left.ShortAcceleration, right.ShortAcceleration);
            failure = valid ? null : "The same actor profile and Heat input sequence was not repeatable.";
            return valid;
        }

        private static bool VerifyControlUsesActorTrajectory(out string failure)
        {
            AxiomTrajectoryState actual = StaticState(.5f, 0f, 0f, false, false);
            AxiomTrajectoryState slowDesired = ApplyHeatSequence(NewActorChannel(CreateSlowHeatProfile()));
            AxiomTrajectoryState fastDesired = ApplyHeatSequence(NewActorChannel(CreateFastHeatProfile()));
            AxiomControlReferenceProfile policy = StateOnlyPolicy(.001f);
            AxiomErrorState slowError = AxiomDynamicsControlResolver.Evaluate(actual, slowDesired, policy, .5f);
            AxiomErrorState fastError = AxiomDynamicsControlResolver.Evaluate(actual, fastDesired, policy, .5f);

            bool valid = slowError.ErrorKind == AxiomErrorKind.State
                && fastError.ErrorKind == AxiomErrorKind.State
                && slowError.SignedDifference > 0f
                && fastError.SignedDifference < 0f
                && AxiomDynamicsControlResolver.RequiredCorrectionDirection(slowError) < 0f
                && AxiomDynamicsControlResolver.RequiredCorrectionDirection(fastError) > 0f;
            failure = valid ? null : "Control direction was not derived from each actor's own desired trajectory.";
            return valid;
        }

        private static bool VerifySignCorrectness(out string failure)
        {
            AxiomTrajectoryState positive = StaticState(2f, 3f, 4f, true, true);
            AxiomTrajectoryState desired = StaticState(1f, 2f, 3f, true, true);
            AxiomTrajectoryState negative = StaticState(0f, 1f, 2f, true, true);

            AxiomErrorState statePositive = AxiomDynamicsControlResolver.Evaluate(positive, desired, StateOnlyPolicy(.01f), 1f);
            AxiomErrorState stateNegative = AxiomDynamicsControlResolver.Evaluate(negative, desired, StateOnlyPolicy(.01f), 1f);
            AxiomErrorState ratePositive = AxiomDynamicsControlResolver.Evaluate(positive, desired, RateOnlyPolicy(.01f), 1f);
            AxiomErrorState rateNegative = AxiomDynamicsControlResolver.Evaluate(negative, desired, RateOnlyPolicy(.01f), 1f);
            AxiomErrorState accelerationPositive = AxiomDynamicsControlResolver.Evaluate(positive, desired, AccelerationOnlyPolicy(.01f), 1f);
            AxiomErrorState accelerationNegative = AxiomDynamicsControlResolver.Evaluate(negative, desired, AccelerationOnlyPolicy(.01f), 1f);

            bool valid = HasDirection(statePositive, AxiomErrorKind.State, -1f)
                && HasDirection(stateNegative, AxiomErrorKind.State, 1f)
                && HasDirection(ratePositive, AxiomErrorKind.Rate, -1f)
                && HasDirection(rateNegative, AxiomErrorKind.Rate, 1f)
                && HasDirection(accelerationPositive, AxiomErrorKind.Acceleration, -1f)
                && HasDirection(accelerationNegative, AxiomErrorKind.Acceleration, 1f);
            failure = valid ? null : "Signed State, Rate, or Acceleration errors did not map to the corrective direction.";
            return valid;
        }

        private static bool VerifyPolicyDoesNotRedefineDesiredTrajectory(out string failure)
        {
            AxiomTrajectoryState desired = StaticState(.42f, -.3f, .7f, true, true);
            AxiomControlReferenceProfile first = StateOnlyPolicy(.1f);
            AxiomControlReferenceProfile second = StateOnlyPolicy(.9f);
            second.OpportunityLifetime = 9f;
            second.MinimumErrorMagnitude = .9f;
            second.Reference.State = 99f;
            second.Reference.Rate = -99f;
            second.Reference.Acceleration = 99f;
            AxiomTrajectoryReference firstReference = AxiomDynamicsControlResolver.BuildReference(desired, first);
            AxiomTrajectoryReference secondReference = AxiomDynamicsControlResolver.BuildReference(desired, second);

            bool valid = Near(firstReference.State, desired.CurrentValue)
                && Near(secondReference.State, desired.CurrentValue)
                && Near(firstReference.Rate, desired.ShortRate)
                && Near(secondReference.Rate, desired.ShortRate)
                && Near(firstReference.Acceleration, desired.ShortAcceleration)
                && Near(secondReference.Acceleration, desired.ShortAcceleration);
            failure = valid ? null : "Control-policy metadata redefined the dynamics-derived desired trajectory.";
            return valid;
        }

        private static bool VerifyFiniteBoundedDynamics(out string failure)
        {
            AxiomDynamicsChannel channel = NewActorChannel(CreateFastHeatProfile());
            for (int index = 0; index < 256; index++)
            {
                channel.ApplyInput(.5f, .5f, index * .03f);
                AxiomTrajectoryState trajectory = channel.GetDesiredTrajectory(
                    (index * .03f) + .02f,
                    AxiomTrajectoryThresholds.Default);
                if (!Finite(trajectory.CurrentValue)
                    || !Finite(trajectory.ShortRate)
                    || !Finite(trajectory.ShortAcceleration)
                    || trajectory.CurrentValue < 0f
                    || trajectory.CurrentValue > 1.0001f)
                {
                    failure = "Dynamics-derived desired trajectory became non-finite or escaped its bounded response range.";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static AxiomDynamicsChannel NewActorChannel(AxiomDynamicsParameters parameters)
        {
            // Historical-only diagnostic channel: the caller has already
            // supplied the actor-local parameters, so this does not need a
            // MonoBehaviour profile-resolution path.
            return new AxiomDynamicsChannel(AxiomKind.Heat, parameters);
        }

        private static AxiomTrajectoryState ApplyHeatSequence(AxiomDynamicsChannel channel)
        {
            channel.ApplyInput(.5f, .5f, 0f);
            return channel.GetDesiredTrajectory(.5f, AxiomTrajectoryThresholds.Default);
        }

        private static AxiomDynamicsParameters CreateSlowHeatProfile()
        {
            AxiomDynamicsParameters parameters = AxiomDynamicsParameters.DefaultFor(AxiomKind.Heat);
            parameters.InputGain = .02f;
            parameters.StackGain = .01f;
            return parameters;
        }

        private static AxiomDynamicsParameters CreateFastHeatProfile()
        {
            AxiomDynamicsParameters parameters = AxiomDynamicsParameters.DefaultFor(AxiomKind.Heat);
            parameters.InputGain = 3f;
            parameters.StackGain = .5f;
            return parameters;
        }

        private static AxiomControlReferenceProfile StateOnlyPolicy(float tolerance)
        {
            AxiomControlReferenceProfile policy = AxiomControlReferenceProfile.DefaultFor(AxiomKind.Heat);
            policy.Reference.EvaluateState = true;
            policy.Reference.StateTolerance = tolerance;
            policy.Reference.EvaluateRate = false;
            policy.Reference.EvaluateAcceleration = false;
            return policy;
        }

        private static AxiomControlReferenceProfile RateOnlyPolicy(float tolerance)
        {
            AxiomControlReferenceProfile policy = StateOnlyPolicy(tolerance);
            policy.Reference.EvaluateState = false;
            policy.Reference.EvaluateRate = true;
            policy.Reference.RateTolerance = tolerance;
            return policy;
        }

        private static AxiomControlReferenceProfile AccelerationOnlyPolicy(float tolerance)
        {
            AxiomControlReferenceProfile policy = StateOnlyPolicy(tolerance);
            policy.Reference.EvaluateState = false;
            policy.Reference.EvaluateAcceleration = true;
            policy.Reference.AccelerationTolerance = tolerance;
            return policy;
        }

        private static AxiomTrajectoryState StaticState(
            float state,
            float rate,
            float acceleration,
            bool hasRate,
            bool hasAcceleration)
        {
            return new AxiomTrajectoryState(
                AxiomKind.Heat,
                state,
                rate,
                rate,
                acceleration,
                hasRate,
                hasRate,
                hasAcceleration,
                AxiomTrajectoryDirection.Stable,
                AxiomRateIntensity.Slow,
                AxiomCurvature.StableRate,
                0f);
        }

        private static bool HasDirection(AxiomErrorState error, AxiomErrorKind kind, float direction)
        {
            return error.ErrorKind == kind
                && AxiomDynamicsControlResolver.RequiredCorrectionDirection(error) == direction;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Near(float left, float right)
        {
            float difference = left - right;
            return difference <= Tolerance && difference >= -Tolerance;
        }
    }
}
