using System;
using Cave.Combat;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    public enum StrengthShieldState
    {
        Unavailable,
        Inactive,
        Ready,
        Broken,
        Recharging
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMana), typeof(PlayerSpecialMode))]
    [RequireComponent(typeof(PlayerSpecialModeUpgradeState))]
    public sealed class PlayerStrengthShield : MonoBehaviour
    {
        [Header("Strength Tier 2 Shield")]
        [SerializeField, Min(0f)] private float rechargeDelay = 8f;
        [SerializeField, Min(0.01f)] private float rechargeDuration = 6f;
        [SerializeField, Min(0f)] private float fullRechargeManaCost = 20f;

        [Header("Current Shield (Read Only)")]
        [SerializeField] private StrengthShieldState currentState = StrengthShieldState.Unavailable;
        [SerializeField, Range(0f, 1f)] private float rechargeProgress = 1f;

        private PlayerMana playerMana;
        private PlayerSpecialMode specialMode;
        private PlayerSpecialModeUpgradeState upgrades;
        private bool hasShieldCharge = true;
        private float delayRemaining;

        public event Action<StrengthShieldState, float> ShieldStateChanged;
        public event Action ShieldBroken;

        public StrengthShieldState CurrentState => currentState;
        public float RechargeProgress => rechargeProgress;
        public bool IsProtectionActive => specialMode != null
            && upgrades != null
            && specialMode.CurrentMode == SpecialMode.DamageBoost
            && upgrades.IsTier2Owned(SpecialMode.DamageBoost);

        private void Awake()
        {
            playerMana = GetComponent<PlayerMana>();
            specialMode = GetComponent<PlayerSpecialMode>();
            upgrades = GetComponent<PlayerSpecialModeUpgradeState>();
        }

        private void OnEnable()
        {
            if (specialMode != null)
            {
                specialMode.SpecialModeChanged += HandleModeChanged;
            }

            if (upgrades != null)
            {
                upgrades.Tier2OwnershipChanged += HandleTier2OwnershipChanged;
            }
        }

        internal void Configure(SpecialModeTier2Settings settings)
        {
            if (settings != null)
            {
                rechargeDelay = settings.ShieldRechargeDelay;
                rechargeDuration = settings.ShieldRechargeDuration;
                fullRechargeManaCost = settings.ShieldFullRechargeManaCost;
            }

            RefreshState();
        }

        private void Update()
        {
            if (!IsProtectionActive || hasShieldCharge)
            {
                return;
            }

            if (delayRemaining > 0f)
            {
                delayRemaining = Mathf.Max(0f, delayRemaining - Time.deltaTime);
                SetState(StrengthShieldState.Broken, rechargeProgress);
                return;
            }

            float timeProgress = Mathf.Min(
                1f - rechargeProgress,
                Time.deltaTime / Mathf.Max(0.01f, rechargeDuration));
            float progressToApply = timeProgress;
            if (fullRechargeManaCost > 0f)
            {
                float requestedMana = fullRechargeManaCost * timeProgress;
                float availableMana = Mathf.Min(requestedMana, playerMana.CurrentMana);
                if (availableMana <= 0f || !playerMana.TrySpendMana(availableMana))
                {
                    SetState(StrengthShieldState.Recharging, rechargeProgress);
                    return;
                }

                progressToApply = availableMana / fullRechargeManaCost;
            }

            float updatedProgress = Mathf.Clamp01(rechargeProgress + progressToApply);
            if (updatedProgress >= 1f)
            {
                hasShieldCharge = true;
                SetState(StrengthShieldState.Ready, 1f);
            }
            else
            {
                SetState(StrengthShieldState.Recharging, updatedProgress);
            }
        }

        public bool TryAbsorbDamage(int incomingDamage)
        {
            if (incomingDamage <= 0 || !IsProtectionActive || !hasShieldCharge)
            {
                return false;
            }

            hasShieldCharge = false;
            rechargeProgress = 0f;
            delayRemaining = rechargeDelay;
            SetState(StrengthShieldState.Broken, 0f);
            ShieldBroken?.Invoke();
            return true;
        }

        private void HandleModeChanged(SpecialMode mode)
        {
            RefreshState();
        }

        private void HandleTier2OwnershipChanged(SpecialMode mode)
        {
            if (mode == SpecialMode.DamageBoost)
            {
                RefreshState();
            }
        }

        private void RefreshState()
        {
            if (upgrades == null || !upgrades.IsTier2Owned(SpecialMode.DamageBoost))
            {
                SetState(StrengthShieldState.Unavailable, rechargeProgress);
            }
            else if (!IsProtectionActive)
            {
                SetState(StrengthShieldState.Inactive, rechargeProgress);
            }
            else if (hasShieldCharge)
            {
                SetState(StrengthShieldState.Ready, 1f);
            }
            else if (delayRemaining > 0f)
            {
                SetState(StrengthShieldState.Broken, rechargeProgress);
            }
            else
            {
                SetState(StrengthShieldState.Recharging, rechargeProgress);
            }
        }

        private void SetState(StrengthShieldState state, float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            if (currentState == state && Mathf.Approximately(rechargeProgress, clampedProgress))
            {
                return;
            }

            currentState = state;
            rechargeProgress = clampedProgress;
            ShieldStateChanged?.Invoke(currentState, rechargeProgress);
        }

        private void OnDisable()
        {
            if (specialMode != null)
            {
                specialMode.SpecialModeChanged -= HandleModeChanged;
            }

            if (upgrades != null)
            {
                upgrades.Tier2OwnershipChanged -= HandleTier2OwnershipChanged;
            }
        }
    }
}
