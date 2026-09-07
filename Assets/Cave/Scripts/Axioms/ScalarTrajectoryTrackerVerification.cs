namespace Cave.Axioms
{
    /// <summary>
    /// Small deterministic coverage for the pure trajectory core. It deliberately
    /// avoids a Unity test dependency because this project has no test assembly.
    /// It is safe for an editor command, CI wrapper, or temporary development call.
    /// </summary>
    public static class ScalarTrajectoryTrackerVerification
    {
        public static bool TryRunAll(out string failure)
        {
            if (!VerifyFirstSampleIsObservationOnly(out failure)
                || !VerifyConstant(out failure)
                || !VerifyLinearIncrease(out failure)
                || !VerifyLinearDecrease(out failure)
                || !VerifyAcceleratingIncrease(out failure)
                || !VerifyDeceleratingIncrease(out failure)
                || !VerifyIrregularTiming(out failure))
            {
                return false;
            }

            failure = null;
            return true;
        }

        private static bool VerifyFirstSampleIsObservationOnly(out string failure)
        {
            ScalarTrajectoryTracker tracker = NewUnitTracker();
            tracker.RecordValue(0f, 3f);
            return Expect(!tracker.HasShortRate
                && !tracker.HasLongRate
                && !tracker.HasShortAcceleration,
                "The first sample exposed a derivative before enough history existed.",
                out failure);
        }

        private static bool VerifyConstant(out string failure)
        {
            ScalarTrajectoryTracker tracker = NewUnitTracker();
            tracker.RecordValue(0f, 3f);
            tracker.RecordValue(1f, 3f);
            tracker.RecordValue(2f, 3f);
            tracker.RecordValue(3f, 3f);
            return Expect(tracker.HasShortRate && tracker.HasLongRate && tracker.HasShortAcceleration
                && Approximately(tracker.ShortRate, 0f)
                && Approximately(tracker.LongRate, 0f)
                && Approximately(tracker.ShortAcceleration, 0f), "Constant sequence did not remain at zero rate/acceleration.", out failure);
        }

        private static bool VerifyLinearIncrease(out string failure)
        {
            ScalarTrajectoryTracker tracker = NewUnitTracker();
            tracker.RecordValue(0f, 0f);
            tracker.RecordValue(1f, 2f);
            tracker.RecordValue(2f, 4f);
            tracker.RecordValue(3f, 6f);
            return Expect(Approximately(tracker.ShortRate, 2f)
                && Approximately(tracker.LongRate, 2f)
                && Approximately(tracker.ShortAcceleration, 0f), "Linear increase did not produce a stable positive rate.", out failure);
        }

        private static bool VerifyLinearDecrease(out string failure)
        {
            ScalarTrajectoryTracker tracker = NewUnitTracker();
            tracker.RecordValue(0f, 6f);
            tracker.RecordValue(1f, 4f);
            tracker.RecordValue(2f, 2f);
            tracker.RecordValue(3f, 0f);
            return Expect(tracker.HasShortRate && tracker.ShortRate < 0f
                && Approximately(tracker.ShortRate, -2f), "Linear decrease lost its signed negative derivative.", out failure);
        }

        private static bool VerifyAcceleratingIncrease(out string failure)
        {
            ScalarTrajectoryTracker tracker = NewUnitTracker();
            tracker.RecordValue(0f, 0f);
            tracker.RecordValue(1f, 1f);
            tracker.RecordValue(2f, 4f);
            tracker.RecordValue(3f, 9f);
            return Expect(tracker.ShortRate > 0f && tracker.ShortAcceleration > 0f
                && Approximately(tracker.ShortRate, 5f)
                && Approximately(tracker.ShortAcceleration, 2f), "Accelerating increase did not produce positive rate-of-rate.", out failure);
        }

        private static bool VerifyDeceleratingIncrease(out string failure)
        {
            ScalarTrajectoryTracker tracker = NewUnitTracker();
            tracker.RecordValue(0f, 0f);
            tracker.RecordValue(1f, 3f);
            tracker.RecordValue(2f, 5f);
            tracker.RecordValue(3f, 6f);
            return Expect(tracker.ShortRate > 0f && tracker.ShortAcceleration < 0f
                && Approximately(tracker.ShortRate, 1f)
                && Approximately(tracker.ShortAcceleration, -1f), "Decelerating increase did not preserve negative rate-of-rate.", out failure);
        }

        private static bool VerifyIrregularTiming(out string failure)
        {
            ScalarTrajectoryTracker tracker = new ScalarTrajectoryTracker(16, 0.0001f, 0.0002f, 0.00001f);
            tracker.RecordValue(0f, 0f);
            tracker.RecordValue(0.5f, 1f);
            tracker.RecordValue(2f, 4f);
            return Expect(Approximately(tracker.ShortRate, 2f)
                && Approximately(tracker.LongRate, 2f), "Irregular timing was treated as sample count instead of elapsed time.", out failure);
        }

        private static ScalarTrajectoryTracker NewUnitTracker()
        {
            return new ScalarTrajectoryTracker(16, 1f, 2f, 0.0001f);
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }

        private static bool Approximately(float value, float expected)
        {
            return System.Math.Abs(value - expected) <= 0.001f;
        }
    }
}
