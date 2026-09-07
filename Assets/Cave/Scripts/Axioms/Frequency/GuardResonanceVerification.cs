namespace Cave.Axioms.Frequency
{
    /// <summary>Runtime-independent deterministic coverage for cadence math.</summary>
    public static class GuardResonanceVerification
    {
        public static bool TryRunAll(out string failure)
        {
            GuardResonanceSettings settings = new GuardResonanceSettings(0.8f, 0.15f, 2.5f, 4);
            return VerifyReference(settings, out failure)
                && VerifyFirstResonance(settings, out failure)
                && VerifyFullBreak(settings, out failure)
                && VerifyBadTimingAndRecovery(settings, out failure)
                && VerifyFastSlowAndEdges(settings, out failure)
                && VerifyStaleReference(settings, out failure)
                && VerifyIndependentTrackers(settings, out failure)
                && VerifyResetAfterBreak(settings, out failure);
        }

        private static bool VerifyReference(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            GuardResonanceContactResult result = tracker.RegisterContact(0f, settings);
            return Expect(result.Outcome == GuardResonanceContactOutcome.Reference && result.Progress == 0,
                "First contact incorrectly produced Resonance.", out failure);
        }

        private static bool VerifyFirstResonance(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            GuardResonanceContactResult result = tracker.RegisterContact(0.8f, settings);
            return Expect(result.Outcome == GuardResonanceContactOutcome.Advanced && result.Progress == 1,
                "Second ideal contact did not produce Resonance 1.", out failure);
        }

        private static bool VerifyFullBreak(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            tracker.RegisterContact(0.8f, settings);
            tracker.RegisterContact(1.6f, settings);
            tracker.RegisterContact(2.4f, settings);
            GuardResonanceContactResult result = tracker.RegisterContact(3.2f, settings);
            return Expect(result.Broke && result.Progress == 4,
                "Five ideal contacts did not break at Resonance 4.", out failure);
        }

        private static bool VerifyBadTimingAndRecovery(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            tracker.RegisterContact(0.8f, settings);
            tracker.RegisterContact(1.6f, settings);
            GuardResonanceContactResult miss = tracker.RegisterContact(2.0f, settings);
            GuardResonanceContactResult recovered = tracker.RegisterContact(2.8f, settings);
            return Expect(miss.Outcome == GuardResonanceContactOutcome.Reduced
                && miss.Progress == 1
                && recovered.Outcome == GuardResonanceContactOutcome.Advanced
                && recovered.Progress == 2,
                "Bad timing did not reduce progress or recover from the newest reference.", out failure);
        }

        private static bool VerifyFastSlowAndEdges(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            GuardResonanceContactResult tooFast = tracker.RegisterContact(0.67f, settings);
            GuardResonanceTracker slowTracker = new GuardResonanceTracker();
            slowTracker.RegisterContact(0f, settings);
            GuardResonanceContactResult tooSlow = slowTracker.RegisterContact(0.93f, settings);
            GuardResonanceTracker edgeTracker = new GuardResonanceTracker();
            edgeTracker.RegisterContact(0f, settings);
            GuardResonanceContactResult inside = edgeTracker.RegisterContact(0.92f, settings);
            GuardResonanceContactResult outside = edgeTracker.RegisterContact(1.05f, settings);
            return Expect(tooFast.Outcome == GuardResonanceContactOutcome.Reduced
                && tooSlow.Outcome == GuardResonanceContactOutcome.Reduced
                && inside.Outcome == GuardResonanceContactOutcome.Advanced
                && outside.Outcome == GuardResonanceContactOutcome.Reduced,
                "Fast, slow, or tolerance-edge cadence classification was incorrect.", out failure);
        }

        private static bool VerifyIndependentTrackers(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker first = new GuardResonanceTracker();
            GuardResonanceTracker second = new GuardResonanceTracker();
            first.RegisterContact(0f, settings);
            second.RegisterContact(0.4f, settings);
            GuardResonanceContactResult firstResult = first.RegisterContact(0.8f, settings);
            GuardResonanceContactResult secondResult = second.RegisterContact(1.2f, settings);
            return Expect(firstResult.Progress == 1 && secondResult.Progress == 1,
                "Independent attackers were combined into one cadence.", out failure);
        }

        private static bool VerifyStaleReference(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            tracker.RegisterContact(0.8f, settings);
            GuardResonanceContactResult stale = tracker.RegisterContact(3f, settings);
            GuardResonanceContactResult resumed = tracker.RegisterContact(3.8f, settings);
            return Expect(stale.Outcome == GuardResonanceContactOutcome.StaleReference
                && stale.Progress == 0
                && resumed.Outcome == GuardResonanceContactOutcome.Advanced
                && resumed.Progress == 1,
                "Stale cadence did not reset to a fresh reference for immediate recovery.", out failure);
        }

        private static bool VerifyResetAfterBreak(GuardResonanceSettings settings, out string failure)
        {
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            tracker.RegisterContact(0.8f, settings);
            tracker.RegisterContact(1.6f, settings);
            tracker.RegisterContact(2.4f, settings);
            tracker.RegisterContact(3.2f, settings);
            tracker.Reset();
            GuardResonanceContactResult result = tracker.RegisterContact(4f, settings);
            return Expect(result.Outcome == GuardResonanceContactOutcome.Reference && result.Progress == 0,
                "Post-break cadence did not restart at a fresh reference.", out failure);
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }
    }
}
