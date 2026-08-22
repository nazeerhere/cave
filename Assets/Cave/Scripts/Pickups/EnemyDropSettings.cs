using System;
using UnityEngine;

namespace Cave.Pickups
{
    [Serializable]
    public sealed class DifficultyDropBand
    {
        [SerializeField, Min(0)] private int minimumDifficultyTier;
        [SerializeField] private Vector2Int healthAmountRange = new Vector2Int(1, 1);
        [SerializeField] private Vector2 manaAmountRange = new Vector2(25f, 25f);
        [SerializeField] private Vector2Int currencyAmountRange = new Vector2Int(1, 1);
        [SerializeField, Range(0f, 1f)] private float additionalCategoryChance;
        [SerializeField, Range(0f, 1f)] private float thirdCategoryChance;

        public int MinimumDifficultyTier => minimumDifficultyTier;
        public int HealthAmount => UnityEngine.Random.Range(
            Mathf.Max(1, healthAmountRange.x),
            Mathf.Max(healthAmountRange.x, healthAmountRange.y) + 1);
        public float ManaAmount => UnityEngine.Random.Range(
            Mathf.Max(0.01f, manaAmountRange.x),
            Mathf.Max(manaAmountRange.x, manaAmountRange.y));
        public int CurrencyAmount => UnityEngine.Random.Range(
            Mathf.Max(1, currencyAmountRange.x),
            Mathf.Max(currencyAmountRange.x, currencyAmountRange.y) + 1);
        public float AdditionalCategoryChance => additionalCategoryChance;
        public float ThirdCategoryChance => thirdCategoryChance;
    }

    [CreateAssetMenu(fileName = "EnemyDropSettings", menuName = "Cave/Enemy Drop Settings")]
    public sealed class EnemyDropSettings : ScriptableObject
    {
        [SerializeField] private DropEntry[] entries;
        [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 0.35f);
        [SerializeField] private DifficultyDropBand[] difficultyBands;

        public DropEntry[] Entries => entries;
        public Vector2 SpawnOffset => spawnOffset;

        public DifficultyDropBand GetDifficultyBand(int difficultyTier)
        {
            DifficultyDropBand selected = null;
            if (difficultyBands == null)
            {
                return null;
            }

            foreach (DifficultyDropBand band in difficultyBands)
            {
                if (band != null
                    && band.MinimumDifficultyTier <= difficultyTier
                    && (selected == null
                        || band.MinimumDifficultyTier > selected.MinimumDifficultyTier))
                {
                    selected = band;
                }
            }

            return selected;
        }
    }
}
