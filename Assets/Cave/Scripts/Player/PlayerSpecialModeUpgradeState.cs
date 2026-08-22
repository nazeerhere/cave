using System;
using Cave.Combat;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCurrency), typeof(PlayerSpecialMode))]
    public sealed class PlayerSpecialModeUpgradeState : MonoBehaviour
    {
        [SerializeField] private SpecialModeTier2Settings settings;

        [Header("Session Tier 2 Ownership (Read Only)")]
        [SerializeField] private bool slowShotTier2Owned;
        [SerializeField] private bool burnShotTier2Owned;
        [SerializeField] private bool flightTier2Owned;
        [SerializeField] private bool strengthTier2Owned;

        [Header("Session Tier 3 Ownership (Read Only)")]
        [SerializeField] private bool slowShotTier3Owned;
        [SerializeField] private bool burnShotTier3Owned;
        [SerializeField] private bool flightTier3Owned;
        [SerializeField] private bool strengthTier3Owned;

        private PlayerCurrency playerCurrency;

        public event Action<SpecialMode> Tier2OwnershipChanged;
        public event Action<SpecialMode> Tier3OwnershipChanged;

        public SpecialModeTier2Settings Settings => settings;

        private void Awake()
        {
            playerCurrency = GetComponent<PlayerCurrency>();
        }

        internal void Configure(SpecialModeTier2Settings tier2Settings)
        {
            settings = tier2Settings;
        }

        public bool IsTier2Owned(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return slowShotTier2Owned;
                case SpecialMode.BurnShot:
                    return burnShotTier2Owned;
                case SpecialMode.Flight:
                    return flightTier2Owned;
                case SpecialMode.DamageBoost:
                    return strengthTier2Owned;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public int GetTier2Cost(SpecialMode mode)
        {
            return settings != null ? settings.GetTier2Cost(mode) : 0;
        }

        public bool IsTier3Owned(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return slowShotTier3Owned;
                case SpecialMode.BurnShot:
                    return burnShotTier3Owned;
                case SpecialMode.Flight:
                    return flightTier3Owned;
                case SpecialMode.DamageBoost:
                    return strengthTier3Owned;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public int GetTier3Cost(SpecialMode mode)
        {
            return settings != null ? settings.GetTier3Cost(mode) : 0;
        }

        public string GetTier3AbilityName(SpecialMode mode)
        {
            return settings != null ? settings.GetTier3AbilityName(mode) : "Tier 3";
        }

        public string GetTier3Description(SpecialMode mode)
        {
            return settings != null ? settings.GetTier3Description(mode) : "Tier 3 ability";
        }

        public int GetCurrentTier(SpecialMode mode)
        {
            if (IsTier3Owned(mode))
            {
                return 3;
            }

            return IsTier2Owned(mode) ? 2 : 1;
        }

        public string GetTier2AbilityName(SpecialMode mode)
        {
            return settings != null ? settings.GetTier2AbilityName(mode) : "Tier 2";
        }

        public string GetTier2Description(SpecialMode mode)
        {
            return settings != null ? settings.GetTier2Description(mode) : "Tier 2 ability";
        }

        public bool TryPurchaseTier2(SpecialMode mode)
        {
            if (settings == null || IsTier2Owned(mode))
            {
                return false;
            }

            int cost = settings.GetTier2Cost(mode);
            if (!playerCurrency.TrySpend(cost))
            {
                return false;
            }

            SetTier2Owned(mode);
            Tier2OwnershipChanged?.Invoke(mode);
            return true;
        }

        public bool TryPurchaseTier3(SpecialMode mode)
        {
            if (settings == null || !IsTier2Owned(mode) || IsTier3Owned(mode))
            {
                return false;
            }

            int cost = settings.GetTier3Cost(mode);
            if (!playerCurrency.TrySpend(cost))
            {
                return false;
            }

            SetTier3Owned(mode);
            Tier3OwnershipChanged?.Invoke(mode);
            return true;
        }

        private void SetTier2Owned(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    slowShotTier2Owned = true;
                    break;
                case SpecialMode.BurnShot:
                    burnShotTier2Owned = true;
                    break;
                case SpecialMode.Flight:
                    flightTier2Owned = true;
                    break;
                case SpecialMode.DamageBoost:
                    strengthTier2Owned = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void SetTier3Owned(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    slowShotTier3Owned = true;
                    break;
                case SpecialMode.BurnShot:
                    burnShotTier3Owned = true;
                    break;
                case SpecialMode.Flight:
                    flightTier3Owned = true;
                    break;
                case SpecialMode.DamageBoost:
                    strengthTier3Owned = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
