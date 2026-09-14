using System;

namespace Cave.Axioms.Control
{
    public enum AxiomTemporalLockStage { Uncontrolled, State, Rate, Acceleration }

    [Serializable]
    public struct AxiomTemporalControlProfile
    {
        public float ActivityDecaySeconds;
        public float ApplicationImpulse;
        public float MaximumActivity;
        public float MaximumAdvanceSeconds;
        public float MinimumSampleSeconds;
        public float StateAcquireTolerance;
        public float StateHoldTolerance;
        public float RateAcquireTolerance;
        public float RateHoldTolerance;
        public float AccelerationAcquireTolerance;
        public float AccelerationHoldTolerance;
        public float LockAcquirePerSecond;
        public float LockDecayPerSecond;
        public float LockAcquireThreshold;
        public float LockReleaseThreshold;

        public static AxiomTemporalControlProfile DefaultFor(AxiomKind kind)
        {
            return new AxiomTemporalControlProfile
            {
                ActivityDecaySeconds = .9f,
                ApplicationImpulse = .34f,
                MaximumActivity = 2f,
                MaximumAdvanceSeconds = 8f,
                MinimumSampleSeconds = .08f,
                StateAcquireTolerance = .24f,
                StateHoldTolerance = .34f,
                RateAcquireTolerance = 1.25f,
                RateHoldTolerance = 1.65f,
                AccelerationAcquireTolerance = 5.5f,
                AccelerationHoldTolerance = 7f,
                LockAcquirePerSecond = .72f,
                LockDecayPerSecond = .28f,
                LockAcquireThreshold = .78f,
                LockReleaseThreshold = .54f
            };
        }

        public AxiomTemporalControlProfile Sanitized(AxiomKind kind)
        {
            AxiomTemporalControlProfile fallback = DefaultFor(kind);
            AxiomTemporalControlProfile value = this;
            value.ActivityDecaySeconds = Positive(value.ActivityDecaySeconds, fallback.ActivityDecaySeconds);
            value.ApplicationImpulse = Positive(value.ApplicationImpulse, fallback.ApplicationImpulse);
            value.MaximumActivity = Positive(value.MaximumActivity, fallback.MaximumActivity);
            value.MaximumAdvanceSeconds = Positive(value.MaximumAdvanceSeconds, fallback.MaximumAdvanceSeconds);
            value.MinimumSampleSeconds = Positive(value.MinimumSampleSeconds, fallback.MinimumSampleSeconds);
            value.StateAcquireTolerance = Positive(value.StateAcquireTolerance, fallback.StateAcquireTolerance);
            value.RateAcquireTolerance = Positive(value.RateAcquireTolerance, fallback.RateAcquireTolerance);
            value.AccelerationAcquireTolerance = Positive(value.AccelerationAcquireTolerance, fallback.AccelerationAcquireTolerance);
            value.StateHoldTolerance = AtLeast(value.StateHoldTolerance, value.StateAcquireTolerance, fallback.StateHoldTolerance);
            value.RateHoldTolerance = AtLeast(value.RateHoldTolerance, value.RateAcquireTolerance, fallback.RateHoldTolerance);
            value.AccelerationHoldTolerance = AtLeast(value.AccelerationHoldTolerance, value.AccelerationAcquireTolerance, fallback.AccelerationHoldTolerance);
            value.LockAcquirePerSecond = Positive(value.LockAcquirePerSecond, fallback.LockAcquirePerSecond);
            value.LockDecayPerSecond = Positive(value.LockDecayPerSecond, fallback.LockDecayPerSecond);
            value.LockAcquireThreshold = Clamp01(value.LockAcquireThreshold, fallback.LockAcquireThreshold);
            value.LockReleaseThreshold = Clamp01(value.LockReleaseThreshold, fallback.LockReleaseThreshold);
            if (value.LockReleaseThreshold >= value.LockAcquireThreshold)
            {
                value.LockReleaseThreshold = Math.Max(0f, value.LockAcquireThreshold - .05f);
            }
            return value;
        }

        private static float Positive(float value, float fallback) => IsFinite(value) && value > 0f ? value : fallback;
        private static float AtLeast(float value, float minimum, float fallback) => IsFinite(value) && value > minimum ? value : Math.Max(minimum + .01f, fallback);
        private static float Clamp01(float value, float fallback) => !IsFinite(value) ? fallback : value < 0f ? 0f : value > 1f ? 1f : value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public struct AxiomTemporalControlSnapshot
    {
        public AxiomTemporalControlSnapshot(AxiomKind kind, float activity, float rate, float acceleration, float stateLock, float rateLock, float accelerationLock, bool stateHeld, bool rateHeld, bool accelerationHeld)
        {
            Kind = kind; Activity = activity; Rate = rate; Acceleration = acceleration;
            StateLock = stateLock; RateLock = rateLock; AccelerationLock = accelerationLock;
            StateHeld = stateHeld; RateHeld = rateHeld; AccelerationHeld = accelerationHeld;
        }
        public AxiomKind Kind { get; }
        public float Activity { get; }
        public float Rate { get; }
        public float Acceleration { get; }
        public float StateLock { get; }
        public float RateLock { get; }
        public float AccelerationLock { get; }
        public bool StateHeld { get; }
        public bool RateHeld { get; }
        public bool AccelerationHeld { get; }
        public AxiomTemporalLockStage PresentationStage => AccelerationHeld ? AxiomTemporalLockStage.Acceleration : RateHeld ? AxiomTemporalLockStage.Rate : StateHeld ? AxiomTemporalLockStage.State : AxiomTemporalLockStage.Uncontrolled;
    }

