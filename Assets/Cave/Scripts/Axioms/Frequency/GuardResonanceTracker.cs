using System;

namespace Cave.Axioms.Frequency
{
    public enum GuardResonanceContactOutcome
    {
        Ignored,
        Reference,
        Advanced,
        Reduced,
        StaleReference,
        Broken
    }

    public struct GuardResonanceSettings
    {
        public GuardResonanceSettings(
            float naturalPeriodSeconds,
            float toleranceFraction,
            float staleIntervalMultiplier,
            int breakProgress)
        {
            NaturalPeriodSeconds = Math.Max(0.01f, naturalPeriodSeconds);
            ToleranceFraction = Math.Max(0f, toleranceFraction);
            StaleIntervalMultiplier = Math.Max(1f, staleIntervalMultiplier);
            BreakProgress = Math.Max(1, breakProgress);
        }

        public float NaturalPeriodSeconds { get; }
        public float ToleranceFraction { get; }
        public float StaleIntervalMultiplier { get; }
        public int BreakProgress { get; }
        public float ToleranceSeconds => NaturalPeriodSeconds * ToleranceFraction;
        public float StaleIntervalSeconds => NaturalPeriodSeconds * StaleIntervalMultiplier;
    }

    public struct GuardResonanceContactResult
    {
        public GuardResonanceContactResult(
            GuardResonanceContactOutcome outcome,
            int progress,
            float intervalSeconds)
        {
            Outcome = outcome;
            Progress = progress;
            IntervalSeconds = intervalSeconds;
        }

        public GuardResonanceContactOutcome Outcome { get; }
        public int Progress { get; }
        public float IntervalSeconds { get; }
        public bool Broke => Outcome == GuardResonanceContactOutcome.Broken;
    }

    /// <summary>Pure cadence tracker for one attacker against one guarded defender.</summary>
    public sealed class GuardResonanceTracker
    {
        private bool hasReference;
        private float previousContactTime;
        private int progress;

        public int Progress => progress;
        public bool HasReference => hasReference;
        public float PreviousContactTime => previousContactTime;

        public GuardResonanceContactResult RegisterContact(float timestamp, GuardResonanceSettings settings)
        {
            if (!hasReference)
            {
                hasReference = true;
                previousContactTime = timestamp;
                return new GuardResonanceContactResult(
                    GuardResonanceContactOutcome.Reference,
                    progress,
                    0f);
            }

            float interval = timestamp - previousContactTime;
            previousContactTime = timestamp;
            if (interval < 0f || interval > settings.StaleIntervalSeconds)
            {
                progress = 0;
                return new GuardResonanceContactResult(
                    GuardResonanceContactOutcome.StaleReference,
                    progress,
                    interval);
            }

            float error = Abs(interval - settings.NaturalPeriodSeconds);
            if (error <= settings.ToleranceSeconds)
            {
                progress++;
                if (progress >= settings.BreakProgress)
                {
                    return new GuardResonanceContactResult(
                        GuardResonanceContactOutcome.Broken,
                        progress,
                        interval);
                }

                return new GuardResonanceContactResult(
                    GuardResonanceContactOutcome.Advanced,
                    progress,
                    interval);
            }

            progress = Math.Max(0, progress - 1);
            return new GuardResonanceContactResult(
                GuardResonanceContactOutcome.Reduced,
                progress,
                interval);
        }

        public void Reset()
        {
            hasReference = false;
            previousContactTime = 0f;
            progress = 0;
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }
}
