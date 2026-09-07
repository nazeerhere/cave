namespace Cave.Axioms
{
    /// <summary>Deterministic validation for qualitative trajectory and error APIs.</summary>
    public static class AxiomRuntimeVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyTrajectoryClassification(out failure)
                && VerifyErrorState(out failure);
        }

        private static bool VerifyTrajectoryClassification(out string failure)
        {
            AxiomTrajectoryThresholds thresholds = new AxiomTrajectoryThresholds(0.05f, 1f, 0.05f);
            AxiomObservationChannel steadyIncrease = NewChannel();
            Apply(steadyIncrease, 0f, 0f, 1f, 1f, 2f, 2f, 3f, 3f);
            AxiomTrajectoryState steady = steadyIncrease.GetTrajectory(thresholds);
            if (steady.Direction != AxiomTrajectoryDirection.Rising
                || steady.Curvature != AxiomCurvature.StableRate)
            {
                failure = "Steady increase did not classify as rising with a stable rate.";
                return false;
            }

            AxiomObservationChannel accelerating = NewChannel();
            Apply(accelerating, 0f, 0f, 1f, 1f, 2f, 4f, 3f, 9f);
            AxiomTrajectoryState upwardCurve = accelerating.GetTrajectory(thresholds);
            if (upwardCurve.Direction != AxiomTrajectoryDirection.Rising
                || upwardCurve.Curvature != AxiomCurvature.Accelerating)
            {
                failure = "Accelerating increase did not classify as rising and accelerating.";
                return false;
            }

            AxiomObservationChannel decelerating = NewChannel();
            Apply(decelerating, 0f, 0f, 1f, 3f, 2f, 5f, 3f, 6f);
            AxiomTrajectoryState downwardCurve = decelerating.GetTrajectory(thresholds);
            if (downwardCurve.Direction != AxiomTrajectoryDirection.Rising
                || downwardCurve.Curvature != AxiomCurvature.Decelerating)
            {
                failure = "Decelerating increase did not classify as rising and decelerating.";
                return false;
            }

            AxiomObservationChannel falling = NewChannel();
            Apply(falling, 0f, 3f, 1f, 2f, 2f, 1f, 3f, 0f);
            if (falling.GetTrajectory(thresholds).Direction != AxiomTrajectoryDirection.Falling)
            {
                failure = "Steady decrease did not classify as falling.";
                return false;
            }

            AxiomObservationChannel stable = NewChannel();
            Apply(stable, 0f, 3f, 1f, 3f, 2f, 3f, 3f, 3f);
            if (stable.GetTrajectory(thresholds).Direction != AxiomTrajectoryDirection.Stable)
            {
                failure = "Constant state did not classify as stable.";
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyErrorState(out string failure)
        {
            AxiomTrajectoryState observation = new AxiomTrajectoryState(
                AxiomKind.Heat,
                4f,
                2f,
                2f,
                1f,
                true,
                true,
                true,
                AxiomTrajectoryDirection.Rising,
                AxiomRateIntensity.Fast,
                AxiomCurvature.Accelerating,
                3f);

            AxiomTrajectoryReference stateReference = new AxiomTrajectoryReference
            {
                EvaluateState = true,
                State = 0f,
                StateTolerance = 0.1f
            };
            if (AxiomErrorEvaluator.Evaluate(observation, stateReference, 3f).ErrorKind != AxiomErrorKind.State)
            {
                failure = "State mismatch did not classify as a state error.";
                return false;
            }

            AxiomTrajectoryReference rateReference = new AxiomTrajectoryReference
            {
                EvaluateRate = true,
                Rate = 0f,
                RateTolerance = 0.1f
            };
            if (AxiomErrorEvaluator.Evaluate(observation, rateReference, 3f).ErrorKind != AxiomErrorKind.Rate)
            {
                failure = "Rate mismatch did not classify as a rate error.";
                return false;
            }

            AxiomTrajectoryReference accelerationReference = new AxiomTrajectoryReference
            {
                EvaluateAcceleration = true,
                Acceleration = 0f,
                AccelerationTolerance = 0.1f
            };
            if (AxiomErrorEvaluator.Evaluate(observation, accelerationReference, 3f).ErrorKind != AxiomErrorKind.Acceleration)
            {
                failure = "Curvature mismatch did not classify as an acceleration error.";
                return false;
            }

            AxiomTrajectoryReference matchedReference = new AxiomTrajectoryReference
            {
                EvaluateState = true,
                State = 4f,
                StateTolerance = 0.1f,
                EvaluateRate = true,
                Rate = 2f,
                RateTolerance = 0.1f,
                EvaluateAcceleration = true,
                Acceleration = 1f,
                AccelerationTolerance = 0.1f
            };
            if (AxiomErrorEvaluator.Evaluate(observation, matchedReference, 3f).ErrorKind != AxiomErrorKind.None)
            {
                failure = "Matching reference did not classify as no error.";
                return false;
            }

            failure = null;
            return true;
        }

        private static AxiomObservationChannel NewChannel()
        {
            return new AxiomObservationChannel(AxiomKind.Heat, 16, 1f, 2f, 0.0001f);
        }

        private static void Apply(
            AxiomObservationChannel channel,
            float time0,
            float value0,
            float time1,
            float value1,
            float time2,
            float value2,
            float time3,
            float value3)
        {
            channel.RecordValue(value0, time0);
            channel.RecordValue(value1, time1);
            channel.RecordValue(value2, time2);
            channel.RecordValue(value3, time3);
        }
    }
}
