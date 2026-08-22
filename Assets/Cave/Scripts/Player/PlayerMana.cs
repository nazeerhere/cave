using System;
using UnityEngine;

namespace Cave.Player
{
    public sealed class PlayerMana : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximumMana = 100f;
        [SerializeField, Min(0f)] private float startingMana = 100f;

        public event Action<float, float> ManaChanged;

        public float CurrentMana { get; private set; }
        public float MaximumMana => maximumMana;

        private void Awake()
        {
            CurrentMana = Mathf.Clamp(startingMana, 0f, maximumMana);
        }

        public bool RestoreMana(float amount)
        {
            if (amount <= 0f || CurrentMana >= maximumMana)
            {
                return false;
            }

            SetMana(CurrentMana + amount);
            return true;
        }

        public bool TrySpendMana(float amount)
        {
            if (amount <= 0f || CurrentMana < amount)
            {
                return false;
            }

            SetMana(CurrentMana - amount);
            return true;
        }

        public bool IncreaseMaximumMana(float amount, bool addIncreaseToCurrentMana = true)
        {
            if (amount <= 0f)
            {
                return false;
            }

            maximumMana += amount;
            if (addIncreaseToCurrentMana)
            {
                CurrentMana = Mathf.Min(maximumMana, CurrentMana + amount);
            }

            ManaChanged?.Invoke(CurrentMana, maximumMana);
            return true;
        }

        private void SetMana(float value)
        {
            float clampedValue = Mathf.Clamp(value, 0f, maximumMana);
            if (Mathf.Approximately(CurrentMana, clampedValue))
            {
                return;
            }

            CurrentMana = clampedValue;
            ManaChanged?.Invoke(CurrentMana, maximumMana);
        }

        private void OnValidate()
        {
            startingMana = Mathf.Clamp(startingMana, 0f, maximumMana);
        }
    }
}
