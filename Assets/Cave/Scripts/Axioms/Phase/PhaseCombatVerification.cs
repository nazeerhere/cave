using System;
using Cave.Axioms.Frequency;

namespace Cave.Axioms.Phase
{
    /// <summary>
    /// Runtime-independent deterministic coverage for Phase opening, stack, and
    /// collapse rules. It deliberately models combat resolution by requiring a
    /// positive applied-damage value before an armed hit or collapse can resolve.
    /// </summary>
    public static class PhaseCombatVerification
    {
        public static bool TryRunAll(out string failure)
        {
            return VerifyNoOpening(out failure)
                && VerifyOpeningAndWrongTarget(out failure)
                && VerifyBlockedAndQualifyingHit(out failure)
                && VerifyExpiryAndReplacement(out failure)
                && VerifyTierAndParity(out failure)
                && VerifyBlockedChargeAndRefresh(out failure)
                && VerifyResonanceToPhaseBridge(out failure);
        }

        private static bool VerifyNoOpening(out string failure)
        {
            Model model = new Model();
            object target = new object();
            return Expect(!model.TryQualifyingHit(target, 1, true, 0f) && model.Stacks == 0,
                "Ordinary hit applied Phase without an opening.", out failure);
        }

        private static bool VerifyOpeningAndWrongTarget(out string failure)
        {
            Model model = new Model();
            object intended = new object();
            model.Arm(intended, 0f);
            bool wrongConsumed = model.TryQualifyingHit(new object(), 1, true, 0.1f);
            return Expect(model.HasOpening(0.1f)
                && !wrongConsumed
                && model.OpeningTarget == intended
                && model.Stacks == 0,
                "A target-bound Phase opening was consumed by the wrong target.", out failure);
        }

        private static bool VerifyBlockedAndQualifyingHit(out string failure)
        {
            Model model = new Model();
            object target = new object();
            model.Arm(target, 0f);
            bool blockedConsumed = model.TryQualifyingHit(target, 0, true, 0.1f);
            bool preservedAfterBlockedHit = model.HasOpening(0.1f);
            bool validConsumed = model.TryQualifyingHit(target, 1, true, 0.2f);
            return Expect(!blockedConsumed
                && preservedAfterBlockedHit
                && validConsumed
                && !model.HasOpening(0.2f)
                && model.Stacks == 1,
                "Blocked or qualifying armed-hit behavior was incorrect.", out failure);
        }

        private static bool VerifyExpiryAndReplacement(out string failure)
        {
            Model model = new Model();
            object first = new object();
            object replacement = new object();
            model.Arm(first, 0f);
            model.Arm(replacement, 1f);
            bool replaced = model.HasOpening(1.1f) && model.OpeningTarget == replacement;
            bool expired = !model.TryQualifyingHit(replacement, 1, true, 4.1f)
                && model.Stacks == 0;
            return Expect(replaced && expired,
                "Phase opening did not replace cleanly or expire without application.", out failure);
        }

        private static bool VerifyTierAndParity(out string failure)
        {
            Model tierOne = new Model();
            tierOne.AddStacks(1);
            bool tierOneCollapsed = tierOne.TryCollapseOnSuccessfulHit(1, 9, 0f);

            Model even = new Model();
            even.AddStacks(2);
            bool evenCollapsed = even.TryCollapseOnSuccessfulHit(2, 9, 0f);

            Model one = new Model();
            one.AddStacks(1);
            one.TryCollapseOnSuccessfulHit(2, 9, 0f);
            Model three = new Model();
            three.AddStacks(3);
            three.TryCollapseOnSuccessfulHit(2, 9, 0f);
            Model five = new Model();
            five.AddStacks(5);
            five.TryCollapseOnSuccessfulHit(3, 18, 0f);
            Model seven = new Model();
            seven.AddStacks(7);
            seven.TryCollapseOnSuccessfulHit(2, 9, 0f);

            return Expect(!tierOneCollapsed && tierOne.Stacks == 1
                && evenCollapsed && even.Stacks == 0 && even.ActiveEffect == PhaseEffectClass.None
                && one.ActiveEffect == PhaseEffectClass.Strong
                && three.ActiveEffect == PhaseEffectClass.WeakLong
                && five.ActiveEffect == PhaseEffectClass.Strong && five.ActiveDuration > one.ActiveDuration
                && seven.ActiveEffect == PhaseEffectClass.WeakLong && seven.ActiveDuration > three.ActiveDuration,
                "Tier threshold or Phase parity result was incorrect.", out failure);
        }

