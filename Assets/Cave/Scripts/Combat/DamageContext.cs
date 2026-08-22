using System;
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
        GuardBreak = 1 << 5
    }

    public interface IDeflectableDamageSource
    {
        bool TryDeflect(GameObject defender);
    }

    public readonly struct DamageContext
    {
        private readonly PlayerResourceMastery masterySource;
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
            explicitSource = source != null ? source.gameObject : null;
            staminaMasteryEligible = qualifiesForStaminaMastery;
            manaMasteryEligible = qualifiesForManaMastery;
            damageTraits = DamageTrait.Direct;
        }

        public DamageContext(GameObject source, DamageTrait traits)
        {
            masterySource = null;
            explicitSource = source;
            staminaMasteryEligible = false;
            manaMasteryEligible = false;
            damageTraits = traits;
        }

        private DamageContext(
            PlayerResourceMastery source,
            GameObject sourceObject,
            bool qualifiesForStaminaMastery,
            bool qualifiesForManaMastery,
            DamageTrait traits)
        {
            masterySource = source;
            explicitSource = sourceObject;
            staminaMasteryEligible = qualifiesForStaminaMastery;
            manaMasteryEligible = qualifiesForManaMastery;
            damageTraits = traits;
        }

        public bool IsPlayerDamage => masterySource != null;
        public GameObject Source => explicitSource;
        public DamageTrait Traits => damageTraits == 0 ? DamageTrait.Direct : damageTraits;

        public bool HasTrait(DamageTrait trait)
        {
            return (Traits & trait) != 0;
        }

        public DamageContext WithTraits(DamageTrait traits)
        {
            return new DamageContext(
                masterySource,
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
            }
        }
    }
}
