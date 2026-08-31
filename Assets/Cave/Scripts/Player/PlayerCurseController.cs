using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.InputSystem;
using Cave.Progression;
using UnityEngine;
 
namespace Cave.Player
{
    public enum PlayerCurseType
    {
        Insanity,
        Detective,
        Avarice,
        Stoneglass,
        CavesGlare
    }

    [Serializable]
    public struct AvariceWealthTier
    {
        [Min(0)] public int MinimumCurrency;
        [Range(0f, 1f)] public float DeathClaim;
        [Min(0f)] public float EnemyDetectionBonus;
        [Min(0f)] public float EnemyDangerBonus;
        [Range(0f, 0.9f)] public float PlayerMovementPenalty;
        [Min(1f)] public float SpawnPressureMultiplier;

        public AvariceWealthTier(
            int minimumCurrency,
            float deathClaim,
            float enemyDetectionBonus,
            float enemyDangerBonus,
            float playerMovementPenalty,
            float spawnPressureMultiplier = 1f)
        {
            MinimumCurrency = minimumCurrency;
            DeathClaim = deathClaim;
            EnemyDetectionBonus = enemyDetectionBonus;
            EnemyDangerBonus = enemyDangerBonus;
            PlayerMovementPenalty = playerMovementPenalty;
            SpawnPressureMultiplier = Mathf.Max(1f, spawnPressureMultiplier);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerCurseController : MonoBehaviour
    {
        [Header("Active Curses")]
        [SerializeField] private bool curseOfDistractionActive;
        [SerializeField] private bool detectivesCurseActive;
        [SerializeField] private bool curseOfAvariceActive;
        [SerializeField] private bool curseOfStoneglassActive;
        [SerializeField] private bool cavesGlareActive;

        [Header("Curse of Insanity — Combat")]
        [SerializeField, Min(1)] private int maximumInsanityStacks = 20;
        [SerializeField, Min(0f)] private float attackSpeedBonusPerStack = 0.025f;
        [SerializeField, Min(0f)] private float attackDamageBonusPerStack = 0.02f;
        [SerializeField, Min(0f)] private float incomingDamageBonusPerStack = 0.025f;
        [SerializeField, Min(0.1f)] private float combatExitDelay = 3f;
        [SerializeField, Min(0.1f)] private float combatDetectionRadius = 14f;
        [SerializeField, Min(0f)] private float collapseVisionSecondsPerStack = 0.5f;
        [SerializeField, Range(0.2f, 1f)] private float visionSizeMultiplier = 0.68f;
        [SerializeField, Min(0.1f)] private float maximumVisionPenaltyDuration = 10f;
        [SerializeField, Range(0f, 1f)] private float ordinaryDropChanceMultiplier = 0.7f;

        [Header("Curse of Insanity — Terminal Berserk")]
        [SerializeField, Min(1)] private int terminalInsanityThreshold = 20;
        [SerializeField, Min(0f)] private float terminalAttackSpeedBonus = 0.45f;
        [SerializeField, Min(0f)] private float terminalDamageBonus = 0.5f;
        [SerializeField, Min(0f)] private float terminalIncomingDamageBonus = 0.4f;
        [SerializeField, Min(0.1f)] private float berserkAttackMaintenanceWindow = 0.75f;
        [SerializeField, Range(0.2f, 1f)] private float berserkVisionSizeMultiplier = 0.58f;
        [SerializeField, Min(0.1f)] private float burnoutDuration = 4f;

        [Header("Curse of Insanity — Distraction")]
        [SerializeField, Min(0.1f)] private float distractionLifetime = 6f;
        [SerializeField, Min(0.1f)] private float distractionInfluenceRadius = 6f;
        [SerializeField, Min(0f)] private float distractionPlacementDistance = 2.5f;
        [SerializeField, Min(0f)] private float distractionCooldown = 8f;
        [SerializeField, Min(0f)] private float distractionAttentionPriority = 1f;
        [SerializeField, Min(0f)] private float distractionVisionDuration = 5f;
        [SerializeField, Min(0f)] private float distractionResistanceDuration = 20f;
        [SerializeField, Min(0)] private int repeatedDistractionInsanityStacks = 2;

        [Header("Detective's Curse")]
        [SerializeField, Range(1, 5)] private int guaranteedRealDetectives = 2;
        [SerializeField, Range(0, 2)] private int additionalResearchActions = 1;
        [SerializeField, Min(1f)] private float masteryGainMultiplier = 1.35f;
        [SerializeField, Range(0.1f, 1f)] private float worldLevelGrowthMultiplier = 0.78f;

        [Header("Curse of Avarice — Wealth Tiers")]
        [SerializeField] private AvariceWealthTier[] avariceWealthTiers =
        {
            new AvariceWealthTier(0, 0.33f, 0f, 0f, 0f, 1f),
            new AvariceWealthTier(25, 0.40f, 0.10f, 0.05f, 0.03f, 1.1f),
            new AvariceWealthTier(50, 0.50f, 0.20f, 0.10f, 0.06f, 1.2f),
            new AvariceWealthTier(100, 0.60f, 0.35f, 0.15f, 0.10f, 1.35f),
            new AvariceWealthTier(200, 0.70f, 0.50f, 0.20f, 0.12f, 1.5f)
        };

        [Header("Curse of Stoneglass — Preparation")]
        [SerializeField, Range(0.5f, 1f)] private float preparedResourceThreshold = 0.9f;
        [SerializeField, Min(0f)] private float singlePreparedDamageBonus = 0.12f;
        [SerializeField, Min(0f)] private float doublePreparedDamageBonus = 0.3f;
        [SerializeField, Min(0f)] private float fullHealthMovementBonus = 0.12f;
        [SerializeField, Min(0f)] private float fullManaMaximumGrowthPerSecond = 0.05f;
        [SerializeField, Min(0f)] private float fullStaminaMaximumGrowthPerSecond = 0.05f;
        [SerializeField, Min(0f)] private float fullResourceGrowthCapPercent = 0.25f;

        [Header("Curse of Stoneglass — Fracture")]
        [SerializeField, Min(0f)] private float manaDrainPerHit = 6f;
        [SerializeField, Min(0f)] private float staminaDrainPerHit = 8f;
        [SerializeField, Min(0f)] private float fullShatterManaDrain = 18f;
        [SerializeField, Min(0f)] private float fullShatterStaminaDrain = 24f;
        [SerializeField, Min(1f)] private float fullShatterIncomingDamageMultiplier = 1.35f;
        [SerializeField, Range(0f, 1f)] private float resistancePenaltyPerHit = 0.02f;
        [SerializeField, Min(0.1f)] private float resistanceFractureDuration = 30f;
        [SerializeField, Min(1f)] private float guardStaminaDrainMultiplier = 1.15f;

        [Header("The Cave's Glare — Player Adaptation")]
        [SerializeField, Range(0.01f, 1f)] private float strengthAdaptationFraction = 0.35f;
        [SerializeField, Min(0.001f)] private float minimumStrengthAdaptationStep = 0.05f;
        [SerializeField, Min(0f)] private float postMatchStrengthGain = 0.1f;
        [SerializeField, Min(1)] private int deathsPerPermanentCost = 3;
        [SerializeField, Min(0)] private int generalShardsConsumedPerCost = 1;
        [SerializeField, Min(0)] private int powerPurchasesConsumedPerCost = 1;
        [SerializeField, Min(0)] private int healthRegenLevelsConsumedPerCost;
        [SerializeField, Min(0)] private int manaRegenLevelsConsumedPerCost;
        [SerializeField, Min(0)] private int maximumHealthMasteryConsumedPerCost = 1;
        [SerializeField, Min(0f)] private float maximumStaminaMasteryConsumedPerCost = 1f;
        [SerializeField, Min(0f)] private float maximumManaMasteryConsumedPerCost = 1f;

        [Header("The Cave's Glare — Corruption")]
        [SerializeField, Range(0f, 1f)] private float ordinaryRevivalCorruptionChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float lowHealthCorruptionChance = 0.2f;
        [SerializeField, Range(0.01f, 0.9f)] private float lowHealthCorruptionThreshold = 0.25f;
        [SerializeField] private bool allowRevivalAfterLowHealthCorruption;
        [SerializeField, Min(0f)] private float corruptedRevivalDelay = 1.25f;
        [SerializeField, Min(0)] private int maximumCorruptedRevivals;
        [SerializeField, Range(0f, 1f)] private float elementalCorruptionChance = 0.35f;
        [SerializeField] private GameObject corruptionTransformationVfxPrefab;
        [SerializeField] private GameObject elementalReactionVfxPrefab;
        [SerializeField, Min(0.1f)] private float elementalReactionRadius = 2f;
        [SerializeField, Min(0)] private int elementalReactionDamage = 2;
        [SerializeField, Min(0f)] private float elementalReactionForce = 6f;

        [Header("Current State (Read Only)")]
        [SerializeField, Min(0f)] private float distractionCooldownRemaining;
        [SerializeField, Min(0)] private int currentAvariceTier;
        [SerializeField, Range(0f, 1f)] private float currentAvariceDeathClaim;
        [SerializeField, Min(1f)] private float currentEnemyDetectionMultiplier = 1f;
        [SerializeField, Min(1f)] private float currentEnemyDangerMultiplier = 1f;
        [SerializeField, Range(0.1f, 1f)] private float currentMovementMultiplier = 1f;
        [SerializeField, Min(1f)] private float currentAvariceSpawnPressureMultiplier = 1f;
        [SerializeField, Min(0)] private int currentInsanityStacks;
        [SerializeField, Min(0f)] private float visionPenaltyRemaining;
        [SerializeField, Min(0)] private int currentStoneglassFractureStacks;
        [SerializeField, Min(0)] private int cavesGlareDeathCount;
        [SerializeField, Min(0)] private int cavesGlareDeathsTowardCost;
        [SerializeField, Min(0f)] private float stoneglassManaMaximumGrowth;
        [SerializeField, Min(0f)] private float stoneglassStaminaMaximumGrowth;
        [SerializeField] private bool terminalBerserkActive;
        [SerializeField] private bool burnoutActive;
        [SerializeField, Min(0f)] private float berserkMaintenanceRemaining;
        [SerializeField, Min(0f)] private float burnoutRemaining;

        private PlayerAimDirection aimDirection;
        private PlayerCurrency currency;
        private PlayerController movement;
        private float nextDistractionTime;
        private PlayerHealth health;
        private PlayerMana mana;
        private SpinSwordAttack stamina;
        private PlayerVisionPenalty visionPenalty;
        private PlayerPermanentProgression permanentProgression;
        private readonly List<float> stoneglassFractureExpirations = new List<float>();
        private float lastCombatActivityTime = float.NegativeInfinity;
        private float nextCombatCheckTime;
        private float distractionPressureUntil;
        private float baseMaximumMana;
        private float baseMaximumStamina;
        private float stoneglassGrowthAccumulator;
        private float berserkMaintenanceExpiresAt;
        private float burnoutExpiresAt;

        public static PlayerCurseController Active { get; private set; }

        public event Action CurseStateChanged;
        public event Action AvariceStateChanged;
        public event Action<int, int, float> AvariceDeathClaimed;

        public bool CurseOfInsanityActive => curseOfDistractionActive;
        [Obsolete("Use CurseOfInsanityActive.")]
        public bool CurseOfDistractionActive => CurseOfInsanityActive;
        public bool DetectivesCurseActive => detectivesCurseActive;
        public bool CurseOfAvariceActive => curseOfAvariceActive;
        public bool CurseOfStoneglassActive => curseOfStoneglassActive;
        public bool CavesGlareActive => cavesGlareActive;
        public int CurrentInsanityStacks => currentInsanityStacks;
        public bool IsTerminalBerserkActive => terminalBerserkActive;
        public bool IsBurnoutActive => burnoutActive && Time.time < burnoutExpiresAt;
        public int StoneglassFractureStacks => stoneglassFractureExpirations.Count;
        public int CavesGlareDeathCount => cavesGlareDeathCount;
        public int CavesGlareDeathsTowardCost => cavesGlareDeathsTowardCost;
        public float StoneglassManaMaximumGrowth => stoneglassManaMaximumGrowth;
        public float StoneglassStaminaMaximumGrowth => stoneglassStaminaMaximumGrowth;
        public float AttackSpeedMultiplier => curseOfDistractionActive
            ? 1f + currentInsanityStacks * attackSpeedBonusPerStack
                + (terminalBerserkActive ? terminalAttackSpeedBonus : 0f)
            : 1f;
        public float OutgoingDamageMultiplier
        {
            get
            {
                float result = curseOfDistractionActive
                    ? 1f + currentInsanityStacks * attackDamageBonusPerStack
                        + (terminalBerserkActive ? terminalDamageBonus : 0f)
                    : 1f;
                if (curseOfStoneglassActive)
                {
                    bool manaPrepared = IsManaPrepared;
                    bool staminaPrepared = IsStaminaPrepared;
                    if (manaPrepared && staminaPrepared)
                    {
                        result *= 1f + doublePreparedDamageBonus;
                    }
                    else if (manaPrepared || staminaPrepared)
                    {
                        result *= 1f + singlePreparedDamageBonus;
                    }
                }

                return Mathf.Max(0f, result);
            }
        }
        public float GuardStaminaDrainMultiplier => curseOfStoneglassActive
            ? guardStaminaDrainMultiplier
            : 1f;
        public float OrdinaryDropChanceMultiplier => curseOfDistractionActive
            ? ordinaryDropChanceMultiplier
            : 1f;
        public float AvariceSpawnPressureMultiplier => curseOfAvariceActive
            ? currentAvariceSpawnPressureMultiplier
            : 1f;
        public float OrdinaryRevivalCorruptionChance => ordinaryRevivalCorruptionChance;
        public float LowHealthCorruptionChance => lowHealthCorruptionChance;
        public float LowHealthCorruptionThreshold => lowHealthCorruptionThreshold;
        public bool AllowRevivalAfterLowHealthCorruption => allowRevivalAfterLowHealthCorruption;
        public float CorruptedRevivalDelay => corruptedRevivalDelay;
        /// <summary>Zero deliberately means no cap, for Cave's Glare testing.</summary>
        public int MaximumCorruptedRevivals => maximumCorruptedRevivals;
        public float ElementalCorruptionChance => elementalCorruptionChance;
        public GameObject CorruptionTransformationVfxPrefab => corruptionTransformationVfxPrefab;
        public GameObject ElementalReactionVfxPrefab => elementalReactionVfxPrefab;
        public float ElementalReactionRadius => elementalReactionRadius;
        public int ElementalReactionDamage => elementalReactionDamage;
        public float ElementalReactionForce => elementalReactionForce;
        public int CurrentAvariceTier => currentAvariceTier;
        public int CurrentAvariceWealth => currency != null ? currency.CurrentCurrency : 0;
        public float CurrentAvariceDeathClaim => curseOfAvariceActive ? currentAvariceDeathClaim : 0f;
        public float EnemyDetectionRangeMultiplier => curseOfAvariceActive
            ? currentEnemyDetectionMultiplier
            : 1f;
        public float EnemyDangerMultiplier => curseOfAvariceActive
            ? currentEnemyDangerMultiplier
            : 1f;
        public float AvariceMovementMultiplier => curseOfAvariceActive
            ? currentMovementMultiplier
            : 1f;
        public float CurrentAvariceDetectionBonus => EnemyDetectionRangeMultiplier - 1f;
        public float CurrentAvariceDangerBonus => EnemyDangerMultiplier - 1f;
        public float CurrentAvariceMovementPenalty => 1f - AvariceMovementMultiplier;
        public int GuaranteedRealDetectives => detectivesCurseActive
            ? guaranteedRealDetectives
            : 0;
        public int AdditionalResearchActions => detectivesCurseActive
            ? additionalResearchActions
            : 0;
        public float MasteryGainMultiplier => detectivesCurseActive
            ? masteryGainMultiplier
            : 1f;
        public float WorldLevelGrowthMultiplier => detectivesCurseActive
            ? worldLevelGrowthMultiplier
            : 1f;
        public int ConfiguredGuaranteedRealDetectives => guaranteedRealDetectives;
        public float ConfiguredMasteryGainMultiplier => masteryGainMultiplier;
        public float ConfiguredWorldLevelGrowthMultiplier => worldLevelGrowthMultiplier;

        private bool IsManaPrepared => mana != null
            && mana.MaximumMana > 0f
            && mana.CurrentMana / mana.MaximumMana >= preparedResourceThreshold;
        private bool IsStaminaPrepared => stamina != null
            && stamina.MaximumStamina > 0f
            && stamina.CurrentStamina / stamina.MaximumStamina >= preparedResourceThreshold;

        private void Awake()
        {
            aimDirection = GetComponent<PlayerAimDirection>();
            currency = GetComponent<PlayerCurrency>();
            movement = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            mana = GetComponent<PlayerMana>();
            stamina = GetComponent<SpinSwordAttack>();
            permanentProgression = GetComponent<PlayerPermanentProgression>();
            visionPenalty = GetComponent<PlayerVisionPenalty>();
            if (visionPenalty == null)
            {
                visionPenalty = gameObject.AddComponent<PlayerVisionPenalty>();
            }

            baseMaximumMana = mana != null ? mana.MaximumMana : 0f;
            baseMaximumStamina = stamina != null ? stamina.MaximumStamina : 0f;
            EnsureAvariceTiers();
            RefreshAvariceState(false);
        }

        private void OnEnable()
        {
            Active = this;
            if (cavesGlareActive)
            {
                EnemyCorruptionLifecycle.EnsureOnExistingEnemies();
            }

            BindCurrency();
            if (health != null)
            {
                health.Respawned -= HandleRespawned;
                health.Respawned += HandleRespawned;
            }
            RefreshAvariceState(true);
        }

        private void Update()
        {
            distractionCooldownRemaining = Mathf.Max(0f, nextDistractionTime - Time.time);
            visionPenaltyRemaining = visionPenalty != null ? visionPenalty.RemainingDuration : 0f;
            ExpireStoneglassFractures();
            UpdateStoneglassGrowth();
            UpdateCurseMovementMultiplier();
            UpdateInsanityCombatExit();
            UpdateTerminalInsanity();
            if (curseOfDistractionActive && GameInput.UseDistractionPressed)
            {
                TryDeployDistraction();
            }
        }

        public bool IsActive(PlayerCurseType curse)
        {
            switch (curse)
            {
                case PlayerCurseType.Insanity:
                    return curseOfDistractionActive;
                case PlayerCurseType.Detective:
                    return detectivesCurseActive;
                case PlayerCurseType.Avarice:
                    return curseOfAvariceActive;
                case PlayerCurseType.Stoneglass:
                    return curseOfStoneglassActive;
                case PlayerCurseType.CavesGlare:
                    return cavesGlareActive;
                default:
                    return false;
            }
        }

        public void SetCurseActive(PlayerCurseType curse, bool active)
        {
            bool changed;
            if (curse == PlayerCurseType.Insanity)
            {
                changed = curseOfDistractionActive != active;
                curseOfDistractionActive = active;
            }
            else if (curse == PlayerCurseType.Detective)
            {
                changed = detectivesCurseActive != active;
                detectivesCurseActive = active;
            }
            else if (curse == PlayerCurseType.Avarice)
            {
                changed = curseOfAvariceActive != active;
                curseOfAvariceActive = active;
            }
            else if (curse == PlayerCurseType.Stoneglass)
            {
                changed = curseOfStoneglassActive != active;
                curseOfStoneglassActive = active;
            }
            else
            {
                changed = cavesGlareActive != active;
                cavesGlareActive = active;
            }

            if (changed)
            {
                if (curse == PlayerCurseType.Avarice)
                {
                    RefreshAvariceState(true);
                }

                if (curse == PlayerCurseType.Insanity && !active)
                {
                    currentInsanityStacks = 0;
                    terminalBerserkActive = false;
                    burnoutActive = false;
                }

                if (curse == PlayerCurseType.Stoneglass && !active)
                {
                    stoneglassFractureExpirations.Clear();
                }

                if (curse == PlayerCurseType.CavesGlare && active)
                {
                    EnemyCorruptionLifecycle.EnsureOnExistingEnemies();
                }

                UpdateCurseMovementMultiplier();

                CurseStateChanged?.Invoke();
            }
        }

        public void ApplyDeathCurrencyRule()
        {
            if (currency == null)
            {
                currency = GetComponent<PlayerCurrency>();
            }

            if (currency == null)
            {
                return;
            }

            if (!curseOfAvariceActive)
            {
                currency.ResetRunCurrency();
                return;
            }

            RefreshAvariceState(false);
            float claimRate = currentAvariceDeathClaim;
            int claimed = currency.ApplyDeathClaim(claimRate);
            AvariceDeathClaimed?.Invoke(claimed, currency.CurrentCurrency, claimRate);
        }

        public bool TryDeployDistraction()
        {
            if (!curseOfDistractionActive || Time.time < nextDistractionTime)
            {
                return false;
            }

            if (aimDirection == null)
            {
                aimDirection = GetComponent<PlayerAimDirection>();
            }

            Vector2 direction = aimDirection != null
                ? aimDirection.ReadDirection()
                : Vector2.right;
            if (direction.sqrMagnitude <= 0.01f)
            {
                direction = Vector2.right;
            }

            Vector2 position = (Vector2)transform.position
                + direction.normalized * distractionPlacementDistance;
            CursedDistraction.Create(
                gameObject,
                position,
                distractionLifetime,
                distractionInfluenceRadius,
                distractionAttentionPriority,
                distractionResistanceDuration);
            visionPenalty?.Apply(
                distractionVisionDuration,
                visionSizeMultiplier,
                maximumVisionPenaltyDuration);
            if (Time.time < distractionPressureUntil)
            {
                AddInsanityStacks(repeatedDistractionInsanityStacks);
            }

            distractionPressureUntil = Time.time
                + distractionLifetime
                + distractionResistanceDuration;
            nextDistractionTime = Time.time + distractionCooldown;
            return true;
        }

        public void NotifyPlayerKillingBlow()
        {
            if (!curseOfDistractionActive)
            {
                return;
            }

            AddInsanityStacks(1);
            lastCombatActivityTime = Time.time;
        }

        public int ResolveIncomingDamage(int baseDamage)
        {
            if (baseDamage <= 0)
            {
                return 0;
            }

            float multiplier = curseOfDistractionActive
                ? 1f + currentInsanityStacks * incomingDamageBonusPerStack
                    + (terminalBerserkActive ? terminalIncomingDamageBonus : 0f)
                : 1f;
            if (curseOfStoneglassActive)
            {
                bool fullShatter = mana != null
                    && stamina != null
                    && mana.CurrentMana >= mana.MaximumMana - 0.001f
                    && stamina.CurrentStamina >= stamina.MaximumStamina - 0.001f;
                multiplier *= 1f + stoneglassFractureExpirations.Count * resistancePenaltyPerHit;
                if (fullShatter)
                {
                    multiplier *= fullShatterIncomingDamageMultiplier;
                    mana.DrainMana(fullShatterManaDrain);
                    stamina.TrySpendStamina(Mathf.Min(
                        fullShatterStaminaDrain,
                        stamina.CurrentStamina));
                }
                else
                {
                    mana?.DrainMana(manaDrainPerHit);
                    if (stamina != null)
                    {
                        stamina.TrySpendStamina(Mathf.Min(
                            staminaDrainPerHit,
                            stamina.CurrentStamina));
                    }
                }

                stoneglassFractureExpirations.Add(Time.time + resistanceFractureDuration);
            }

            return Mathf.Max(1, Mathf.CeilToInt(baseDamage * multiplier));
        }

        public void NotifyPlayerDeath(GameObject killer)
        {
            currentInsanityStacks = 0;
            stoneglassFractureExpirations.Clear();
            if (!cavesGlareActive || killer == null)
            {
                return;
            }

            cavesGlareDeathCount++;
            cavesGlareDeathsTowardCost++;
            if (permanentProgression == null)
            {
                permanentProgression = GetComponent<PlayerPermanentProgression>();
            }

            float killerBenchmark = EnemyStrengthBenchmark.Resolve(killer);
            permanentProgression?.AdaptBaseStrengthToward(
                killerBenchmark,
                strengthAdaptationFraction,
                minimumStrengthAdaptationStep,
                postMatchStrengthGain);
            if (cavesGlareDeathsTowardCost >= deathsPerPermanentCost)
            {
                cavesGlareDeathsTowardCost = 0;
                GetComponent<PlayerResourceMastery>()?.ConsumeMasteryGrowth(
                    maximumHealthMasteryConsumedPerCost,
                    maximumStaminaMasteryConsumedPerCost,
                    maximumManaMasteryConsumedPerCost);
                permanentProgression?.ConsumeForCavesGlare(
                    generalShardsConsumedPerCost,
                    powerPurchasesConsumedPerCost,
                    healthRegenLevelsConsumedPerCost,
                    manaRegenLevelsConsumedPerCost);
            }
        }

        private void OnDisable()
        {
            if (currency != null)
            {
                currency.CurrencyChanged -= HandleCurrencyChanged;
            }

            if (health != null)
            {
                health.Respawned -= HandleRespawned;
            }

            movement?.SetCurseMovementMultiplier(1f);
            visionPenalty?.Clear();
            stoneglassFractureExpirations.Clear();
            currentInsanityStacks = 0;
            terminalBerserkActive = false;
            burnoutActive = false;
            if (Active == this)
            {
                Active = null;
            }
        }

        private void BindCurrency()
        {
            if (currency == null)
            {
                currency = GetComponent<PlayerCurrency>();
            }

            if (currency != null)
            {
                currency.CurrencyChanged -= HandleCurrencyChanged;
                currency.CurrencyChanged += HandleCurrencyChanged;
            }
        }

        private void HandleCurrencyChanged(int _)
        {
            RefreshAvariceState(true);
        }

        private void RefreshAvariceState(bool notify)
        {
            EnsureAvariceTiers();
            if (movement == null)
            {
                movement = GetComponent<PlayerController>();
            }

            int wealth = currency != null ? currency.CurrentCurrency : 0;
            int resolvedTier = 0;
            for (int index = 1; index < avariceWealthTiers.Length; index++)
            {
                if (wealth < avariceWealthTiers[index].MinimumCurrency)
                {
                    break;
                }

                resolvedTier = index;
            }

            AvariceWealthTier tier = avariceWealthTiers[resolvedTier];
            currentAvariceTier = resolvedTier;
            currentAvariceDeathClaim = Mathf.Clamp01(tier.DeathClaim);
            currentEnemyDetectionMultiplier = 1f + Mathf.Max(0f, tier.EnemyDetectionBonus);
            currentEnemyDangerMultiplier = 1f + Mathf.Max(0f, tier.EnemyDangerBonus);
            currentMovementMultiplier = 1f - Mathf.Clamp(tier.PlayerMovementPenalty, 0f, 0.9f);
            currentAvariceSpawnPressureMultiplier = tier.SpawnPressureMultiplier >= 1f
                ? tier.SpawnPressureMultiplier
                : 1f + Mathf.Max(0f, tier.EnemyDangerBonus);
            UpdateCurseMovementMultiplier();
            if (notify)
            {
                AvariceStateChanged?.Invoke();
            }
        }

        private void EnsureAvariceTiers()
        {
            if (avariceWealthTiers != null && avariceWealthTiers.Length > 0)
            {
                return;
            }

            avariceWealthTiers = new[]
            {
                new AvariceWealthTier(0, 0.33f, 0f, 0f, 0f, 1f),
                new AvariceWealthTier(25, 0.40f, 0.10f, 0.05f, 0.03f, 1.1f),
                new AvariceWealthTier(50, 0.50f, 0.20f, 0.10f, 0.06f, 1.2f),
                new AvariceWealthTier(100, 0.60f, 0.35f, 0.15f, 0.10f, 1.35f),
                new AvariceWealthTier(200, 0.70f, 0.50f, 0.20f, 0.12f, 1.5f)
            };
        }

        private void AddInsanityStacks(int amount)
        {
            currentInsanityStacks = Mathf.Clamp(
                currentInsanityStacks + Mathf.Max(0, amount),
                0,
                maximumInsanityStacks);
            lastCombatActivityTime = Time.time;
            if (curseOfDistractionActive && currentInsanityStacks >= terminalInsanityThreshold)
            {
                BeginTerminalBerserk();
            }
        }

        /// <summary>Called by real committed player attacks, never raw input.</summary>
        public void NotifyOffensiveCommitment()
        {
            if (terminalBerserkActive)
            {
                berserkMaintenanceExpiresAt = Time.time + berserkAttackMaintenanceWindow;
            }
        }

        private void BeginTerminalBerserk()
        {
            if (terminalBerserkActive)
            {
                return;
            }

            terminalBerserkActive = true;
            burnoutActive = false;
            berserkMaintenanceExpiresAt = Time.time + berserkAttackMaintenanceWindow;
            visionPenalty?.Apply(
                berserkAttackMaintenanceWindow,
                berserkVisionSizeMultiplier,
                maximumVisionPenaltyDuration);
        }

        private void UpdateTerminalInsanity()
        {
            berserkMaintenanceRemaining = terminalBerserkActive
                ? Mathf.Max(0f, berserkMaintenanceExpiresAt - Time.time)
                : 0f;
            burnoutRemaining = IsBurnoutActive
                ? Mathf.Max(0f, burnoutExpiresAt - Time.time)
                : 0f;
            if (terminalBerserkActive)
            {
                visionPenalty?.Apply(
                    berserkAttackMaintenanceWindow,
                    berserkVisionSizeMultiplier,
                    maximumVisionPenaltyDuration);
                if (Time.time >= berserkMaintenanceExpiresAt)
                {
                    BeginBurnout();
                }
            }
            else if (burnoutActive && Time.time >= burnoutExpiresAt)
            {
                burnoutActive = false;
            }
        }

        private void BeginBurnout()
        {
            if (!terminalBerserkActive)
            {
                return;
            }

            terminalBerserkActive = false;
            currentInsanityStacks = 0;
            burnoutActive = true;
            burnoutExpiresAt = Time.time + burnoutDuration;
            visionPenalty?.Apply(burnoutDuration, berserkVisionSizeMultiplier, maximumVisionPenaltyDuration);
        }

        private void UpdateInsanityCombatExit()
        {
            if (!curseOfDistractionActive || currentInsanityStacks <= 0 || Time.time < nextCombatCheckTime)
            {
                return;
            }

            nextCombatCheckTime = Time.time + 0.5f;
            if (HasEngagedHostileNearby())
            {
                lastCombatActivityTime = Time.time;
                return;
            }

            if (Time.time < lastCombatActivityTime + combatExitDelay)
            {
                return;
            }

            if (terminalBerserkActive)
            {
                BeginBurnout();
                return;
            }

            float duration = Mathf.Min(
                maximumVisionPenaltyDuration,
                currentInsanityStacks * collapseVisionSecondsPerStack);
            currentInsanityStacks = 0;
            visionPenalty?.Apply(duration, visionSizeMultiplier, maximumVisionPenaltyDuration);
        }

        private bool HasEngagedHostileNearby()
        {
            float radiusSquared = combatDetectionRadius * combatDetectionRadius;
            foreach (Damageable candidate in FindObjectsOfType<Damageable>())
            {
                if (candidate == null
                    || candidate.CurrentHealth <= 0
                    || ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude
                        > radiusSquared)
                {
                    continue;
                }

                MobBrainBase brain = candidate.GetComponent<MobBrainBase>();
                if (brain != null
                    && brain.CurrentState != MobBrainState.Patrol
                    && brain.CurrentState != MobBrainState.ReturnToPatrol)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateStoneglassGrowth()
        {
            if (!curseOfStoneglassActive || mana == null || stamina == null)
            {
                stoneglassGrowthAccumulator = 0f;
                return;
            }

            stoneglassGrowthAccumulator += Time.deltaTime;
            if (stoneglassGrowthAccumulator < 1f)
            {
                return;
            }

            float elapsed = stoneglassGrowthAccumulator;
            stoneglassGrowthAccumulator = 0f;
            float manaCap = baseMaximumMana * (1f + fullResourceGrowthCapPercent);
            float staminaCap = baseMaximumStamina * (1f + fullResourceGrowthCapPercent);
            if (mana.CurrentMana >= mana.MaximumMana - 0.001f && mana.MaximumMana < manaCap)
            {
                float growth = Mathf.Min(
                    fullManaMaximumGrowthPerSecond * elapsed,
                    manaCap - mana.MaximumMana);
                if (mana.IncreaseMaximumMana(growth, true))
                {
                    stoneglassManaMaximumGrowth += growth;
                }
            }

            if (stamina.CurrentStamina >= stamina.MaximumStamina - 0.001f
                && stamina.MaximumStamina < staminaCap)
            {
                float growth = Mathf.Min(
                    fullStaminaMaximumGrowthPerSecond * elapsed,
                    staminaCap - stamina.MaximumStamina);
                if (stamina.IncreaseMaximumStamina(growth, true))
                {
                    stoneglassStaminaMaximumGrowth += growth;
                }
            }
        }

        private void UpdateCurseMovementMultiplier()
        {
            if (movement == null)
            {
                movement = GetComponent<PlayerController>();
            }

            float multiplier = AvariceMovementMultiplier;
            if (curseOfStoneglassActive
                && health != null
                && health.CurrentHealth >= health.MaxHealth)
            {
                multiplier *= 1f + fullHealthMovementBonus;
            }

            movement?.SetCurseMovementMultiplier(multiplier);
        }

        private void ExpireStoneglassFractures()
        {
            for (int index = stoneglassFractureExpirations.Count - 1; index >= 0; index--)
            {
                if (Time.time >= stoneglassFractureExpirations[index])
                {
                    stoneglassFractureExpirations.RemoveAt(index);
                }
            }

            currentStoneglassFractureStacks = stoneglassFractureExpirations.Count;
        }

        private void HandleRespawned()
        {
            currentInsanityStacks = 0;
            terminalBerserkActive = false;
            burnoutActive = false;
            stoneglassFractureExpirations.Clear();
            visionPenalty?.Clear();
        }

        private void OnValidate()
        {
            masteryGainMultiplier = Mathf.Max(1f, masteryGainMultiplier);
            guaranteedRealDetectives = Mathf.Clamp(guaranteedRealDetectives, 1, 5);
            EnsureAvariceTiers();
            int previousThreshold = 0;
            for (int index = 0; index < avariceWealthTiers.Length; index++)
            {
                AvariceWealthTier tier = avariceWealthTiers[index];
                tier.MinimumCurrency = index == 0
                    ? 0
                    : Mathf.Max(previousThreshold, tier.MinimumCurrency);
                tier.DeathClaim = Mathf.Clamp01(tier.DeathClaim);
                tier.EnemyDetectionBonus = Mathf.Max(0f, tier.EnemyDetectionBonus);
                tier.EnemyDangerBonus = Mathf.Max(0f, tier.EnemyDangerBonus);
                tier.PlayerMovementPenalty = Mathf.Clamp(tier.PlayerMovementPenalty, 0f, 0.9f);
                tier.SpawnPressureMultiplier = tier.SpawnPressureMultiplier > 0f
                    ? Mathf.Max(1f, tier.SpawnPressureMultiplier)
                    : 1f + tier.EnemyDangerBonus;
                avariceWealthTiers[index] = tier;
                previousThreshold = tier.MinimumCurrency;
            }

            maximumVisionPenaltyDuration = Mathf.Clamp(maximumVisionPenaltyDuration, 0.1f, 10f);
            deathsPerPermanentCost = Mathf.Max(1, deathsPerPermanentCost);
            fullShatterIncomingDamageMultiplier = Mathf.Max(1f, fullShatterIncomingDamageMultiplier);
            guardStaminaDrainMultiplier = Mathf.Max(1f, guardStaminaDrainMultiplier);
        }
    }
}
