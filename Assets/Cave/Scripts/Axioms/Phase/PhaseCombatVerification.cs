using System;
using Cave.Axioms.Frequency;

namespace Cave.Axioms.Phase
{
    /// <summary>Runtime-independent coverage for player-owned Imaginary and latent Phase collapse.</summary>
    public static class PhaseCombatVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyTimedMode(out failure)
                && VerifyMasteryDuration(out failure)
                && VerifyFirstHeavyOrdering(out failure)
                && VerifyResonanceActivation(out failure)
                && VerifyTierAndParity(out failure)
                && VerifyExposureTiming(out failure);
        }

        private static bool VerifyTimedMode(out string failure)
        {
            ImaginaryModel model = new ImaginaryModel();
            model.Activate(0f, 0f, 4);
            return Expect(model.IsActive(4.19f)
                && !model.OrdinaryHitConsumesMode(1f)
                && model.IsActive(1f)
                && !model.IsActive(4.2f),
                "Imaginary did not remain player-owned for its full 4.2-second duration.", out failure);
        }

        private static bool VerifyMasteryDuration(out string failure)
        {
            ImaginaryModel model = new ImaginaryModel();
            model.Activate(0f, 0f, 0);
            float baseDuration = model.Duration;
            model.Activate(10f, 1f, 0);
            return Expect(baseDuration == 4.2f && model.Duration > baseDuration,
                "Phase mastery did not increase Imaginary duration above its baseline.", out failure);
        }

        private static bool VerifyFirstHeavyOrdering(out string failure)
        {
            ImaginaryModel model = new ImaginaryModel();
            model.Activate(0f, 0f, 8);
            bool activatingHeavyConsumed = model.TryConsumeHeavyToken(8, 0.1f);
            bool firstFollowingHeavyConsumed = model.TryConsumeHeavyToken(9, 0.2f);
            bool secondFollowingHeavyConsumed = model.TryConsumeHeavyToken(10, 0.3f);
            model.Activate(1f, 0f, 0);
            return Expect(!activatingHeavyConsumed
                && firstFollowingHeavyConsumed
                && !secondFollowingHeavyConsumed
                && model.ResistanceBreakAvailable,
                "Imaginary heavy-token ordering or reset was incorrect.", out failure);
        }

        private static bool VerifyResonanceActivation(out string failure)
        {
            GuardResonanceSettings settings = new GuardResonanceSettings(0.8f, 0.15f, 2.5f, 4);
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            tracker.RegisterContact(0.8f, settings);
            tracker.RegisterContact(1.6f, settings);
            tracker.RegisterContact(2.4f, settings);
            GuardResonanceContactResult breakResult = tracker.RegisterContact(3.2f, settings);
            ImaginaryModel model = new ImaginaryModel();
            if (breakResult.Broke) model.Activate(3.2f, 0f, 0);
            return Expect(breakResult.Broke && model.IsActive(3.3f) && model.ResistanceBreakAvailable,
                "Resonance break did not establish a player-owned Imaginary activation.", out failure);
        }

        private static bool VerifyTierAndParity(out string failure)
        {
            ExposureModel tierOne = new ExposureModel();
            tierOne.AddStacks(1);
            bool tierOneCollapsed = tierOne.TryCollapseOnSuccessfulHit(1, 9);
            ExposureModel even = new ExposureModel();
            even.AddStacks(2);
            bool evenCollapsed = even.TryCollapseOnSuccessfulHit(2, 9);
            ExposureModel odd = new ExposureModel();
            odd.AddStacks(3);
            odd.TryCollapseOnSuccessfulHit(2, 9);
            return Expect(!tierOneCollapsed && tierOne.Stacks == 1
                && evenCollapsed && even.ActiveEffect == PhaseEffectClass.None
                && odd.ActiveEffect == PhaseEffectClass.WeakLong,
                "Latent Phase collapse parity regressed during Imaginary migration.", out failure);
        }

        private static bool VerifyExposureTiming(out string failure)
        {
            PhaseExposureDefinition one = PhaseCombatRules.ResolveExposure(1, 1.35f, 1.18f, 3.5f, 5.6f, .84f);
            PhaseExposureDefinition three = PhaseCombatRules.ResolveExposure(3, 1.35f, 1.18f, 3.5f, 5.6f, .84f);
            PhaseExposureDefinition five = PhaseCombatRules.ResolveExposure(5, 1.35f, 1.18f, 3.5f, 5.6f, .84f);
            return Expect(one.Duration == 3.5f && three.Duration == 6.44f && five.Duration == 5.18f,
                "Phase exposure timings no longer preserve the requested 1.4x values.", out failure);
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }

        private sealed class ImaginaryModel
        {
            private const float BaseDuration = 4.2f;
            private const float FullMasteryBonus = .8f;
            private float expiresAt;
            private bool resistanceBreakAvailable;
            private int activationAttackSequence;
            public float Duration { get; private set; }
            public bool ResistanceBreakAvailable => resistanceBreakAvailable;
            public void Activate(float timestamp, float phaseMastery, int attackSequence)
            {
                Duration = BaseDuration + Math.Max(0f, Math.Min(1f, phaseMastery)) * FullMasteryBonus;
                expiresAt = timestamp + Duration;
                resistanceBreakAvailable = true;
                activationAttackSequence = attackSequence;
            }
            public bool IsActive(float timestamp)
            {
                if (timestamp < expiresAt) return true;
                resistanceBreakAvailable = false;
                return false;
            }
            public bool OrdinaryHitConsumesMode(float timestamp) { return false; }
            public bool TryConsumeHeavyToken(int attackSequence, float timestamp)
            {
                if (!IsActive(timestamp) || !resistanceBreakAvailable
                    || (activationAttackSequence != 0 && attackSequence == activationAttackSequence)) return false;
                resistanceBreakAvailable = false;
                return true;
            }
        }

        private sealed class ExposureModel
        {
            private const float StrongMultiplier = 1.35f;
            private const float WeakMultiplier = 1.18f;
            private const float StrongDuration = 3.5f;
            private const float WeakDuration = 5.6f;
            private const float DurationPerPair = .84f;
            private int stacks;
            public int Stacks => stacks;
            public PhaseEffectClass ActiveEffect { get; private set; }
            public void AddStacks(int amount) { stacks += Math.Max(0, amount); }
            public bool TryCollapseOnSuccessfulHit(int chargeTier, int appliedDamage)
            {
                if (chargeTier < 2 || appliedDamage <= 0 || stacks <= 0) return false;
                PhaseExposureDefinition exposure = PhaseCombatRules.ResolveExposure(stacks, StrongMultiplier, WeakMultiplier, StrongDuration, WeakDuration, DurationPerPair);
                stacks = 0;
                ActiveEffect = exposure.EffectClass;
                return true;
            }
        }
    }
}
