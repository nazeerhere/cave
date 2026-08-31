using System;
using Cave.Combat;
using Cave.Player;
using UnityEngine;

namespace Cave.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerMana), typeof(PlayerController))]
    public sealed class PlayerPermanentProgression : MonoBehaviour
    {
        [Header("General Shards")]
        [SerializeField, Min(0)] private int generalShards;

        [Header("Repeatable Regeneration Upgrade Costs")]
        [SerializeField, Min(1)] private int healthRegenerationCost = 10;
        [SerializeField, Min(0)] private int healthPriceIncreasePerLevel = 5;
        [SerializeField, Min(1)] private int manaRegenerationCost = 10;
        [SerializeField, Min(0)] private int manaPriceIncreasePerLevel = 5;

        [Header("One-Time Upgrade Costs")]
        [SerializeField, Min(1)] private int speedBurstCost = 12;

        [Header("Health / Mana Regeneration")]
        [SerializeField, Min(0.1f)] private float regenerationTickInterval = 3f;
        [SerializeField, Range(0.001f, 1f)] private float healthPercentPerLevelPerTick = 0.01f;
        [SerializeField, Range(0.001f, 1f)] private float manaPercentPerLevelPerTick = 0.01f;

        [Header("Speed Burst")]
        [SerializeField, Min(1f)] private float speedBurstMultiplier = 1.2f;
        [SerializeField, Min(0.05f)] private float speedBurstDuration = 2.5f;

        [Header("Uncapped Power")]
        [SerializeField, Min(1)] private int powerCostPerPurchase = 1;
        [SerializeField, Min(0.001f)] private float damagePercentPerPurchase = 0.01f;

        [Header("Owned Upgrades (Runtime / Save Ready)")]
        [Tooltip("Legacy ownership flag retained to migrate existing configured players.")]
        [SerializeField] private bool healthRegenerationOwned;
        [Tooltip("Legacy ownership flag retained to migrate existing configured players.")]
        [SerializeField] private bool manaRegenerationOwned;
        [SerializeField] private bool speedBurstOwned;
        [SerializeField, Min(0)] private int healthRegenerationLevel;
        [SerializeField, Min(0)] private int manaRegenerationLevel;
        [SerializeField, Min(0)] private int powerPurchaseCount;
        [SerializeField, Min(1f)] private float cavesGlareBaseStrengthMultiplier = 1f;

        private PlayerHealth playerHealth;
        private PlayerMana playerMana;
        private PlayerController playerController;
        private SidewaysParryAttack defense;
        private float healthRegenAccumulator;
        private float nextRegenerationTickTime;
        private PlayerRecoveryModifiers recoveryModifiers;

        public event Action<int, int> GeneralShardsChanged;
        public event Action UpgradesChanged;

        public int GeneralShards => generalShards;
        public bool HealthRegenerationOwned => healthRegenerationLevel > 0;
        public bool ManaRegenerationOwned => manaRegenerationLevel > 0;
        public bool SpeedBurstOwned => speedBurstOwned;
        public int HealthRegenerationLevel => healthRegenerationLevel;
        public int ManaRegenerationLevel => manaRegenerationLevel;
        public int PowerPurchaseCount => powerPurchaseCount;
        public int HealthRegenerationCost => ResolveScaledCost(
            healthRegenerationCost,
            healthPriceIncreasePerLevel,
            healthRegenerationLevel);
        public int ManaRegenerationCost => ResolveScaledCost(
            manaRegenerationCost,
            manaPriceIncreasePerLevel,
            manaRegenerationLevel);
        public int HealthRegenerationBaseCost => healthRegenerationCost;
        public int ManaRegenerationBaseCost => manaRegenerationCost;
        public int HealthPriceIncreasePerLevel => healthPriceIncreasePerLevel;
        public int ManaPriceIncreasePerLevel => manaPriceIncreasePerLevel;
        public float RegenerationTickInterval => regenerationTickInterval;
        public float HealthRegenerationPercentPerTick =>
            healthRegenerationLevel * healthPercentPerLevelPerTick;
        public float ManaRegenerationPercentPerTick =>
            manaRegenerationLevel * manaPercentPerLevelPerTick;
        public int SpeedBurstCost => speedBurstCost;
        public int PowerCostPerPurchase => powerCostPerPurchase;
        public float PermanentDamagePercent => powerPurchaseCount * damagePercentPerPurchase;
        public float PermanentDamageMultiplier => 1f + PermanentDamagePercent;
        public float CavesGlareBaseStrengthMultiplier => cavesGlareBaseStrengthMultiplier;
        public float AuthoritativeDamageMultiplier =>
            PermanentDamageMultiplier * cavesGlareBaseStrengthMultiplier;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerMana = GetComponent<PlayerMana>();
            playerController = GetComponent<PlayerController>();
            defense = GetComponent<SidewaysParryAttack>();
            recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            MigrateLegacyUpgradeFlags();
            ScheduleNextRegenerationTick();
        }

        private void OnEnable()
        {
            if (defense != null)
            {
                defense.DefenseSucceeded += HandleDefenseSucceeded;
            }

            if (playerHealth != null)
            {
                playerHealth.Respawned += HandleRespawned;
            }

            ScheduleNextRegenerationTick();
        }

        private void OnDisable()
        {
            if (defense != null)
            {
                defense.DefenseSucceeded -= HandleDefenseSucceeded;
            }

            if (playerHealth != null)
            {
                playerHealth.Respawned -= HandleRespawned;
            }
        }

        private void Update()
        {
            if (recoveryModifiers == null)
            {
                recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            }

            if (Time.time < nextRegenerationTickTime)
            {
                return;
            }

            float interval = Mathf.Max(0.1f, regenerationTickInterval);
            int elapsedTicks = Mathf.Max(
                1,
                Mathf.FloorToInt((Time.time - nextRegenerationTickTime) / interval) + 1);
            nextRegenerationTickTime += elapsedTicks * interval;
            for (int tick = 0; tick < elapsedTicks; tick++)
            {
                ApplyRegenerationTick();
            }
        }

        private void ApplyRegenerationTick()
        {
            float healthRecoveryMultiplier = recoveryModifiers != null
                ? recoveryModifiers.HealthRegenerationMultiplier
                : 1f;
            float manaRecoveryMultiplier = recoveryModifiers != null
                ? recoveryModifiers.ManaRegenerationMultiplier
                : 1f;

            if (healthRegenerationLevel > 0
                && playerHealth.CurrentHealth < playerHealth.MaxHealth)
            {
                healthRegenAccumulator += playerHealth.MaxHealth
                    * HealthRegenerationPercentPerTick
                    * healthRecoveryMultiplier;
                int availableWholeHealth = Mathf.FloorToInt(healthRegenAccumulator);
                int restoredHealth = Mathf.Min(
                    availableWholeHealth,
                    playerHealth.MaxHealth - playerHealth.CurrentHealth);
                if (restoredHealth > 0 && playerHealth.RestoreHealth(restoredHealth))
                {
                    healthRegenAccumulator -= restoredHealth;
                }
            }
            else if (playerHealth.CurrentHealth >= playerHealth.MaxHealth
                || healthRegenerationLevel <= 0)
            {
                healthRegenAccumulator = 0f;
            }

            if (manaRegenerationLevel > 0 && playerMana.CurrentMana < playerMana.MaximumMana)
            {
                playerMana.RestoreMana(
                    playerMana.MaximumMana
                    * ManaRegenerationPercentPerTick
                    * manaRecoveryMultiplier);
            }
        }

        public void AddGeneralShards(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int previous = generalShards;
            long resolved = (long)generalShards + amount;
            generalShards = (int)Math.Min(int.MaxValue, resolved);
            GeneralShardsChanged?.Invoke(generalShards, generalShards - previous);
        }

        public bool TryPurchaseHealthRegeneration()
        {
            return TryPurchaseRepeatableRegeneration(
                ref healthRegenerationLevel,
                ref healthRegenerationOwned,
                HealthRegenerationCost);
        }

        public bool TryPurchaseManaRegeneration()
        {
            return TryPurchaseRepeatableRegeneration(
                ref manaRegenerationLevel,
                ref manaRegenerationOwned,
                ManaRegenerationCost);
        }

        public bool TryPurchaseSpeedBurst()
        {
            return TryPurchaseOneTime(ref speedBurstOwned, speedBurstCost);
        }

        public bool TryPurchasePower()
        {
            if (!TrySpendShards(powerCostPerPurchase))
            {
                return false;
            }

            powerPurchaseCount++;
            UpgradesChanged?.Invoke();
            return true;
        }

        public int ResolvePlayerDamage(int baseDamage)
        {
            return Mathf.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Max(0, baseDamage)
                    * AuthoritativeDamageMultiplier
                    * (GetComponent<PlayerCurseController>()?.OutgoingDamageMultiplier ?? 1f)));
        }

        public void AdaptBaseStrengthToward(
            float killerBenchmark,
            float adaptationFraction,
            float minimumStep,
            float postMatchGain)
        {
            float target = Mathf.Max(1f, killerBenchmark);
            if (cavesGlareBaseStrengthMultiplier < target - 0.001f)
            {
                float gap = target - cavesGlareBaseStrengthMultiplier;
                cavesGlareBaseStrengthMultiplier = Mathf.Min(
                    target,
                    cavesGlareBaseStrengthMultiplier
                        + Mathf.Max(Mathf.Max(0.001f, minimumStep), gap * Mathf.Clamp01(adaptationFraction)));
            }
            else
            {
                cavesGlareBaseStrengthMultiplier *= 1f + Mathf.Max(0f, postMatchGain);
            }

            UpgradesChanged?.Invoke();
        }

        public void ConsumeForCavesGlare(
            int shards,
            int powerPurchases,
            int healthRegenLevels,
            int manaRegenLevels)
        {
            int removedShards = Mathf.Min(generalShards, Mathf.Max(0, shards));
            if (removedShards > 0)
            {
                generalShards -= removedShards;
                GeneralShardsChanged?.Invoke(generalShards, -removedShards);
            }

            powerPurchaseCount = Mathf.Max(0, powerPurchaseCount - Mathf.Max(0, powerPurchases));
            healthRegenerationLevel = Mathf.Max(
                0,
                healthRegenerationLevel - Mathf.Max(0, healthRegenLevels));
            manaRegenerationLevel = Mathf.Max(
                0,
                manaRegenerationLevel - Mathf.Max(0, manaRegenLevels));
            healthRegenerationOwned = healthRegenerationLevel > 0;
            manaRegenerationOwned = manaRegenerationLevel > 0;
            UpgradesChanged?.Invoke();
        }

        public void NotifyDefensiveCounterUsed(PlayerDefenseQuality quality)
        {
            if (speedBurstOwned && quality != PlayerDefenseQuality.None)
            {
                playerController.ApplyTemporarySpeedMultiplier(
                    speedBurstMultiplier,
                    speedBurstDuration);
            }
        }

        private bool TryPurchaseOneTime(ref bool owned, int cost)
        {
            if (owned || !TrySpendShards(cost))
            {
                return false;
            }

            owned = true;
            UpgradesChanged?.Invoke();
            return true;
        }

        private bool TryPurchaseRepeatableRegeneration(
            ref int level,
            ref bool legacyOwned,
            int cost)
        {
            if (level == int.MaxValue || !TrySpendShards(cost))
            {
                return false;
            }

            level++;
            legacyOwned = true;
            UpgradesChanged?.Invoke();
            return true;
        }

        private bool TrySpendShards(int amount)
        {
            if (amount < 0 || generalShards < amount)
            {
                return false;
            }

            generalShards -= amount;
            GeneralShardsChanged?.Invoke(generalShards, -amount);
            return true;
        }

        private static int ResolveScaledCost(int baseCost, int increasePerLevel, int level)
        {
            long resolved = Math.Max(1, baseCost)
                + (long)Math.Max(0, increasePerLevel) * Math.Max(0, level);
            return (int)Math.Min(int.MaxValue, resolved);
        }

        private void MigrateLegacyUpgradeFlags()
        {
            if (healthRegenerationOwned && healthRegenerationLevel == 0)
            {
                healthRegenerationLevel = 1;
            }

            if (manaRegenerationOwned && manaRegenerationLevel == 0)
            {
                manaRegenerationLevel = 1;
            }

            healthRegenerationOwned = healthRegenerationLevel > 0;
            manaRegenerationOwned = manaRegenerationLevel > 0;
        }

        private void ScheduleNextRegenerationTick()
        {
            nextRegenerationTickTime = Time.time + Mathf.Max(0.1f, regenerationTickInterval);
        }

        private void HandleRespawned()
        {
            healthRegenAccumulator = 0f;
            ScheduleNextRegenerationTick();
        }

        private void HandleDefenseSucceeded(PlayerDefenseQuality quality)
        {
            if (quality == PlayerDefenseQuality.PerfectParry && speedBurstOwned)
            {
                playerController.ApplyTemporarySpeedMultiplier(
                    speedBurstMultiplier,
                    speedBurstDuration);
            }
        }

        private void OnValidate()
        {
            healthRegenerationCost = Mathf.Max(1, healthRegenerationCost);
            manaRegenerationCost = Mathf.Max(1, manaRegenerationCost);
            healthPriceIncreasePerLevel = Mathf.Max(0, healthPriceIncreasePerLevel);
            manaPriceIncreasePerLevel = Mathf.Max(0, manaPriceIncreasePerLevel);
            regenerationTickInterval = Mathf.Max(0.1f, regenerationTickInterval);
            healthRegenerationLevel = Mathf.Max(0, healthRegenerationLevel);
            manaRegenerationLevel = Mathf.Max(0, manaRegenerationLevel);
            MigrateLegacyUpgradeFlags();
            cavesGlareBaseStrengthMultiplier = Mathf.Max(1f, cavesGlareBaseStrengthMultiplier);
        }
    }
}
