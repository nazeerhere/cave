using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(PlayerSpecialMode), typeof(PlayerMana))]
    public sealed class PlayerDamageBoost : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float damageMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float spinHitManaCost = 3f;
        [SerializeField, Min(0f)] private float chargedHitManaCost = 5f;

        private PlayerSpecialMode specialMode;
        private PlayerMana playerMana;
        private PlayerSpecialModeUpgradeState upgradeState;

        private void Awake()
        {
            specialMode = GetComponent<PlayerSpecialMode>();
            playerMana = GetComponent<PlayerMana>();
            upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
        }

        public int ResolveSpinDamage(int baseDamage)
        {
            return ResolveSpinDamage(baseDamage, out _);
        }

        public int ResolveSpinDamage(int baseDamage, out bool manaWasConsumed)
        {
            return ResolvePaidDamage(baseDamage, spinHitManaCost, out manaWasConsumed);
        }

        public int ResolveChargedDamage(int baseDamage)
        {
            return ResolveChargedDamage(baseDamage, out _);
        }

        public int ResolveChargedDamage(int baseDamage, out bool manaWasConsumed)
        {
            return ResolvePaidDamage(baseDamage, chargedHitManaCost, out manaWasConsumed);
        }

        public int ResolveProjectileDamage(int baseDamage)
        {
            return IsDamageBoostSelected ? ApplyMultiplier(baseDamage) : baseDamage;
        }

        private int ResolvePaidDamage(int baseDamage, float manaCost, out bool manaWasConsumed)
        {
            manaWasConsumed = false;
            if (!IsDamageBoostSelected)
            {
                return baseDamage;
            }

            if (manaCost > 0f && !playerMana.TrySpendMana(manaCost))
            {
                return baseDamage;
            }

            manaWasConsumed = manaCost > 0f;
            return ApplyMultiplier(baseDamage);
        }

        private bool IsDamageBoostSelected => specialMode.CurrentMode == SpecialMode.DamageBoost;

        private int ApplyMultiplier(int baseDamage)
        {
            return Mathf.Max(
                baseDamage,
                Mathf.CeilToInt(baseDamage * GetCurrentDamageMultiplier()) + GetCurrentDamageBonus());
        }

        private int GetCurrentDamageBonus()
        {
            if (upgradeState == null)
            {
                upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            if (upgradeState == null || upgradeState.Settings == null)
            {
                return 0;
            }

            if (upgradeState.IsTier3Owned(SpecialMode.DamageBoost))
            {
                return upgradeState.Settings.StrengthTier3DamageBonus;
            }

            return upgradeState.IsTier2Owned(SpecialMode.DamageBoost)
                ? upgradeState.Settings.StrengthTier2DamageBonus
                : 0;
        }

        private float GetCurrentDamageMultiplier()
        {
            if (upgradeState == null)
            {
                upgradeState = GetComponent<PlayerSpecialModeUpgradeState>();
            }

            if (upgradeState == null || upgradeState.Settings == null)
            {
                return damageMultiplier;
            }

            if (upgradeState.IsTier3Owned(SpecialMode.DamageBoost))
            {
                return upgradeState.Settings.StrengthTier3DamageMultiplier;
            }

            return upgradeState.IsTier2Owned(SpecialMode.DamageBoost)
                ? upgradeState.Settings.StrengthTier2DamageMultiplier
                : damageMultiplier;
        }
    }
}
