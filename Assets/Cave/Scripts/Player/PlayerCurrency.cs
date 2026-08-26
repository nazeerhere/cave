using System;
using UnityEngine;

namespace Cave.Player
{
    public sealed class PlayerCurrency : MonoBehaviour
    {
        public event Action<int> CurrencyChanged;

        public int CurrentCurrency { get; private set; }

        public void AddCurrency(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            long newBalance = (long)CurrentCurrency + amount;
            CurrentCurrency = (int)Math.Min(int.MaxValue, newBalance);
            CurrencyChanged?.Invoke(CurrentCurrency);
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && CurrentCurrency >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            CurrentCurrency -= amount;
            CurrencyChanged?.Invoke(CurrentCurrency);
            return true;
        }

        public void ResetRunCurrency()
        {
            if (CurrentCurrency == 0)
            {
                return;
            }

            CurrentCurrency = 0;
            CurrencyChanged?.Invoke(CurrentCurrency);
        }

        public int ApplyDeathClaim(float claimFraction)
        {
            if (CurrentCurrency <= 0)
            {
                return 0;
            }

            int claimed = Mathf.Clamp(
                Mathf.CeilToInt(CurrentCurrency * Mathf.Clamp01(claimFraction)),
                0,
                CurrentCurrency);
            CurrentCurrency -= claimed;
            CurrencyChanged?.Invoke(CurrentCurrency);
            return claimed;
        }
    }
}
