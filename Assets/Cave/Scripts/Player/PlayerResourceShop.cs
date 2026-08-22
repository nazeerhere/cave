using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCurrency), typeof(PlayerHealth), typeof(PlayerMana))]
    public sealed class PlayerResourceShop : MonoBehaviour
    {
        [SerializeField] private SpecialModeTier2Settings settings;

        private PlayerCurrency playerCurrency;
        private PlayerHealth playerHealth;
        private PlayerMana playerMana;

        public int HealthPotionCost => settings != null ? settings.HealthPotionCost : 0;
        public float HealthPotionRestorePercent => settings != null ? settings.HealthPotionRestorePercent : 0f;
        public int ManaPotionCost => settings != null ? settings.ManaPotionCost : 0;
        public float ManaPotionRestorePercent => settings != null ? settings.ManaPotionRestorePercent : 0f;

        private void Awake()
        {
            playerCurrency = GetComponent<PlayerCurrency>();
            playerHealth = GetComponent<PlayerHealth>();
            playerMana = GetComponent<PlayerMana>();
        }

        internal void Configure(SpecialModeTier2Settings shopSettings)
        {
            settings = shopSettings;
        }

        public bool TryBuyHealthPotion(out int restoredHealth)
        {
            restoredHealth = 0;
            if (settings == null
                || playerHealth.CurrentHealth >= playerHealth.MaxHealth
                || !playerCurrency.CanSpend(settings.HealthPotionCost))
            {
                return false;
            }

            int restoreAmount = Mathf.Max(
                1,
                Mathf.CeilToInt(playerHealth.MaxHealth * settings.HealthPotionRestorePercent));
            if (!playerCurrency.TrySpend(settings.HealthPotionCost))
            {
                return false;
            }

            int healthBefore = playerHealth.CurrentHealth;
            playerHealth.RestoreHealth(restoreAmount);
            restoredHealth = playerHealth.CurrentHealth - healthBefore;
            return restoredHealth > 0;
        }

        public bool TryBuyManaPotion(out float restoredMana)
        {
            restoredMana = 0f;
            if (settings == null
                || playerMana.CurrentMana >= playerMana.MaximumMana
                || !playerCurrency.CanSpend(settings.ManaPotionCost))
            {
                return false;
            }

            float restoreAmount = Mathf.Max(
                0.01f,
                playerMana.MaximumMana * settings.ManaPotionRestorePercent);
            if (!playerCurrency.TrySpend(settings.ManaPotionCost))
            {
                return false;
            }

            float manaBefore = playerMana.CurrentMana;
            playerMana.RestoreMana(restoreAmount);
            restoredMana = playerMana.CurrentMana - manaBefore;
            return restoredMana > 0f;
        }
    }
}
