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

        [Header("Curse Mastery Scaling (Read Only)")]
        [SerializeField, Min(0f)] private float healthMasteryFractionCarry;
        [SerializeField, Min(1f)] private float currentMasteryGainMultiplier = 1f;

        private PlayerHealth playerHealth;
        private SpinSwordAttack spinSwordAttack;
        private PlayerMana playerMana;
        private PlayerCurseController curseController;

        public event Action MasteryChanged;

        public int BaseMaximumHealth => baseMaximumHealth;
        public float BaseMaximumStamina => baseMaximumStamina;
        public float BaseMaximumMana => baseMaximumMana;
        public float HealthGrowthPercent => CalculateGrowth(playerHealth.MaxHealth, baseMaximumHealth);
        public float StaminaGrowthPercent => CalculateGrowth(
            spinSwordAttack.MaximumStamina - StoneglassStaminaGrowth,
            baseMaximumStamina);
        public float ManaGrowthPercent => CalculateGrowth(
            playerMana.MaximumMana - StoneglassManaGrowth,
            baseMaximumMana);

        private float StoneglassStaminaGrowth => curseController != null
            ? curseController.StoneglassStaminaMaximumGrowth
            : 0f;
        private float StoneglassManaGrowth => curseController != null
            ? curseController.StoneglassManaMaximumGrowth
            : 0f;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            spinSwordAttack = GetComponent<SpinSwordAttack>();
            playerMana = GetComponent<PlayerMana>();
            curseController = GetComponent<PlayerCurseController>();

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
            if (curseController == null)
            {
                curseController = GetComponent<PlayerCurseController>();
            }

            currentMasteryGainMultiplier = curseController != null
                ? curseController.MasteryGainMultiplier
                : 1f;

            if (IsHealthCritical())
            {
                int healthIncrease = ResolveHealthMasteryIncrease(
                    settings.HealthIncreasePerMastery,
                    currentMasteryGainMultiplier);
                changed |= playerHealth.IncreaseMaxHealth(healthIncrease, addToCurrent);
            }

            if (manaMasteryEligible)
            {
                changed |= playerMana.IncreaseMaximumMana(
                    playerMana.MaximumMana
                        * settings.ManaIncreasePercentPerMastery
                        * currentMasteryGainMultiplier,
                    addToCurrent);
            }
            else if (staminaMasteryEligible)
            {
                changed |= spinSwordAttack.IncreaseMaximumStamina(
                    spinSwordAttack.MaximumStamina
                        * settings.StaminaIncreasePercentPerMastery
                        * currentMasteryGainMultiplier,
                    addToCurrent);
            }

            if (changed)
            {
                MasteryChanged?.Invoke();
            }
        }

        public bool ConsumeMasteryGrowth(
            int maximumHealthAmount,
            float maximumStaminaAmount,
            float maximumManaAmount)
        {
            bool changed = false;
            int availableHealth = Mathf.Max(0, playerHealth.MaxHealth - baseMaximumHealth);
            float availableStamina = Mathf.Max(
                0f,
                spinSwordAttack.MaximumStamina
                    - baseMaximumStamina
                    - StoneglassStaminaGrowth);
            float availableMana = Mathf.Max(
                0f,
                playerMana.MaximumMana
                    - baseMaximumMana
                    - StoneglassManaGrowth);

            changed |= playerHealth.ReduceMaxHealth(
                Mathf.Min(Mathf.Max(0, maximumHealthAmount), availableHealth),
                baseMaximumHealth);
            changed |= spinSwordAttack.ReduceMaximumStamina(
                Mathf.Min(Mathf.Max(0f, maximumStaminaAmount), availableStamina),
                baseMaximumStamina + StoneglassStaminaGrowth);
            changed |= playerMana.ReduceMaximumMana(
                Mathf.Min(Mathf.Max(0f, maximumManaAmount), availableMana),
                baseMaximumMana + StoneglassManaGrowth);

            if (changed)
            {
                MasteryChanged?.Invoke();
            }

            return changed;
        }

        private int ResolveHealthMasteryIncrease(int baseIncrease, float multiplier)
        {
            float scaled = Mathf.Max(1, baseIncrease) * Mathf.Max(1f, multiplier)
                + healthMasteryFractionCarry;
            int wholeIncrease = Mathf.Max(1, Mathf.FloorToInt(scaled));
            healthMasteryFractionCarry = Mathf.Max(0f, scaled - wholeIncrease);
            return wholeIncrease;
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