        private static bool VerifyBlockedChargeAndRefresh(out string failure)
        {
            Model model = new Model();
            model.AddStacks(1);
            bool blockedCollapse = model.TryCollapseOnSuccessfulHit(2, 0, 0f);
            model.TryCollapseOnSuccessfulHit(2, 9, 0.1f);
            float strongMultiplier = model.ActiveMultiplier;
            float initialExpiry = model.ActiveExpiresAt;
            model.AddStacks(3);
            model.TryCollapseOnSuccessfulHit(2, 9, 0.2f);
            return Expect(!blockedCollapse
                && model.Stacks == 0
                && model.ActiveEffect == PhaseEffectClass.Strong
                && model.ActiveMultiplier == strongMultiplier
                && model.ActiveExpiresAt > initialExpiry,
                "Blocked charged hit or active Phase replacement policy was incorrect.", out failure);
        }

        private static bool VerifyResonanceToPhaseBridge(out string failure)
        {
            GuardResonanceSettings settings = new GuardResonanceSettings(0.8f, 0.15f, 2.5f, 4);
            GuardResonanceTracker tracker = new GuardResonanceTracker();
            tracker.RegisterContact(0f, settings);
            tracker.RegisterContact(0.8f, settings);
            tracker.RegisterContact(1.6f, settings);
            tracker.RegisterContact(2.4f, settings);
            GuardResonanceContactResult breakResult = tracker.RegisterContact(3.2f, settings);

            Model phase = new Model();
            object defender = new object();
            // Break only arms the opening; it cannot create a stack itself.
            if (breakResult.Broke)
            {
                phase.Arm(defender, 3.2f);
            }

            bool applied = phase.TryQualifyingHit(defender, 1, true, 3.3f);
            bool collapsed = phase.TryCollapseOnSuccessfulHit(2, 9, 3.4f);
            return Expect(breakResult.Broke
                && applied
                && collapsed
                && phase.ActiveEffect == PhaseEffectClass.Strong,
                "Resonance -> opening -> Phase -> Tier-2 collapse sequence was incorrect.", out failure);
        }

        private static bool Expect(bool condition, string message, out string failure)
        {
            failure = condition ? null : message;
            return condition;
        }

        private sealed class Model
        {
            private const float OpeningDuration = 3f;
            private const float StrongMultiplier = 1.35f;
            private const float WeakMultiplier = 1.18f;
            private const float StrongDuration = 2.5f;
            private const float WeakDuration = 4f;
            private const float DurationPerPair = 0.6f;

            private object openingTarget;
            private float openingExpiresAt;
            private int stacks;
            private PhaseEffectClass activeEffect;
            private float activeMultiplier = 1f;
            private float activeExpiresAt;
            private float activeDuration;

            public int Stacks => stacks;
            public object OpeningTarget => openingTarget;
            public PhaseEffectClass ActiveEffect => activeEffect;
            public float ActiveMultiplier => activeMultiplier;
            public float ActiveExpiresAt => activeExpiresAt;
            public float ActiveDuration => activeDuration;

            public void Arm(object target, float timestamp)
            {
                openingTarget = target;
                openingExpiresAt = timestamp + OpeningDuration;
            }

            public bool HasOpening(float timestamp)
            {
                if (openingTarget == null || timestamp >= openingExpiresAt)
                {
                    openingTarget = null;
                    openingExpiresAt = 0f;
                    return false;
                }

                return true;
            }

            public bool TryQualifyingHit(object target, int appliedDamage, bool meleeNonArea, float timestamp)
            {
                if (!HasOpening(timestamp)
                    || openingTarget != target
                    || appliedDamage <= 0
                    || !meleeNonArea)
                {
                    return false;
                }

                openingTarget = null;
                openingExpiresAt = 0f;
                stacks++;
                return true;
            }

            public void AddStacks(int amount)
            {
                stacks += Math.Max(0, amount);
            }

            public bool TryCollapseOnSuccessfulHit(int chargeTier, int appliedDamage, float timestamp)
            {
                if (chargeTier < 2 || appliedDamage <= 0 || stacks <= 0)
                {
                    return false;
                }

                PhaseExposureDefinition exposure = PhaseCombatRules.ResolveExposure(
                    stacks,
                    StrongMultiplier,
                    WeakMultiplier,
                    StrongDuration,
                    WeakDuration,
                    DurationPerPair);
                stacks = 0;
                if (!exposure.IsActive)
                {
                    return true;
                }

                if (activeEffect == PhaseEffectClass.None || exposure.DamageMultiplier >= activeMultiplier)
                {
                    activeEffect = exposure.EffectClass;
                    activeMultiplier = exposure.DamageMultiplier;
                }

                activeDuration = exposure.Duration;
                activeExpiresAt = Math.Max(activeExpiresAt, timestamp + exposure.Duration);
                return true;
            }
        }
    }
}
