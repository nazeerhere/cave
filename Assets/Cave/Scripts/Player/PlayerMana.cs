using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cave.Player
{
    public sealed class PlayerMana : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximumMana = 100f;
        [SerializeField, Min(0f)] private float startingMana = 100f;

        [Header("Skill Tier Mana Multipliers")]
        [SerializeField, Min(0f)] private float tierOneManaCostMultiplier = 1f;
        [SerializeField, Min(0f)] private float tierTwoManaCostMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float tierThreeManaCostMultiplier = 2f;
        [SerializeField, Min(0f)] private float tierFourManaCostMultiplier = 2.75f;

        public event Action<float, float> ManaChanged;

        private readonly Dictionary<int, float> costMultipliers =
            new Dictionary<int, float>();

        public float CurrentMana { get; private set; }
        public float MaximumMana => maximumMana;
        public float ManaCostMultiplier
        {
            get
            {
                float result = 1f;
                foreach (float multiplier in costMultipliers.Values)
                {
                    result = Mathf.Max(result, multiplier);
                }

                return result;
            }
        }
        public bool HasManaCostPenalty => ManaCostMultiplier > 1.001f;

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
            float finalCost = GetModifiedManaCost(amount);
            if (finalCost <= 0f || CurrentMana < finalCost)
            {
                return false;
            }

            SetMana(CurrentMana - finalCost);
            return true;
        }

        public bool TrySpendMana(float baseAmount, int skillTier)
        {
            return TrySpendMana(GetTierAdjustedBaseManaCost(baseAmount, skillTier));
        }

        public float DrainMana(float amount)
        {
            float previous = CurrentMana;
            SetMana(CurrentMana - Mathf.Max(0f, amount));
            return previous - CurrentMana;
        }

        public bool CanSpendMana(float baseCost)
        {
            float finalCost = GetModifiedManaCost(baseCost);
            return finalCost > 0f && CurrentMana >= finalCost;
        }

        public bool CanSpendMana(float baseCost, int skillTier)
        {
            return CanSpendMana(GetTierAdjustedBaseManaCost(baseCost, skillTier));
        }

        public float GetModifiedManaCost(float baseCost)
        {
            return Mathf.Max(0f, baseCost) * ManaCostMultiplier;
        }

        public float GetFinalManaCost(float baseCost, int skillTier)
        {
            return GetModifiedManaCost(GetTierAdjustedBaseManaCost(baseCost, skillTier));
        }

        public float GetTierAdjustedBaseManaCost(float baseCost, int skillTier)
        {
            float multiplier = skillTier >= 4
                ? tierFourManaCostMultiplier
                : skillTier == 3
                    ? tierThreeManaCostMultiplier
                    : skillTier == 2
                        ? tierTwoManaCostMultiplier
                        : tierOneManaCostMultiplier;
            return Mathf.Max(0f, baseCost) * Mathf.Max(0f, multiplier);
        }

        public float GetAffordableBaseManaCost(float requestedBaseCost)
        {
            return Mathf.Min(
                Mathf.Max(0f, requestedBaseCost),
                CurrentMana / Mathf.Max(1f, ManaCostMultiplier));
        }

        public void SetCostModifier(UnityEngine.Object source, float multiplier)
        {
            if (source == null)
            {
                return;
            }

            costMultipliers[source.GetInstanceID()] = Mathf.Max(1f, multiplier);
        }

        public void RemoveCostModifier(UnityEngine.Object source)
        {
            if (source != null)
            {
                costMultipliers.Remove(source.GetInstanceID());
            }
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

        public bool ReduceMaximumMana(float amount, float minimumMaximumMana = 1f)
        {
            float permitted = Mathf.Min(
                Mathf.Max(0f, amount),
                Mathf.Max(0f, maximumMana - Mathf.Max(1f, minimumMaximumMana)));
            if (permitted <= 0f)
            {
                return false;
            }

            maximumMana -= permitted;
            CurrentMana = Mathf.Min(CurrentMana, maximumMana);
            ManaChanged?.Invoke(CurrentMana, maximumMana);
            return true;
        }

        public void ResetCurrentMana()
        {
            SetMana(maximumMana);
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

        private void OnDisable()
        {
            costMultipliers.Clear();
        }
    }
}