    /// <summary>Finite-memory activity estimator and simultaneous tolerance-band locks.</summary>
    public sealed class AxiomTemporalControlChannel
    {
        private readonly AxiomKind kind;
        private readonly AxiomTemporalControlProfile profile;
        private float activity, rate, acceleration;
        private float stateLock, rateLock, accelerationLock;
        private bool stateHeld, rateHeld, accelerationHeld, hasTime;
        private float lastTime, lastApplicationTime;
        private bool hasApplicationTime;

        public AxiomTemporalControlChannel(AxiomKind kind, AxiomTemporalControlProfile profile)
        {
            this.kind = kind;
            this.profile = profile.Sanitized(kind);
        }

        public void RecordSuccessfulApplication(float amount, float timestamp, AxiomTrajectoryReference reference)
        {
            AdvanceTo(timestamp, reference);
            float impulse = Math.Max(0f, Finite(amount) ? amount : 0f) * profile.ApplicationImpulse;
            if (impulse <= 0f) return;
            float spacing = hasApplicationTime ? Math.Max(profile.MinimumSampleSeconds, timestamp - lastApplicationTime) : profile.MinimumSampleSeconds;
            float previousRate = rate;
            activity = Clamp(activity + impulse, profile.MaximumActivity);
            rate = ClampSigned(rate + impulse / spacing, profile.MaximumActivity / profile.MinimumSampleSeconds);
            acceleration = ClampSigned((rate - previousRate) / spacing, profile.MaximumActivity / (profile.MinimumSampleSeconds * profile.MinimumSampleSeconds));
            lastApplicationTime = timestamp;
            hasApplicationTime = true;
        }

        public AxiomTemporalControlSnapshot AdvanceTo(float timestamp, AxiomTrajectoryReference reference)
        {
            if (!hasTime)
            {
                hasTime = true;
                lastTime = timestamp;
                return Snapshot();
            }
            if (!Finite(timestamp) || timestamp <= lastTime) return Snapshot();
            float delta = Math.Min(profile.MaximumAdvanceSeconds, timestamp - lastTime);
            float previousActivity = activity;
            float previousRate = rate;
            float decay = (float)Math.Exp(-delta / profile.ActivityDecaySeconds);
            activity = Clamp(activity * decay, profile.MaximumActivity);
            rate = ClampSigned((activity - previousActivity) / Math.Max(profile.MinimumSampleSeconds, delta), profile.MaximumActivity / profile.MinimumSampleSeconds);
            acceleration = ClampSigned((rate - previousRate) / Math.Max(profile.MinimumSampleSeconds, delta), profile.MaximumActivity / (profile.MinimumSampleSeconds * profile.MinimumSampleSeconds));
            UpdateLocks(reference, delta);
            lastTime = timestamp;
            return Snapshot();
        }

        public AxiomTemporalControlSnapshot Snapshot() => new AxiomTemporalControlSnapshot(kind, activity, rate, acceleration, stateLock, rateLock, accelerationLock, stateHeld, rateHeld, accelerationHeld);

        private void UpdateLocks(AxiomTrajectoryReference reference, float delta)
        {
            UpdateLock(ref stateLock, ref stateHeld, reference.EvaluateState, activity - reference.State, profile.StateAcquireTolerance, profile.StateHoldTolerance, delta);
            UpdateLock(ref rateLock, ref rateHeld, reference.EvaluateRate, rate - reference.Rate, profile.RateAcquireTolerance, profile.RateHoldTolerance, delta);
            UpdateLock(ref accelerationLock, ref accelerationHeld, reference.EvaluateAcceleration, acceleration - reference.Acceleration, profile.AccelerationAcquireTolerance, profile.AccelerationHoldTolerance, delta);
        }

        private void UpdateLock(ref float confidence, ref bool held, bool enabled, float difference, float acquire, float hold, float delta)
        {
            bool accepted = enabled && Math.Abs(difference) <= (held ? hold : acquire);
            confidence = Clamp01(confidence + (accepted ? profile.LockAcquirePerSecond : -profile.LockDecayPerSecond) * delta);
            if (!held && confidence >= profile.LockAcquireThreshold) held = true;
            else if (held && confidence <= profile.LockReleaseThreshold) held = false;
        }

        private static float Clamp(float value, float maximum) => !Finite(value) || value < 0f ? 0f : value > maximum ? maximum : value;
        private static float ClampSigned(float value, float maximum) => !Finite(value) ? 0f : value < -maximum ? -maximum : value > maximum ? maximum : value;
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
