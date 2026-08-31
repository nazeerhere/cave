using System;
using Cave.Enemies;
using Cave.Progression;
using UnityEngine;

namespace Cave.Combat
{
    [Flags]
    public enum DamageTrait
    {
        Direct = 1 << 0,
        Projectile = 1 << 1,
        AreaOfEffect = 1 << 2,
        Piercing = 1 << 3,
        Melee = 1 << 4,
        GuardBreak = 1 << 5,
        StaggerNormal = 1 << 6,
        StaggerHeavy = 1 << 7,
        FrenzyCritical = 1 << 8,
        /// <summary>Cannot be negated by ordinary Guard; parry remains a separate rule.</summary>
        Unblockable = 1 << 9
    }

    public interface IDeflectableDamageSource
    {
        bool TryDeflect(GameObject defender);
    }

    public readonly struct DamageContext
    {
        private readonly PlayerResourceMastery masterySource;
        private readonly PlayerPermanentProgression permanentProgressionSource;
        private readonly GameObject explicitSource;
        private readonly bool staminaMasteryEligible;
        private readonly bool manaMasteryEligible;
        private readonly DamageTrait damageTraits;

        public DamageContext(
            PlayerResourceMastery source,
            bool qualifiesForStaminaMastery,
            bool qualifiesForManaMastery)
        {
            masterySource = source;
            permanentProgressionSource = source != null
                ? source.GetComponent<PlayerPermanentProgression>()
                : null;
            explicitSource = source != null ? source.gameObject : null;
            staminaMasteryEligible = qualifiesForStaminaMastery;
            manaMasteryEligible = qualifiesForManaMastery;
            damageTraits = DamageTrait.Direct;
        }

        public DamageContext(GameObject source, DamageTrait traits)
        {
            masterySource = null;
            permanentProgressionSource = null;
            explicitSource = source;
            staminaMasteryEligible = false;
            manaMasteryEligible = false;
            damageTraits = ApplyEnemyFrenzyTrait(source, traits);
        }

        private static DamageTrait ApplyEnemyFrenzyTrait(GameObject source, DamageTrait traits)
        {
            if (source != null
                && source.GetComponentInParent<EnemyCorruptionLifecycle>()?.IsFrenzied == true)
            {
                traits |= DamageTrait.Unblockable;
            }

            return traits;
        }

        private DamageContext(
            PlayerResourceMastery source,
            PlayerPermanentProgression progressionSource,
            GameObject sourceObject,
            bool qualifiesForStaminaMastery,
            bool qualifiesForManaMastery,
            DamageTrait traits)
        {
            masterySource = source;
            permanentProgressionSource = progressionSource;
            explicitSource = sourceObject;
            staminaMasteryEligible = qualifiesForStaminaMastery;
            manaMasteryEligible = qualifiesForManaMastery;
            damageTraits = traits;
        }

        public bool IsPlayerDamage => masterySource != null;
        public GameObject Source => explicitSource;
        public PlayerPermanentProgression PermanentProgressionSource => permanentProgressionSource;
        public DamageTrait Traits => damageTraits == 0 ? DamageTrait.Direct : damageTraits;

        public bool HasTrait(DamageTrait trait)
        {
            return (Traits & trait) != 0;
        }

        public DamageContext WithTraits(DamageTrait traits)
        {
            return new DamageContext(
                masterySource,
                permanentProgressionSource,
                explicitSource,
                staminaMasteryEligible,
                manaMasteryEligible,
                Traits | traits);
        }

        internal void ReportKillingBlow()
        {
            if (masterySource != null)
            {
                masterySource.ProcessKillingBlow(staminaMasteryEligible, manaMasteryEligible);
                masterySource.GetComponent<Cave.Player.PlayerCurseController>()
                    ?.NotifyPlayerKillingBlow();
            }
        }
    }
}
