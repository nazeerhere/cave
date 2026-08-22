using System;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Progression
{
    [DisallowMultipleComponent]
    public sealed class PlayerResourceMastery : MonoBehaviour
    {
        [SerializeField] private ProgressionDifficultySettings settings;

        [Header("Session Baselines (Read Only)")]
        [SerializeField] private int baseMaximumHealth;
        [SerializeField] private float baseMaximumStamina;
        [SerializeField] private float baseMaximumMana;

        private PlayerHealth playerHealth;
        private SpinSwordAttack spinSwordAttack;
        private PlayerMana playerMana;

        public event Action MasteryChanged;

        public int BaseMaximumHealth => baseMaximumHealth;
        public float BaseMaximumStamina => baseMaximumStamina;
        public float BaseMaximumMana => baseMaximumMana;
        public float HealthGrowthPercent => CalculateGrowth(playerHealth.MaxHealth, baseMaximumHealth);
        public float StaminaGrowthPercent => CalculateGrowth(spinSwordAttack.MaximumStamina, baseMaximumStamina);
        public float ManaGrowthPercent => CalculateGrowth(playerMana.MaximumMana, baseMaximumMana);

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            spinSwordAttack = GetComponent<SpinSwordAttack>();
            playerMana = GetComponent<PlayerMana>();

            if (playerHealth == null || spinSwordAttack == null || playerMana == null)
            {
                enabled = false;
                Debug.LogError("PlayerResourceMastery requires PlayerHealth, SpinSwordAttack, and PlayerMana.", this);
                return;
            }

            baseMaximumHealth = playerHealth.MaxHealth;
            baseMaximumStamina = spinSwordAttack.MaximumStamina;
            baseMaximumMana = playerMana.MaximumMana;
        }

        internal void Configure(ProgressionDifficultySettings progressionSettings)
        {
            settings = progressionSettings;
        }

        public DamageContext CreatePlayerDamageContext()
        {
            return new DamageContext(this, false, false);
        }

        public DamageContext CreateSpinDamageContext(bool manaWasConsumed)
        {
            bool manaEligible = manaWasConsumed && IsManaCritical();
            bool staminaEligible = !manaWasConsumed && IsStaminaCritical();
            return new DamageContext(this, staminaEligible, manaEligible);
        }

        public DamageContext CreateManaDamageContext()
        {
            return new DamageContext(this, false, IsManaCritical());
        }

        internal void ProcessKillingBlow(bool staminaMasteryEligible, bool manaMasteryEligible)
        {
            if (settings == null)
            {
                return;
            }

            bool changed = false;
            bool addToCurrent = settings.AddIncreaseToCurrentResource;

            if (IsHealthCritical())
            {
                changed |= playerHealth.IncreaseMaxHealth(settings.HealthIncreasePerMastery, addToCurrent);
            }

            if (manaMasteryEligible)
            {
                changed |= playerMana.IncreaseMaximumMana(settings.ManaIncreasePerMastery, addToCurrent);
            }
            else if (staminaMasteryEligible)
            {
                changed |= spinSwordAttack.IncreaseMaximumStamina(
                    settings.StaminaIncreasePerMastery,
                    addToCurrent);
            }

            if (changed)
            {
                MasteryChanged?.Invoke();
            }
        }

        private bool IsHealthCritical()
        {
            return playerHealth.MaxHealth > 0
                && playerHealth.CurrentHealth <= playerHealth.MaxHealth * settings.CriticalHealthThreshold;
        }

        private bool IsStaminaCritical()
        {
            return spinSwordAttack.MaximumStamina > 0f
                && spinSwordAttack.CurrentStamina
                    <= spinSwordAttack.MaximumStamina * settings.CriticalStaminaThreshold;
        }

        private bool IsManaCritical()
        {
            return playerMana.MaximumMana > 0f
                && playerMana.CurrentMana <= playerMana.MaximumMana * settings.CriticalManaThreshold;
        }

        private static float CalculateGrowth(float currentMaximum, float baseMaximum)
        {
            return baseMaximum > 0f ? Mathf.Max(0f, (currentMaximum - baseMaximum) / baseMaximum) : 0f;
        }
    }
}
