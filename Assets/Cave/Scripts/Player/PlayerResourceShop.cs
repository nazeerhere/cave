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

        public int HealthPotionCost => settings != null ? settings.HealthPotionCost : 0;
        public float HealthPotionRestorePercent => settings != null ? settings.HealthPotionRestorePercent : 0f;
        public int ManaPotionCost => settings != null ? settings.ManaPotionCost : 0;
        public float ManaPotionRestorePercent => settings != null ? settings.ManaPotionRestorePercent : 0f;
        public int LandmineCost => ResolveConsumables() != null ? consumables.CurrencyCost : 0;
        public int OwnedHealthPotions => ResolveConsumables() != null
            ? consumables.OwnedHealthPotions
            : 0;
        public int OwnedManaPotions => ResolveConsumables() != null
            ? consumables.OwnedManaPotions
            : 0;
        public int OwnedLandmines => ResolveConsumables() != null
            ? consumables.OwnedLandmines
            : 0;

        private void Awake()
        {
            playerCurrency = GetComponent<PlayerCurrency>();
            consumables = GetComponent<PlayerLandmineInventory>();
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
                || !playerCurrency.TrySpend(settings.HealthPotionCost))
            {
                return false;
            }

            inventory.AddHealthPotion();
            ownedQuantity = inventory.OwnedHealthPotions;
            return true;
        }

        public bool TryBuyManaPotion(out int ownedQuantity)
        {
            ownedQuantity = OwnedManaPotions;
            PlayerLandmineInventory inventory = ResolveConsumables();
            if (settings == null
                || inventory == null
                || !playerCurrency.TrySpend(settings.ManaPotionCost))
            {
                return false;
            }

            inventory.AddManaPotion();
            ownedQuantity = inventory.OwnedManaPotions;
            return true;
        }

        public bool TryBuyLandmine()
        {
            PlayerLandmineInventory inventory = ResolveConsumables();
            return inventory != null && inventory.TryPurchaseLandmine();
        }

        private PlayerLandmineInventory ResolveConsumables()
        {
            if (consumables == null)
            {
                consumables = GetComponent<PlayerLandmineInventory>();
            }

            return consumables;
        }
    }
}
