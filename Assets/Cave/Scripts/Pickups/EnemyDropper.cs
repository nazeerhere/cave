using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.World;
using UnityEngine;

namespace Cave.Pickups
{
    [RequireComponent(typeof(Damageable))]
    public sealed class EnemyDropper : MonoBehaviour
    {
        [SerializeField] private EnemyDropSettings sharedSettings;
        [SerializeField] private DropEntry[] overrideEntries;
        [SerializeField] private Vector2 overrideSpawnOffset = new Vector2(0f, 0.35f);

        [Header("General Drop Quality")]
        [SerializeField, Min(1f)] private float generalPrimaryChanceMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float veteranPrimaryChanceBonusPerDeath = 0.05f;
        [SerializeField, Range(0f, 1f)] private float generalAdditionalCategoryBonus = 0.2f;
        [SerializeField, Range(0f, 0.2f)] private float veteranAdditionalBonusPerDeath = 0.015f;

        private Damageable damageable;
        private bool hasEvaluatedDrop;
        private WorldDifficultyManager difficultyManager;

        private enum DropCategory
        {
            Health,
            Mana,
            Currency
        }

        internal void UseSharedSettings(EnemyDropSettings settings)
        {
            sharedSettings = settings;
        }

        internal void ConfigureDifficulty(WorldDifficultyManager manager)
        {
            difficultyManager = manager;
        }

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            hasEvaluatedDrop = false;
            damageable.Died += HandleDeath;
        }

        private void OnDisable()
        {
            damageable.Died -= HandleDeath;
        }

        private void HandleDeath()
        {
            EnemyCorruptionLifecycle corruption = GetComponent<EnemyCorruptionLifecycle>();
            if (corruption != null && corruption.SuppressDeathRewards)
            {
                return;
            }

            if (hasEvaluatedDrop)
            {
                return;
            }

            hasEvaluatedDrop = true;
            DropEntry[] entries = overrideEntries != null && overrideEntries.Length > 0
                ? overrideEntries
                : sharedSettings != null ? sharedSettings.Entries : null;
            Vector2 spawnOffset = overrideEntries != null && overrideEntries.Length > 0
                ? overrideSpawnOffset
                : sharedSettings != null ? sharedSettings.SpawnOffset : overrideSpawnOffset;

            if (entries == null || entries.Length == 0)
            {
                return;
            }

            List<DropEntry> eligibleEntries = BuildEligibleEntries(entries);
            SkeletonInheritance inheritance = GetComponent<SkeletonInheritance>();
            bool isGeneral = inheritance != null && inheritance.Rank == SkeletonRank.General;
            int witnessedDeaths = isGeneral ? inheritance.WitnessedDeaths : 0;
            float primaryChanceMultiplier = isGeneral
                ? generalPrimaryChanceMultiplier
                    + witnessedDeaths * veteranPrimaryChanceBonusPerDeath
                : 1f;
            float towerDropMultiplier = DetectiveTower.GetOrdinaryDropRateMultiplier(
                transform.position);
            float insanityDropMultiplier = Cave.Player.PlayerCurseController.Active != null
                ? Cave.Player.PlayerCurseController.Active.OrdinaryDropChanceMultiplier
                : 1f;
            primaryChanceMultiplier *= towerDropMultiplier * insanityDropMultiplier;
            DropEntry primary = ChoosePrimary(eligibleEntries, primaryChanceMultiplier);
            if (primary == null)
            {
                return;
            }

            int difficultyTier = difficultyManager != null ? difficultyManager.DifficultyTier : 0;
            DifficultyDropBand band = sharedSettings != null
                ? sharedSettings.GetDifficultyBand(difficultyTier)
                : null;
            HashSet<DropCategory> spawnedCategories = new HashSet<DropCategory>();
            SpawnEntry(primary, spawnOffset, band, 0);
            spawnedCategories.Add(GetCategory(primary));

            float additionalChance = band != null ? band.AdditionalCategoryChance : 0f;
            if (isGeneral)
            {
                additionalChance = Mathf.Clamp01(
                    additionalChance
                    + generalAdditionalCategoryBonus
                    + witnessedDeaths * veteranAdditionalBonusPerDeath);
            }
            additionalChance *= towerDropMultiplier * insanityDropMultiplier;

            if (Random.value >= additionalChance)
            {
                return;
            }

            DropEntry second = ChooseAdditional(eligibleEntries, spawnedCategories);
            if (second == null)
            {
                return;
            }

            SpawnEntry(second, spawnOffset, band, 1);
            spawnedCategories.Add(GetCategory(second));
            if (band == null
                || Random.value >= band.ThirdCategoryChance
                    * towerDropMultiplier
                    * insanityDropMultiplier)
            {
                return;
            }

            DropEntry third = ChooseAdditional(eligibleEntries, spawnedCategories);
            if (third != null)
            {
                SpawnEntry(third, spawnOffset, band, 2);
            }
        }

