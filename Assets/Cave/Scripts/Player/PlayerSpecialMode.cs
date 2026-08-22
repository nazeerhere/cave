using System;
using Cave.Combat;
using UnityEngine;

namespace Cave.Player
{
    [RequireComponent(typeof(PlayerCurrency))]
    public sealed class PlayerSpecialMode : MonoBehaviour
    {
        [SerializeField] private SpecialMode startingMode = SpecialMode.SlowShot;

        [Header("Currency Switch Costs")]
        [SerializeField, Min(0)] private int slowShotSwitchCost = 3;
        [SerializeField, Min(0)] private int burnShotSwitchCost = 3;
        [SerializeField, Min(0)] private int flightSwitchCost = 5;
        [SerializeField, Min(0)] private int damageBoostSwitchCost = 5;

        private PlayerCurrency playerCurrency;

        public event Action<SpecialMode> SpecialModeChanged;

        public SpecialMode CurrentMode { get; private set; }

        private void Awake()
        {
            playerCurrency = GetComponent<PlayerCurrency>();
            CurrentMode = startingMode;
        }

        public bool TrySwitchMode(SpecialMode newMode)
        {
            if (newMode == CurrentMode)
            {
                return true;
            }

            int switchCost = GetSwitchCost(newMode);
            if (!playerCurrency.TrySpend(switchCost))
            {
                return false;
            }

            CurrentMode = newMode;
            SpecialModeChanged?.Invoke(CurrentMode);
            return true;
        }

        public int GetSwitchCost(SpecialMode mode)
        {
            switch (mode)
            {
                case SpecialMode.SlowShot:
                    return slowShotSwitchCost;
                case SpecialMode.BurnShot:
                    return burnShotSwitchCost;
                case SpecialMode.Flight:
                    return flightSwitchCost;
                case SpecialMode.DamageBoost:
                    return damageBoostSwitchCost;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
