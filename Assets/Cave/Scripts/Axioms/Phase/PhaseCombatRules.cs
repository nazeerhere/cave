namespace Cave.Axioms.Phase
{
    public enum PhaseOpeningSource
    {
        PerfectParry,
        ResonanceBreak
    }

    public enum PhaseEffectClass
    {
        None,
        Strong,
        WeakLong
    }

    public struct PhaseExposureDefinition
    {
        public PhaseExposureDefinition(PhaseEffectClass effectClass, float multiplier, float duration)
        {
            EffectClass = effectClass;
            DamageMultiplier = multiplier;
            Duration = duration;
        }

        public PhaseEffectClass EffectClass { get; }
        public float DamageMultiplier { get; }
        public float Duration { get; }
        public bool IsActive => EffectClass != PhaseEffectClass.None;
    }

    /// <summary>Centralized parity and duration rules for latent Phase collapse.</summary>
    public static class PhaseCombatRules
    {
        public static PhaseExposureDefinition ResolveExposure(
            int stackCount,
            float strongMultiplier,
            float weakMultiplier,
            float strongBaseDuration,
            float weakBaseDuration,
            float durationPerOddPair)
        {
            if (stackCount <= 0 || (stackCount & 1) == 0)
            {
                return new PhaseExposureDefinition(PhaseEffectClass.None, 1f, 0f);
            }

            int pairCount = (stackCount - 1) / 2;
            bool strong = stackCount % 4 == 1;
            return strong
                ? new PhaseExposureDefinition(
                    PhaseEffectClass.Strong,
                    strongMultiplier,
                    strongBaseDuration + pairCount * durationPerOddPair)
                : new PhaseExposureDefinition(
                    PhaseEffectClass.WeakLong,
                    weakMultiplier,
                    weakBaseDuration + pairCount * durationPerOddPair);
        }
    }
}
