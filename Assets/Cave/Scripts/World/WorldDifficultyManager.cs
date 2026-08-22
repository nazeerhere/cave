using System;
using Cave.Progression;
using UnityEngine;

namespace Cave.World
{
    [DisallowMultipleComponent]
    public sealed class WorldDifficultyManager : MonoBehaviour
    {
        [SerializeField] private ProgressionDifficultySettings settings;
        [SerializeField] private PlayerResourceMastery playerMastery;

        [Header("Current Difficulty (Read Only)")]
        [SerializeField] private float currentCombinedGrowthPercent;
        [SerializeField] private int currentDifficultyTier;
        [SerializeField] private float currentDifficultyMultiplier = 1f;

        public event Action DifficultyChanged;

        public ProgressionDifficultySettings Settings => settings;
        public float CombinedGrowthPercent => currentCombinedGrowthPercent;
        public int DifficultyTier => currentDifficultyTier;
        public float DifficultyMultiplier => currentDifficultyMultiplier;

        internal void Configure(
            PlayerResourceMastery mastery,
            ProgressionDifficultySettings progressionSettings)
        {
            if (playerMastery != null)
            {
                playerMastery.MasteryChanged -= RecalculateDifficulty;
            }

            playerMastery = mastery;
            settings = progressionSettings;

            if (playerMastery != null)
            {
                playerMastery.MasteryChanged -= RecalculateDifficulty;
                playerMastery.MasteryChanged += RecalculateDifficulty;
            }

            RecalculateDifficulty();
        }

        public float GetStatScale(float scalingStrength)
        {
            return Mathf.Pow(currentDifficultyMultiplier, Mathf.Max(0f, scalingStrength));
        }

        private void RecalculateDifficulty()
        {
            int previousTier = currentDifficultyTier;
            currentCombinedGrowthPercent = CalculateCombinedGrowth();

            float growthPerTier = settings != null
                ? Mathf.Max(0.0001f, settings.GrowthPerDifficultyTier)
                : 0.08f;
            float exponentPerTier = settings != null
                ? Mathf.Max(0f, settings.DifficultyExponentPerTier)
                : 0.18f;

            currentDifficultyTier = Mathf.Max(
                0,
                Mathf.FloorToInt(currentCombinedGrowthPercent / growthPerTier));
            currentDifficultyMultiplier = Mathf.Exp(exponentPerTier * currentDifficultyTier);

            if (previousTier == currentDifficultyTier)
            {
                return;
            }

            DifficultyChanged?.Invoke();
            if (settings != null && settings.LogDifficultyTierChanges)
            {
                Debug.Log(
                    "WORLD DIFFICULTY TIER " + currentDifficultyTier
                    + "\nMultiplier: " + currentDifficultyMultiplier.ToString("0.00"),
                    this);
            }
        }

        private float CalculateCombinedGrowth()
        {
            if (playerMastery == null)
            {
                return 0f;
            }

            float healthWeight = settings != null ? Mathf.Max(0f, settings.HealthGrowthWeight) : 1f;
            float staminaWeight = settings != null ? Mathf.Max(0f, settings.StaminaGrowthWeight) : 1f;
            float manaWeight = settings != null ? Mathf.Max(0f, settings.ManaGrowthWeight) : 1f;
            float totalWeight = healthWeight + staminaWeight + manaWeight;
            if (totalWeight <= 0f)
            {
                return 0f;
            }

            return (
                playerMastery.HealthGrowthPercent * healthWeight
                + playerMastery.StaminaGrowthPercent * staminaWeight
                + playerMastery.ManaGrowthPercent * manaWeight)
                / totalWeight;
        }

        private void OnDestroy()
        {
            if (playerMastery != null)
            {
                playerMastery.MasteryChanged -= RecalculateDifficulty;
            }
        }
    }
}
