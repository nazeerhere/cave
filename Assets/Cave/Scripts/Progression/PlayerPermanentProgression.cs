using System;
using Cave.Combat;
using Cave.Inventory;
using Cave.Missions;
using Cave.Player;
using Cave.Projectiles;
using UnityEngine;

namespace Cave.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerMana), typeof(PlayerController))]
    public sealed class PlayerPermanentProgression : MonoBehaviour
    {
        private const string HealthPotionEfficiencyKey = "Cave.Permanent.PotionEfficiency.Health.v1";
        private const string ManaPotionEfficiencyKey = "Cave.Permanent.PotionEfficiency.Mana.v1";
        private const string HealthPotionRegenerationKey = "Cave.Permanent.PotionRegeneration.Health.v1";
        private const string ManaPotionRegenerationKey = "Cave.Permanent.PotionRegeneration.Mana.v1";
        private const string DiskDamageKey = "Cave.Permanent.OblivionDisk.Damage.v1";
        private const string DiskAreaKey = "Cave.Permanent.OblivionDisk.Area.v1";
        private const string DiskPullStrengthKey = "Cave.Permanent.OblivionDisk.PullStrength.v1";
        private const string GeneralShardsKey = "Cave.Permanent.GeneralShards.v1";
        private const string CarapaceStructuralKey = "Cave.Permanent.Carapace.Structural.v1";
        private const string CarapaceRegenerationKey = "Cave.Permanent.Carapace.Regeneration.v1";
        private const string CarapaceLatticeKey = "Cave.Permanent.Carapace.Lattice.v1";
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

        [Header("Potion Efficiency")]
        [Tooltip("Each rank adds this much restoration to newly purchased potions.")]
        [SerializeField, Range(0.001f, 0.5f)] private float potionEfficiencyIncreasePerLevel = 0.05f;
        [SerializeField, Min(1)] private int healthPotionEfficiencyCost = 10;
        [SerializeField, Min(0)] private int healthPotionEfficiencyCostIncrease = 5;
        [SerializeField, Min(1)] private int manaPotionEfficiencyCost = 10;
        [SerializeField, Min(0)] private int manaPotionEfficiencyCostIncrease = 5;

        [Header("Potion Regeneration (General Shards)")]
        [SerializeField, Min(1)] private int healthPotionRegenerationCost = 10;
        [SerializeField, Min(0)] private int healthPotionRegenerationCostIncrease = 3;
        [SerializeField, Min(1)] private int manaPotionRegenerationCost = 10;
        [SerializeField, Min(0)] private int manaPotionRegenerationCostIncrease = 3;
        [SerializeField, Min(.1f)] private float potionRegenerationDuration = 3f;
        [SerializeField, Range(.001f, 1f)] private float healthPotionRegenerationPercentPerLevel = .03f;
        [SerializeField, Range(.001f, 1f)] private float manaPotionRegenerationPercentPerLevel = .03f;

        [Header("Oblivion Disk (General Shards)")]
        [SerializeField, Min(1)] private int diskDamageCost = 10;
        [SerializeField, Min(0)] private int diskDamageCostIncrease = 3;
        [SerializeField, Min(1)] private int diskAreaCost = 10;
        [SerializeField, Min(0)] private int diskAreaCostIncrease = 3;
        [SerializeField, Min(1)] private int diskPullStrengthCost = 10;
        [SerializeField, Min(0)] private int diskPullStrengthCostIncrease = 3;
        [SerializeField, Min(0)] private int diskDamagePerLevel = 1;
        [SerializeField, Range(0f, 1f)] private float diskAreaPercentPerLevel = .1f;
        [SerializeField, Range(0f, 1f)] private float diskPullStrengthPercentPerLevel = .15f;

        [Header("Owned Upgrades (Runtime / Save Ready)")]
        [Tooltip("Legacy ownership flag retained to migrate existing configured players.")]
        [SerializeField] private bool healthRegenerationOwned;
        [Tooltip("Legacy ownership flag retained to migrate existing configured players.")]
        [SerializeField] private bool manaRegenerationOwned;
        [SerializeField] private bool speedBurstOwned;
        [SerializeField, Min(0)] private int healthRegenerationLevel;
        [SerializeField, Min(0)] private int manaRegenerationLevel;
        [SerializeField, Min(0)] private int powerPurchaseCount;
        [SerializeField, Min(0)] private int healthPotionEfficiencyLevel;
        [SerializeField, Min(0)] private int manaPotionEfficiencyLevel;
        [SerializeField, Min(0)] private int healthPotionRegenerationLevel;
        [SerializeField, Min(0)] private int manaPotionRegenerationLevel;
        [SerializeField, Min(0)] private int diskDamageLevel;
        [SerializeField, Min(0)] private int diskAreaLevel;
        [SerializeField, Min(0)] private int diskPullStrengthLevel;
        [SerializeField, Min(1f)] private float cavesGlareBaseStrengthMultiplier = 1f;
        [Header("Resonant Carapace (General Shards)")]
        [SerializeField, Min(1)] private int structuralReinforcementCost = 12;
        [SerializeField, Min(0)] private int structuralReinforcementCostIncrease = 6;
        [SerializeField, Min(1)] private int livingRegenerationCost = 12;
        [SerializeField, Min(0)] private int livingRegenerationCostIncrease = 6;
        [SerializeField, Min(1)] private int anchoredLatticeCost = 16;
        [SerializeField, Min(0)] private int anchoredLatticeCostIncrease = 8;
        [SerializeField, Min(0)] private int structuralReinforcementDurabilityPerLevel = 20;
        [SerializeField, Range(0f, 1f)] private float livingRegenerationEfficiencyPerLevel = .2f;
        [SerializeField, Range(1, 5)] private int anchoredLatticeMaximumLevel = 5;
        [SerializeField, Min(0)] private int structuralReinforcementLevel;
        [SerializeField, Min(0)] private int livingRegenerationLevel;
        [SerializeField, Min(0)] private int anchoredLatticeLevel;

        private PlayerHealth playerHealth;
        private PlayerMana playerMana;
        private PlayerController playerController;
        private SidewaysParryAttack defense;
        private float healthRegenAccumulator;
        private float nextRegenerationTickTime;
        private PlayerRecoveryModifiers recoveryModifiers;
        private PlayerBrace brace;
        private float healthPotionRegenerationUntil;
        private float manaPotionRegenerationUntil;
        private float healthPotionRegenerationAccumulator;

        public event Action<int, int> GeneralShardsChanged;
        public event Action UpgradesChanged;

        public int GeneralShards => generalShards;
        public bool HealthRegenerationOwned => healthRegenerationLevel > 0;
        public bool ManaRegenerationOwned => manaRegenerationLevel > 0;
        public bool SpeedBurstOwned => speedBurstOwned;
        public int HealthRegenerationLevel => healthRegenerationLevel;
        public int ManaRegenerationLevel => manaRegenerationLevel;
        public int PowerPurchaseCount => powerPurchaseCount;
        public int HealthPotionEfficiencyLevel => healthPotionEfficiencyLevel;
        public int ManaPotionEfficiencyLevel => manaPotionEfficiencyLevel;
        public int HealthPotionRegenerationLevel => healthPotionRegenerationLevel;
        public int ManaPotionRegenerationLevel => manaPotionRegenerationLevel;
        public int DiskDamageLevel => diskDamageLevel;
        public int DiskAreaLevel => diskAreaLevel;
        public int DiskPullStrengthLevel => diskPullStrengthLevel;
        public int DiskCombinedUpgradeLevel => diskDamageLevel + diskAreaLevel + diskPullStrengthLevel;
        public float PotionEfficiencyIncreasePerLevel => potionEfficiencyIncreasePerLevel;
        public float PotionRegenerationDuration => potionRegenerationDuration;
        public float HealthPotionRegenerationPercent => healthPotionRegenerationLevel * healthPotionRegenerationPercentPerLevel;
        public float ManaPotionRegenerationPercent => manaPotionRegenerationLevel * manaPotionRegenerationPercentPerLevel;
        public int DiskDamageBonus => diskDamageLevel * diskDamagePerLevel;
        public float DiskAreaMultiplier => 1f + diskAreaLevel * diskAreaPercentPerLevel;
        public float DiskPullStrengthMultiplier => 1f + diskPullStrengthLevel * diskPullStrengthPercentPerLevel;
        public int HealthPotionEfficiencyCost => ResolveScaledCost(
            healthPotionEfficiencyCost,
            healthPotionEfficiencyCostIncrease,
            healthPotionEfficiencyLevel);
        public int ManaPotionEfficiencyCost => ResolveScaledCost(
            manaPotionEfficiencyCost,
            manaPotionEfficiencyCostIncrease,
            manaPotionEfficiencyLevel);
        public int HealthPotionRegenerationCost => ResolveScaledCost(
            healthPotionRegenerationCost,
            healthPotionRegenerationCostIncrease,
            healthPotionRegenerationLevel);
        public int ManaPotionRegenerationCost => ResolveScaledCost(
            manaPotionRegenerationCost,
            manaPotionRegenerationCostIncrease,
            manaPotionRegenerationLevel);
        public int DiskDamageCost => ResolveScaledCost(diskDamageCost, diskDamageCostIncrease, diskDamageLevel);
        public int DiskAreaCost => ResolveScaledCost(diskAreaCost, diskAreaCostIncrease, diskAreaLevel);
        public int DiskPullStrengthCost => ResolveScaledCost(
            diskPullStrengthCost, diskPullStrengthCostIncrease, diskPullStrengthLevel);
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
        public int StructuralReinforcementLevel => structuralReinforcementLevel;
        public int LivingRegenerationLevel => livingRegenerationLevel;
        public int AnchoredLatticeLevel => anchoredLatticeLevel;
        public int StructuralReinforcementDurabilityPerLevel => structuralReinforcementDurabilityPerLevel;
        public float LivingRegenerationEfficiencyPerLevel => livingRegenerationEfficiencyPerLevel;
        public int StructuralReinforcementCost => ResolveScaledCost(structuralReinforcementCost, structuralReinforcementCostIncrease, structuralReinforcementLevel);
        public int LivingRegenerationCost => ResolveScaledCost(livingRegenerationCost, livingRegenerationCostIncrease, livingRegenerationLevel);
        public int AnchoredLatticeCost => anchoredLatticeLevel >= anchoredLatticeMaximumLevel ? 0 : ResolveScaledCost(anchoredLatticeCost, anchoredLatticeCostIncrease, anchoredLatticeLevel);
        public float AuthoritativeDamageMultiplier =>
            PermanentDamageMultiplier * cavesGlareBaseStrengthMultiplier;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerMana = GetComponent<PlayerMana>();
            playerController = GetComponent<PlayerController>();
            defense = GetComponent<SidewaysParryAttack>();
            recoveryModifiers = GetComponent<PlayerRecoveryModifiers>();
            brace = GetComponent<PlayerBrace>();
            LoadGeneralShards();
            LoadPotionEfficiencyProgression();
            LoadNewProgression();
            structuralReinforcementLevel = PlayerPrefs.GetInt(CarapaceStructuralKey, structuralReinforcementLevel);
            livingRegenerationLevel = PlayerPrefs.GetInt(CarapaceRegenerationKey, livingRegenerationLevel);
            anchoredLatticeLevel = Mathf.Clamp(PlayerPrefs.GetInt(CarapaceLatticeKey, anchoredLatticeLevel), 0, anchoredLatticeMaximumLevel);
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

            ApplyPotionRegeneration(Time.deltaTime);

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

        private void ApplyPotionRegeneration(float deltaTime)
        {
            if (healthPotionRegenerationLevel > 0
                && Time.time < healthPotionRegenerationUntil
                && playerHealth != null
                && playerHealth.CurrentHealth < playerHealth.MaxHealth)
            {
                healthPotionRegenerationAccumulator += playerHealth.MaxHealth
                    * HealthPotionRegenerationPercent
                    / Mathf.Max(.1f, potionRegenerationDuration)
                    * Mathf.Max(0f, deltaTime);
                int restored = Mathf.Min(
                    Mathf.FloorToInt(healthPotionRegenerationAccumulator),
                    playerHealth.MaxHealth - playerHealth.CurrentHealth);
                if (restored > 0 && playerHealth.RestoreHealth(restored))
                {
                    healthPotionRegenerationAccumulator -= restored;
                }
            }
            else if (Time.time >= healthPotionRegenerationUntil
                || playerHealth == null
                || playerHealth.CurrentHealth >= playerHealth.MaxHealth)
            {
                healthPotionRegenerationAccumulator = 0f;
            }

            if (manaPotionRegenerationLevel > 0
                && Time.time < manaPotionRegenerationUntil
                && playerMana != null
                && playerMana.CurrentMana < playerMana.MaximumMana)
            {
                playerMana.RestoreMana(playerMana.MaximumMana
                    * ManaPotionRegenerationPercent
                    / Mathf.Max(.1f, potionRegenerationDuration)
                    * Mathf.Max(0f, deltaTime));
            }
        }

        private void ApplyRegenerationTick()
        {
            if (brace == null) brace = GetComponent<PlayerBrace>();
            // Purchased regeneration keeps its owned ranks and magnitude, but
            // applies only while the authoritative Deep Brace stage is active.
            if (brace == null || !brace.IsDeepBrace)
            {
                healthRegenAccumulator = 0f;
                return;
            }
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
            SaveGeneralShards();
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

        /// <summary>
        /// Potion potency is permanent progression, but it is only sampled by
        /// the shop when an individual potion is bought. Existing inventory is
        /// intentionally not rewritten by this purchase.
        /// </summary>
        public bool TryPurchaseHealthPotionEfficiency()
        {
            if (healthPotionEfficiencyLevel == int.MaxValue
                || !TrySpendShards(HealthPotionEfficiencyCost))
            {
                return false;
            }

            healthPotionEfficiencyLevel++;
            SavePotionEfficiencyProgression();
            UpgradesChanged?.Invoke();
            return true;
        }

        public bool TryPurchaseManaPotionEfficiency()
        {
            if (manaPotionEfficiencyLevel == int.MaxValue
                || !TrySpendShards(ManaPotionEfficiencyCost))
            {
                return false;
            }

            manaPotionEfficiencyLevel++;
            SavePotionEfficiencyProgression();
            UpgradesChanged?.Invoke();
            return true;
        }

        public bool TryPurchaseHealthPotionRegeneration()
        {
            return TryPurchasePersistentRank(ref healthPotionRegenerationLevel,
                HealthPotionRegenerationCost, SaveNewProgression);
        }

        public bool TryPurchaseManaPotionRegeneration()
        {
            return TryPurchasePersistentRank(ref manaPotionRegenerationLevel,
                ManaPotionRegenerationCost, SaveNewProgression);
        }

        public bool TryPurchaseDiskDamage()
        {
            return TryPurchasePersistentRank(ref diskDamageLevel, DiskDamageCost, SaveNewProgression);
        }

        public bool TryPurchaseDiskArea()
        {
            return TryPurchasePersistentRank(ref diskAreaLevel, DiskAreaCost, SaveNewProgression);
        }

        public bool TryPurchaseDiskPullStrength()
        {
            return TryPurchasePersistentRank(ref diskPullStrengthLevel, DiskPullStrengthCost, SaveNewProgression);
        }

        public bool TryPurchaseStructuralReinforcement() => TryPurchasePersistentRank(ref structuralReinforcementLevel, StructuralReinforcementCost, SaveCarapaceProgression);
        public bool TryPurchaseLivingRegeneration() => TryPurchasePersistentRank(ref livingRegenerationLevel, LivingRegenerationCost, SaveCarapaceProgression);
        public bool TryPurchaseAnchoredLattice()
        {
            if (anchoredLatticeLevel >= anchoredLatticeMaximumLevel || !TrySpendShards(AnchoredLatticeCost)) return false;
            anchoredLatticeLevel++;
            SaveCarapaceProgression();
            UpgradesChanged?.Invoke();
            return true;
        }

        public void NotifyHealthPotionConsumed()
        {
            if (healthPotionRegenerationLevel > 0)
            {
                healthPotionRegenerationUntil = Mathf.Max(healthPotionRegenerationUntil, Time.time)
                    + potionRegenerationDuration;
            }
        }

        public void NotifyManaPotionConsumed()
        {
            if (manaPotionRegenerationLevel > 0)
            {
                manaPotionRegenerationUntil = Mathf.Max(manaPotionRegenerationUntil, Time.time)
                    + potionRegenerationDuration;
            }
        }

        /// <summary>
        /// Explicit new-game reset seam. Death/run reset deliberately does not
        /// call this, so paid shard ranks persist across a death as required.
        /// </summary>
        public void ResetForNewGame()
        {
            generalShards = 0;
            healthPotionEfficiencyLevel = 0;
            manaPotionEfficiencyLevel = 0;
            healthPotionRegenerationLevel = 0;
            manaPotionRegenerationLevel = 0;
            diskDamageLevel = 0;
            diskAreaLevel = 0;
            diskPullStrengthLevel = 0;
            structuralReinforcementLevel = 0;
            livingRegenerationLevel = 0;
            anchoredLatticeLevel = 0;
            PlayerPrefs.DeleteKey(GeneralShardsKey);
            PlayerPrefs.DeleteKey(HealthPotionEfficiencyKey);
            PlayerPrefs.DeleteKey(ManaPotionEfficiencyKey);
            PlayerPrefs.DeleteKey(HealthPotionRegenerationKey);
            PlayerPrefs.DeleteKey(ManaPotionRegenerationKey);
            PlayerPrefs.DeleteKey(DiskDamageKey);
            PlayerPrefs.DeleteKey(DiskAreaKey);
            PlayerPrefs.DeleteKey(DiskPullStrengthKey);
            PlayerPrefs.DeleteKey(CarapaceStructuralKey);
            PlayerPrefs.DeleteKey(CarapaceRegenerationKey);
            PlayerPrefs.DeleteKey(CarapaceLatticeKey);
            PlayerPrefs.Save();
            healthPotionRegenerationUntil = 0f;
            manaPotionRegenerationUntil = 0f;
            healthPotionRegenerationAccumulator = 0f;
            GetComponent<PlayerResonantCarapace>()?.ResetForNewGame();
            PlayerManifestationOrigin manifestation = GetComponent<PlayerManifestationOrigin>();
            if (manifestation != null) manifestation.ResetForNewGame();
            else ManifestationProgression.ResetForNewGame();
            // Only definitions that declare death loss (for example future Flora)
            // are part of this explicit new-game reset; unrelated item semantics remain owned elsewhere.
            GetComponent<PlayerCollectedInventory>()?.RemoveDeathLossItems();
            GetComponent<PlayerContractRecords>()?.ResetForNewGame();
            if (GetComponent<PlayerContractRecords>() == null) PlayerContractRecords.ResetKnownRecords();
            GeneralShardsChanged?.Invoke(generalShards, 0);
            UpgradesChanged?.Invoke();
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
                SaveGeneralShards();
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

        private bool TryPurchasePersistentRank(ref int level, int cost, Action save)
        {
            if (level == int.MaxValue || !TrySpendShards(cost))
            {
                return false;
            }

            level++;
            save?.Invoke();
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
            SaveGeneralShards();
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

        private void LoadPotionEfficiencyProgression()
        {
            healthPotionEfficiencyLevel = Mathf.Max(
                healthPotionEfficiencyLevel,
                PlayerPrefs.GetInt(HealthPotionEfficiencyKey, 0));
            manaPotionEfficiencyLevel = Mathf.Max(
                manaPotionEfficiencyLevel,
                PlayerPrefs.GetInt(ManaPotionEfficiencyKey, 0));
        }

        private void LoadNewProgression()
        {
            healthPotionRegenerationLevel = Mathf.Max(healthPotionRegenerationLevel,
                PlayerPrefs.GetInt(HealthPotionRegenerationKey, 0));
            manaPotionRegenerationLevel = Mathf.Max(manaPotionRegenerationLevel,
                PlayerPrefs.GetInt(ManaPotionRegenerationKey, 0));
            diskDamageLevel = Mathf.Max(diskDamageLevel, PlayerPrefs.GetInt(DiskDamageKey, 0));
            diskAreaLevel = Mathf.Max(diskAreaLevel, PlayerPrefs.GetInt(DiskAreaKey, 0));
            diskPullStrengthLevel = Mathf.Max(diskPullStrengthLevel,
                PlayerPrefs.GetInt(DiskPullStrengthKey, 0));
        }

        private void LoadGeneralShards()
        {
            generalShards = Mathf.Max(generalShards, PlayerPrefs.GetInt(GeneralShardsKey, 0));
        }

        private void SaveGeneralShards()
        {
            PlayerPrefs.SetInt(GeneralShardsKey, generalShards);
            PlayerPrefs.Save();
        }

        private void SavePotionEfficiencyProgression()
        {
            PlayerPrefs.SetInt(HealthPotionEfficiencyKey, healthPotionEfficiencyLevel);
            PlayerPrefs.SetInt(ManaPotionEfficiencyKey, manaPotionEfficiencyLevel);
            PlayerPrefs.Save();
        }

        private void SaveNewProgression()
        {
            PlayerPrefs.SetInt(HealthPotionRegenerationKey, healthPotionRegenerationLevel);
            PlayerPrefs.SetInt(ManaPotionRegenerationKey, manaPotionRegenerationLevel);
            PlayerPrefs.SetInt(DiskDamageKey, diskDamageLevel);
            PlayerPrefs.SetInt(DiskAreaKey, diskAreaLevel);
            PlayerPrefs.SetInt(DiskPullStrengthKey, diskPullStrengthLevel);
            PlayerPrefs.Save();
        }

        private void SaveCarapaceProgression()
        {
            PlayerPrefs.SetInt(CarapaceStructuralKey, structuralReinforcementLevel);
            PlayerPrefs.SetInt(CarapaceRegenerationKey, livingRegenerationLevel);
            PlayerPrefs.SetInt(CarapaceLatticeKey, anchoredLatticeLevel);
            PlayerPrefs.Save();
        }

        private void ScheduleNextRegenerationTick()
        {
            nextRegenerationTickTime = Time.time + Mathf.Max(0.1f, regenerationTickInterval);
        }

        private void HandleRespawned()
        {
            healthRegenAccumulator = 0f;
            healthPotionRegenerationAccumulator = 0f;
            healthPotionRegenerationUntil = 0f;
            manaPotionRegenerationUntil = 0f;
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
            healthPotionEfficiencyLevel = Mathf.Max(0, healthPotionEfficiencyLevel);
            manaPotionEfficiencyLevel = Mathf.Max(0, manaPotionEfficiencyLevel);
            potionEfficiencyIncreasePerLevel = Mathf.Max(0.001f, potionEfficiencyIncreasePerLevel);
            healthPotionEfficiencyCost = Mathf.Max(1, healthPotionEfficiencyCost);
            manaPotionEfficiencyCost = Mathf.Max(1, manaPotionEfficiencyCost);
            healthPotionEfficiencyCostIncrease = Mathf.Max(0, healthPotionEfficiencyCostIncrease);
            manaPotionEfficiencyCostIncrease = Mathf.Max(0, manaPotionEfficiencyCostIncrease);
            healthPotionRegenerationLevel = Mathf.Max(0, healthPotionRegenerationLevel);
            manaPotionRegenerationLevel = Mathf.Max(0, manaPotionRegenerationLevel);
            diskDamageLevel = Mathf.Max(0, diskDamageLevel);
            diskAreaLevel = Mathf.Max(0, diskAreaLevel);
            diskPullStrengthLevel = Mathf.Max(0, diskPullStrengthLevel);
            healthPotionRegenerationCost = Mathf.Max(1, healthPotionRegenerationCost);
            manaPotionRegenerationCost = Mathf.Max(1, manaPotionRegenerationCost);
            diskDamageCost = Mathf.Max(1, diskDamageCost);
            diskAreaCost = Mathf.Max(1, diskAreaCost);
            diskPullStrengthCost = Mathf.Max(1, diskPullStrengthCost);
            healthPotionRegenerationCostIncrease = Mathf.Max(0, healthPotionRegenerationCostIncrease);
            manaPotionRegenerationCostIncrease = Mathf.Max(0, manaPotionRegenerationCostIncrease);
            diskDamageCostIncrease = Mathf.Max(0, diskDamageCostIncrease);
            diskAreaCostIncrease = Mathf.Max(0, diskAreaCostIncrease);
            diskPullStrengthCostIncrease = Mathf.Max(0, diskPullStrengthCostIncrease);
            potionRegenerationDuration = Mathf.Max(.1f, potionRegenerationDuration);
            healthPotionRegenerationPercentPerLevel = Mathf.Max(.001f, healthPotionRegenerationPercentPerLevel);
            manaPotionRegenerationPercentPerLevel = Mathf.Max(.001f, manaPotionRegenerationPercentPerLevel);
            diskDamagePerLevel = Mathf.Max(0, diskDamagePerLevel);
            diskAreaPercentPerLevel = Mathf.Max(0f, diskAreaPercentPerLevel);
            diskPullStrengthPercentPerLevel = Mathf.Max(0f, diskPullStrengthPercentPerLevel);
            MigrateLegacyUpgradeFlags();
            cavesGlareBaseStrengthMultiplier = Mathf.Max(1f, cavesGlareBaseStrengthMultiplier);
        }
    }
}
