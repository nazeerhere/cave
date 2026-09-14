namespace Cave.Axioms.Control
{
    /// <summary>Deterministic finite-memory checks for activity and hysteretic locks.</summary>
    public static class AxiomTemporalControlVerification
    {
        public static bool VerifyAll()
        {
            return RegularApplicationsBuildActivity() && ActivityDecaysWithoutInput()
                && MissDoesNotZeroActivityOrConfidence() && RepeatedPoorTimingUnlocks()
                && RecoveryReacquires() && AcquireIsStricterThanHold()
                && AllDimensionsAreBoundedAndSimultaneous() && EqualStacksCanDifferByTiming();
        }

        private static bool RegularApplicationsBuildActivity()
        {
            AxiomTemporalControlChannel channel = NewChannel();
            for (int i = 0; i < 4; i++) channel.RecordSuccessfulApplication(1f, i * .3f, Reference());
            return channel.AdvanceTo(1f, Reference()).Activity > .1f;
        }

        private static bool ActivityDecaysWithoutInput()
        {
            AxiomTemporalControlChannel channel = NewChannel(); channel.RecordSuccessfulApplication(1f, 0f, Reference());
            float early = channel.AdvanceTo(.1f, Reference()).Activity; float late = channel.AdvanceTo(2f, Reference()).Activity;
            return late < early && late >= 0f;
        }

        private static bool MissDoesNotZeroActivityOrConfidence()
        {
            AxiomTemporalControlChannel channel = NewChannel(); AxiomTrajectoryReference reference = StateOnlyReference(1f);
            for (int i = 0; i < 6; i++) { channel.RecordSuccessfulApplication(1f, i * .2f, reference); channel.AdvanceTo(i * .2f + .15f, reference); }
            AxiomTemporalControlSnapshot before = channel.Snapshot(); AxiomTemporalControlSnapshot after = channel.AdvanceTo(1.35f, reference);
            return before.Activity > 0f && after.Activity > 0f && after.StateLock > 0f && after.StateLock < 1f;
        }

        private static bool RepeatedPoorTimingUnlocks()
        {
            AxiomTemporalControlChannel channel = NewChannel(); AxiomTrajectoryReference stable = StateOnlyReference(0f);
            for (int i = 0; i < 8; i++) channel.AdvanceTo((i + 1) * .2f, stable);
            for (int i = 0; i < 80; i++) { channel.RecordSuccessfulApplication(1f, 2f + i * .05f, Reference()); channel.AdvanceTo(2.03f + i * .05f, Reference()); }
            return !channel.Snapshot().StateHeld;
        }

        private static bool RecoveryReacquires()
        {
            AxiomTemporalControlChannel channel = NewChannel(); AxiomTrajectoryReference stable = StateOnlyReference(0f);
            for (int i = 0; i < 12; i++) channel.AdvanceTo((i + 1) * .2f, stable);
            return channel.Snapshot().StateHeld;
        }

        private static bool AcquireIsStricterThanHold()
        {
            AxiomTemporalControlProfile p = AxiomTemporalControlProfile.DefaultFor(AxiomKind.Heat).Sanitized(AxiomKind.Heat);
            return p.StateAcquireTolerance < p.StateHoldTolerance && p.RateAcquireTolerance < p.RateHoldTolerance && p.AccelerationAcquireTolerance < p.AccelerationHoldTolerance;
        }

        private static bool AllDimensionsAreBoundedAndSimultaneous()
        {
            AxiomTemporalControlChannel channel = NewChannel();
            for (int i = 0; i < 20; i++) { channel.RecordSuccessfulApplication(1f, i * .13f, LooseReference()); channel.AdvanceTo(i * .13f + .08f, LooseReference()); }
            AxiomTemporalControlSnapshot s = channel.Snapshot();
            return Bounded(s.StateLock) && Bounded(s.RateLock) && Bounded(s.AccelerationLock);
        }

        private static bool EqualStacksCanDifferByTiming()
        {
            AxiomTemporalControlChannel rapid = NewChannel(), spaced = NewChannel();
            for (int i = 0; i < 4; i++) { rapid.RecordSuccessfulApplication(1f, i * .12f, Reference()); spaced.RecordSuccessfulApplication(1f, i * .8f, Reference()); }
            AxiomTemporalControlSnapshot a = rapid.AdvanceTo(2.6f, Reference()), b = spaced.AdvanceTo(2.6f, Reference());
            return Abs(a.Activity - b.Activity) > .001f || Abs(a.Rate - b.Rate) > .001f;
        }

        private static AxiomTemporalControlChannel NewChannel() => new AxiomTemporalControlChannel(AxiomKind.Heat, AxiomTemporalControlProfile.DefaultFor(AxiomKind.Heat));
        private static AxiomTrajectoryReference Reference() => new AxiomTrajectoryReference { EvaluateState = true, State = .7f, EvaluateRate = true, Rate = 0f, EvaluateAcceleration = true, Acceleration = 0f };
        private static AxiomTrajectoryReference LooseReference() => new AxiomTrajectoryReference { EvaluateState = true, State = 0f, EvaluateRate = true, Rate = 0f, EvaluateAcceleration = true, Acceleration = 0f };
        private static AxiomTrajectoryReference StateOnlyReference(float state) => new AxiomTrajectoryReference { EvaluateState = true, State = state };
        private static bool Bounded(float value) => value >= 0f && value <= 1f;
        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
