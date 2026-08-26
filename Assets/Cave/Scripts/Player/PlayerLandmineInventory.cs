using System;
using Cave.Combat;
using Cave.InputSystem;
using Cave.Progression;
using UnityEngine;

namespace Cave.Player
{
    public enum PlayerConsumableType
    {
        HealthPotion,
        ManaPotion,
        Landmine
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCurrency))]
    public sealed class PlayerLandmineInventory : MonoBehaviour
    {
        [Header("Shop / Inventory")]
        [SerializeField, Min(0)] private int currencyCost = 8;
        [SerializeField, Min(0)] private int ownedHealthPotions;
        [SerializeField, Min(0)] private int ownedManaPotions;
        [SerializeField, Min(0)] private int ownedLandmines;

        [Header("Potion Effects")]
        [SerializeField, Range(0.01f, 1f)] private float healthPotionRestorePercent = 0.25f;
        [SerializeField, Range(0.01f, 1f)] private float manaPotionRestorePercent = 0.25f;

        [Header("Placement")]
        [SerializeField] private PlayerLandmine landminePrefab;
        [SerializeField] private Vector2 placementOffset = new Vector2(0f, 0.08f);

        [Header("Fallback Mine Tuning")]
        [SerializeField, Min(0f)] private float armingDelay = 0.65f;
        [SerializeField, Min(0.1f)] private float triggerRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float blastRadius = 2.2f;
        [SerializeField, Min(0)] private int damage = 2;
        [SerializeField, Min(0f)] private float knockback = 11f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        private PlayerCurrency currency;
        private PlayerHealth playerHealth;
        private PlayerMana playerMana;
        private PlayerResourceMastery mastery;
        private Collider2D playerCollider;

        public event Action<int> InventoryChanged;
        public event Action<PlayerConsumableType, int> ConsumableQuantityChanged;

        public int CurrencyCost => currencyCost;
        public int OwnedHealthPotions => ownedHealthPotions;
        public int OwnedManaPotions => ownedManaPotions;
        public int OwnedLandmines => ownedLandmines;

        private void Awake()
        {
            currency = GetComponent<PlayerCurrency>();
            playerHealth = GetComponent<PlayerHealth>();
            playerMana = GetComponent<PlayerMana>();
            mastery = GetComponent<PlayerResourceMastery>();
            playerCollider = GetComponent<Collider2D>();
        }

        internal void Configure(SpecialModeTier2Settings settings)
        {
            if (settings == null)
            {
                return;
            }

            healthPotionRestorePercent = settings.HealthPotionRestorePercent;
            manaPotionRestorePercent = settings.ManaPotionRestorePercent;
        }

        private void Update()
        {
            if (GameInput.UseHealthPotionPressed)
            {
                TryUseHealthPotion();
            }

            if (GameInput.UseManaPotionPressed)
            {
                TryUseManaPotion();
            }

            if (GameInput.PlaceLandminePressed)
            {
                TryPlaceLandmine();
            }
        }

        public int GetOwnedCount(PlayerConsumableType type)
        {
            switch (type)
            {
                case PlayerConsumableType.HealthPotion:
                    return ownedHealthPotions;
                case PlayerConsumableType.ManaPotion:
                    return ownedManaPotions;
                default:
                    return ownedLandmines;
            }
        }

        public void AddHealthPotion()
        {
            ownedHealthPotions++;
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.HealthPotion,
                ownedHealthPotions);
        }

        public void AddManaPotion()
        {
            ownedManaPotions++;
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.ManaPotion,
                ownedManaPotions);
        }

        public bool TryUseHealthPotion()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (ownedHealthPotions <= 0 || playerHealth == null)
            {
                return false;
            }

            int restoreAmount = Mathf.Max(
                1,
                Mathf.CeilToInt(playerHealth.MaxHealth * healthPotionRestorePercent));
            if (!playerHealth.RestoreHealth(restoreAmount))
            {
                return false;
            }

            ownedHealthPotions--;
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.HealthPotion,
                ownedHealthPotions);
            return true;
        }

        public bool TryUseManaPotion()
        {
            if (playerMana == null)
            {
                playerMana = GetComponent<PlayerMana>();
            }

            if (ownedManaPotions <= 0 || playerMana == null)
            {
                return false;
            }

            float restoreAmount = Mathf.Max(
                0.01f,
                playerMana.MaximumMana * manaPotionRestorePercent);
            if (!playerMana.RestoreMana(restoreAmount))
            {
                return false;
            }

            ownedManaPotions--;
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.ManaPotion,
                ownedManaPotions);
            return true;
        }

        public bool TryPurchaseLandmine()
        {
            if (currency == null || !currency.TrySpend(currencyCost))
            {
                return false;
            }

            ownedLandmines++;
            InventoryChanged?.Invoke(ownedLandmines);
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.Landmine,
                ownedLandmines);
            return true;
        }

        public bool TryPlaceLandmine()
        {
            if (ownedLandmines <= 0)
            {
                return false;
            }

            Vector2 position = transform.position;
            if (playerCollider != null)
            {
                position = new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y);
            }

            position += placementOffset;
            PlayerLandmine mine;
            if (landminePrefab != null)
            {
                mine = Instantiate(landminePrefab, position, Quaternion.identity);
            }
            else
            {
                GameObject mineObject = new GameObject("Player Landmine");
                mineObject.transform.position = position;
                mine = mineObject.AddComponent<PlayerLandmine>();
                mine.ConfigureFallback(
                    armingDelay,
                    triggerRadius,
                    blastRadius,
                    damage,
                    knockback,
                    enemyLayers);
            }

            if (mastery == null)
            {
                mastery = GetComponent<PlayerResourceMastery>();
            }

            DamageContext context = mastery != null
                ? mastery.CreatePlayerDamageContext().WithTraits(
                    DamageTrait.AreaOfEffect | DamageTrait.StaggerHeavy)
                : default;
            mine.Arm(gameObject, context);
            ownedLandmines--;
            InventoryChanged?.Invoke(ownedLandmines);
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.Landmine,
                ownedLandmines);
            return true;
        }

        public void ResetRunInventory()
        {
            ownedHealthPotions = 0;
            ownedManaPotions = 0;
            ownedLandmines = 0;
            InventoryChanged?.Invoke(ownedLandmines);
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.HealthPotion, 0);
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.ManaPotion, 0);
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.Landmine, 0);
        }
    }
}