        private static List<DropEntry> BuildEligibleEntries(DropEntry[] entries)
        {
            List<DropEntry> eligible = new List<DropEntry>();
            foreach (DropEntry entry in entries)
            {
                if (entry != null
                    && entry.PickupPrefab != null
                    && !(entry.PickupPrefab is StaminaPickup))
                {
                    eligible.Add(entry);
                }
            }

            return eligible;
        }

        private static DropEntry ChoosePrimary(
            List<DropEntry> entries,
            float chanceMultiplier)
        {
            float roll = Random.value;
            float cumulativeChance = 0f;
            foreach (DropEntry entry in entries)
            {
                cumulativeChance = Mathf.Min(
                    1f,
                    cumulativeChance
                        + Mathf.Clamp01(entry.Chance) * Mathf.Max(0f, chanceMultiplier));
                if (roll < cumulativeChance)
                {
                    return entry;
                }
            }

            return null;
        }

        private static DropEntry ChooseAdditional(
            List<DropEntry> entries,
            HashSet<DropCategory> excludedCategories)
        {
            float totalWeight = 0f;
            foreach (DropEntry entry in entries)
            {
                if (!excludedCategories.Contains(GetCategory(entry)))
                {
                    totalWeight += Mathf.Max(0f, entry.Chance);
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = Random.value * totalWeight;
            foreach (DropEntry entry in entries)
            {
                if (excludedCategories.Contains(GetCategory(entry)))
                {
                    continue;
                }

                roll -= Mathf.Max(0f, entry.Chance);
                if (roll <= 0f)
                {
                    return entry;
                }
            }

            return null;
        }

        private void SpawnEntry(
            DropEntry entry,
            Vector2 baseOffset,
            DifficultyDropBand band,
            int spawnIndex)
        {
            float horizontalOffset = spawnIndex == 0 ? 0f : spawnIndex == 1 ? -0.22f : 0.22f;
            Vector2 spreadOffset = baseOffset + new Vector2(horizontalOffset, 0f);
            PickupBase pickup = Instantiate(
                entry.PickupPrefab,
                (Vector2)transform.position + spreadOffset,
                Quaternion.identity);
            if (band == null)
            {
                return;
            }

            if (pickup is HealthPickup healthPickup)
            {
                healthPickup.SetAmount(band.HealthAmount);
            }
            else if (pickup is ManaPickup manaPickup)
            {
                manaPickup.SetAmount(band.ManaAmount);
            }
            else if (pickup is CurrencyPickup currencyPickup)
            {
                currencyPickup.SetAmount(band.CurrencyAmount);
            }
        }

        private static DropCategory GetCategory(DropEntry entry)
        {
            if (entry.PickupPrefab is HealthPickup)
            {
                return DropCategory.Health;
            }

            if (entry.PickupPrefab is ManaPickup)
            {
                return DropCategory.Mana;
            }

            return DropCategory.Currency;
        }
    }
}
