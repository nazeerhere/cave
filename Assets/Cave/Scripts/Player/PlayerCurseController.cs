using System;
using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    public enum PlayerCurseType
    {
        Distraction,
        Detective,
        Avarice
    }

    [Serializable]
    public struct AvariceWealthTier
    {
        [Min(0)] public int MinimumCurrency;
        [Range(0f, 1f)] public float DeathClaim;
        [Min(0f)] public float EnemyDetectionBonus;
        [Min(0f)] public float EnemyDangerBonus;
        [Range(0f, 0.9f)] public float PlayerMovementPenalty;

        public AvariceWealthTier(
            int minimumCurrency,
            float deathClaim,
            float enemyDetectionBonus,
            float enemyDangerBonus,
            float playerMovementPenalty)
        {
            MinimumCurrency = minimumCurrency;
            DeathClaim = deathClaim;
            EnemyDetectionBonus = enemyDetectionBonus;
            EnemyDangerBonus = enemyDangerBonus;
            PlayerMovementPenalty = playerMovementPenalty;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerCurseController : MonoBehaviour
    {
        [Header("Active Curses")]
        [SerializeField] private bool curseOfDistractionActive;
        [SerializeField] private bool detectivesCurseActive;
        [SerializeField] private bool curseOfAvariceActive;

        [Header("Curse of Distraction")]
        [SerializeField, Min(0.1f)] private float distractionLifetime = 6f;
        [SerializeField, Min(0.1f)] private float distractionInfluenceRadius = 6f;
        [SerializeField, Min(0f)] private float distractionPlacementDistance = 2.5f;
        [SerializeField, Min(0f)] private float distractionCooldown = 8f;
        [SerializeField, Min(0f)] private float distractionAttentionPriority = 1f;

        [Header("Detective's Curse")]
        [SerializeField, Range(1, 5)] private int guaranteedRealDetectives = 2;
        [SerializeField, Range(0, 2)] private int additionalResearchActions = 1;
        [SerializeField, Min(1f)] private float masteryGainMultiplier = 1.35f;
        [SerializeField, Range(0.1f, 1f)] private float worldLevelGrowthMultiplier = 0.78f;

        [Header("Curse of Avarice — Wealth Tiers")]
        [SerializeField] private AvariceWealthTier[] avariceWealthTiers =
        {
            new AvariceWealthTier(0, 0.33f, 0f, 0f, 0f),
            new AvariceWealthTier(25, 0.40f, 0.10f, 0.05f, 0.03f),
            new AvariceWealthTier(50, 0.50f, 0.20f, 0.10f, 0.06f),
            new AvariceWealthTier(100, 0.60f, 0.35f, 0.15f, 0.10f),
            new AvariceWealthTier(200, 0.70f, 0.50f, 0.20f, 0.12f)
        };

        [Header("Current State (Read Only)")]
        [SerializeField, Min(0f)] private float distractionCooldownRemaining;
        [SerializeField, Min(0)] private int currentAvariceTier;
        [SerializeField, Range(0f, 1f)] private float currentAvariceDeathClaim;
        [SerializeField, Min(1f)] private float currentEnemyDetectionMultiplier = 1f;
        [SerializeField, Min(1f)] private float currentEnemyDangerMultiplier = 1f;
        [SerializeField, Range(0.1f, 1f)] private float currentMovementMultiplier = 1f;

        private PlayerAimDirection aimDirection;
        private PlayerCurrency currency;
        private PlayerController movement;
        private float nextDistractionTime;

        public static PlayerCurseController Active { get; private set; }

        public event Action CurseStateChanged;
        public event Action AvariceStateChanged;
        public event Action<int, int, float> AvariceDeathClaimed;

        public bool CurseOfDistractionActive => curseOfDistractionActive;
        public bool DetectivesCurseActive => detectivesCurseActive;
        public bool CurseOfAvariceActive => curseOfAvariceActive;
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

        private void Awake()
        {
            aimDirection = GetComponent<PlayerAimDirection>();
            currency = GetComponent<PlayerCurrency>();
            movement = GetComponent<PlayerController>();
            EnsureAvariceTiers();
            RefreshAvariceState(false);
        }

        private void OnEnable()
        {
            Active = this;
            BindCurrency();
            RefreshAvariceState(true);
        }

        private void Update()
        {
            distractionCooldownRemaining = Mathf.Max(0f, nextDistractionTime - Time.time);
            if (curseOfDistractionActive && GameInput.UseDistractionPressed)
            {
                TryDeployDistraction();
            }
        }

        public bool IsActive(PlayerCurseType curse)
        {
            switch (curse)
            {
                case PlayerCurseType.Distraction:
                    return curseOfDistractionActive;
                case PlayerCurseType.Detective:
                    return detectivesCurseActive;
                case PlayerCurseType.Avarice:
                    return curseOfAvariceActive;
                default:
                    return false;
            }
        }

        public void SetCurseActive(PlayerCurseType curse, bool active)
        {
            bool changed;
            if (curse == PlayerCurseType.Distraction)
            {
                changed = curseOfDistractionActive != active;
                curseOfDistractionActive = active;
            }
            else if (curse == PlayerCurseType.Detective)
            {
                changed = detectivesCurseActive != active;
                detectivesCurseActive = active;
            }
            else
            {
                changed = curseOfAvariceActive != active;
                curseOfAvariceActive = active;
            }

            if (changed)
            {
                if (curse == PlayerCurseType.Avarice)
                {
                    RefreshAvariceState(true);
                }

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
                distractionAttentionPriority);
            nextDistractionTime = Time.time + distractionCooldown;
            return true;
        }

        private void OnDisable()
        {
            if (currency != null)
            {
                currency.CurrencyChanged -= HandleCurrencyChanged;
            }

            movement?.SetCurseMovementMultiplier(1f);
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
            movement?.SetCurseMovementMultiplier(AvariceMovementMultiplier);
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
                new AvariceWealthTier(0, 0.33f, 0f, 0f, 0f),
                new AvariceWealthTier(25, 0.40f, 0.10f, 0.05f, 0.03f),
                new AvariceWealthTier(50, 0.50f, 0.20f, 0.10f, 0.06f),
                new AvariceWealthTier(100, 0.60f, 0.35f, 0.15f, 0.10f),
                new AvariceWealthTier(200, 0.70f, 0.50f, 0.20f, 0.12f)
            };
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
                avariceWealthTiers[index] = tier;
                previousThreshold = tier.MinimumCurrency;
            }
        }
    }
}
