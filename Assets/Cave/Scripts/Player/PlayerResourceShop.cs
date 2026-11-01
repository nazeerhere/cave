using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCurrency), typeof(PlayerHealth), typeof(PlayerMana))]
    [RequireComponent(typeof(PlayerLandmineInventory))]
    public sealed class PlayerResourceShop : MonoBehaviour
    {
        [SerializeField] private SpecialModeTier2Settings settings;

        private PlayerCurrency playerCurrency;
        private PlayerLandmineInventory consumables;
        private PlayerPermanentProgression progression;

        public int HealthPotionCost => settings != null
            ? PotionEconomyPolicy.ResolvePrice(settings.HealthPotionCost, settings.HealthPotionRestorePercent,
                HealthPotionRestorePercent, ResolveProgression()?.HealthPotionRegenerationLevel ?? 0)
            : 0;
        public float HealthPotionRestorePercent => settings != null
            ? PotionEconomyPolicy.ResolvePotency(settings.HealthPotionRestorePercent, ResolveProgression()?.HealthPotionEfficiencyLevel ?? 0,
                ResolveProgression()?.PotionEfficiencyIncreasePerLevel ?? 0f)
            : 0f;
        public int ManaPotionCost => settings != null
            ? PotionEconomyPolicy.ResolvePrice(settings.ManaPotionCost, settings.ManaPotionRestorePercent,
                ManaPotionRestorePercent, ResolveProgression()?.ManaPotionRegenerationLevel ?? 0)
            : 0;
        public float ManaPotionRestorePercent => settings != null
            ? PotionEconomyPolicy.ResolvePotency(settings.ManaPotionRestorePercent, ResolveProgression()?.ManaPotionEfficiencyLevel ?? 0,
                ResolveProgression()?.PotionEfficiencyIncreasePerLevel ?? 0f)
            : 0f;
        public int LandmineCost => ResolveConsumables() != null
            ? consumables.GetNextDiskCapacityCost(ResolveProgression()?.DiskCombinedUpgradeLevel ?? 0)
            : 0;
        public int OwnedHealthPotions => ResolveConsumables() != null
            ? consumables.OwnedHealthPotions
            : 0;
        public int OwnedManaPotions => ResolveConsumables() != null
            ? consumables.OwnedManaPotions
            : 0;
        public int OwnedLandmines => ResolveConsumables() != null
            ? consumables.OwnedLandmines
            : 0;
        public int DiskCapacity => ResolveConsumables() != null ? consumables.DiskCapacity : 0;
        public int StoredDiskCharges => ResolveConsumables() != null ? consumables.StoredDiskCharges : 0;
        public float DiskRechargeProgress => ResolveConsumables() != null ? consumables.DiskRechargeProgress : 0f;
        public float DiskRechargeSeconds => ResolveConsumables() != null ? consumables.RechargeSecondsPerCharge : 0f;
        public string NextDiskCapacityLabel => ResolveConsumables() != null
            ? consumables.NextDiskCapacityLabel
            : "OBLIVION DISK I";
        public bool IsDiskAtMaximumCapacity => ResolveConsumables() != null && consumables.DiskCapacity >= 3;

        private void Awake()
        {
            playerCurrency = GetComponent<PlayerCurrency>();
            consumables = GetComponent<PlayerLandmineInventory>();
            progression = GetComponent<PlayerPermanentProgression>();
        }

        internal void Configure(SpecialModeTier2Settings shopSettings)
        {
            settings = shopSettings;
            ResolveConsumables()?.Configure(shopSettings);
        }

        public bool TryBuyHealthPotion(out int ownedQuantity)
        {
            ownedQuantity = OwnedHealthPotions;
            PlayerLandmineInventory inventory = ResolveConsumables();
            if (settings == null
                || inventory == null
                || !playerCurrency.TrySpend(HealthPotionCost))
            {
                return false;
            }

            inventory.AddHealthPotion(HealthPotionRestorePercent);
            ownedQuantity = inventory.OwnedHealthPotions;
            return true;
        }

        public bool TryBuyManaPotion(out int ownedQuantity)
        {
            ownedQuantity = OwnedManaPotions;
            PlayerLandmineInventory inventory = ResolveConsumables();
            if (settings == null
                || inventory == null
                || !playerCurrency.TrySpend(ManaPotionCost))
            {
                return false;
            }

            inventory.AddManaPotion(ManaPotionRestorePercent);
            ownedQuantity = inventory.OwnedManaPotions;
            return true;
        }

        public bool TryBuyLandmine()
        {
            PlayerLandmineInventory inventory = ResolveConsumables();
            return inventory != null && inventory.TryPurchaseLandmine(
                ResolveProgression()?.DiskCombinedUpgradeLevel ?? 0);
        }

        private PlayerLandmineInventory ResolveConsumables()
        {
            if (consumables == null)
            {
                consumables = GetComponent<PlayerLandmineInventory>();
            }

            return consumables;
        }

        private PlayerPermanentProgression ResolveProgression()
        {
            if (progression == null)
            {
                progression = GetComponent<PlayerPermanentProgression>();
            }

            return progression;
        }
    }

    /// <summary>Shared deterministic potion economics; currency is always rounded up.</summary>
    public static class PotionEconomyPolicy
    {
        public static float ResolvePotency(float basePotency, int level, float increasePerLevel)
        {
            return Mathf.Clamp01(Mathf.Max(0f, basePotency)
                + Mathf.Max(0, level) * Mathf.Max(0f, increasePerLevel));
        }

        public static int ResolvePrice(
            int basePrice,
            float basePotency,
            float potency,
            int associatedUpgradeRank = 0)
        {
            float safeBasePotency = Mathf.Max(.0001f, basePotency);
            float resolvedBasePrice = Mathf.Max(0, basePrice);
            float potencyAdjustedPrice = resolvedBasePrice * Mathf.Max(0f, potency) / safeBasePotency;
            float progressionSurcharge = resolvedBasePrice * .25f * Mathf.Max(0, associatedUpgradeRank);
            return Mathf.Max(0, Mathf.CeilToInt(potencyAdjustedPrice + progressionSurcharge));
        }
    }
}
