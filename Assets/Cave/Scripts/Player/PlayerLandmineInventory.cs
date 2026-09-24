using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.FieldControl;
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
        private const string DiskCapacityKey = "Cave.OblivionDisk.Capacity";
        private const int MaximumDiskCapacity = 3;

        [Header("Shop / Inventory")]
        [SerializeField, Min(0)] private int currencyCost = 18;
        [SerializeField, Min(0.1f)] private float rechargeSecondsPerCharge = 12f;
        [SerializeField, Min(0)] private int ownedHealthPotions;
        [SerializeField, Min(0)] private int ownedManaPotions;

        [Header("Potion Effects")]
        [SerializeField, Range(0.01f, 1f)] private float healthPotionRestorePercent = 0.25f;
        [SerializeField, Range(0.01f, 1f)] private float manaPotionRestorePercent = 0.25f;

        [Header("Placement")]
        [SerializeField] private PlayerLandmine landminePrefab;
        [SerializeField] private Vector2 placementOffset = new Vector2(0f, 0.08f);
        [SerializeField, Min(1)] private int maximumActiveMines = 3;
        [SerializeField, Min(0.05f)] private float sacrificeHoldThreshold = 0.45f;

        [Header("Editable Presentation")]
        [SerializeField] private GameObject diskVisualPrefab;
        [SerializeField] private GameObject localPulseVisualPrefab;
        [SerializeField] private GameObject fieldLinkVisualPrefab;
        [SerializeField] private GameObject overloadVisualPrefab;
        [SerializeField] private GameObject sacrificeExplosionVisualPrefab;

        [Header("Field Node Tuning")]
        [SerializeField, Min(1)] private int mineHealth = 8;
        [SerializeField, Min(0.1f)] private float mineEnergy = 8f;
        [SerializeField, Min(0f)] private float mineLifetime = 36f;
        [SerializeField, Min(0.1f)] private float maximumLinkDistance = 9f;
        [SerializeField, Min(0.01f)] private float fieldBaseStrength = 12f;
        [SerializeField, Min(0.01f)] private float fieldDistanceOffset = 1f;
        [SerializeField, Min(0.1f)] private float fieldDistanceExponent = 1.25f;

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
        private FieldNetwork fieldNetwork;
        private readonly List<PlayerLandmine> activeMines = new List<PlayerLandmine>(3);
        private ulong placementSequence;
        private bool trackingMineInput;
        private bool sacrificeTriggered;
        private float mineInputStartedAt;
        private int diskCapacity;
        private int storedDiskCharges;
        private float nextDiskChargeAt;

        public event Action<int> InventoryChanged;
        public event Action<PlayerConsumableType, int> ConsumableQuantityChanged;
        public event Action DiskChargeStateChanged;

        public int CurrencyCost => NextDiskCapacityCost;
        public int OwnedHealthPotions => ownedHealthPotions;
        public int OwnedManaPotions => ownedManaPotions;
        public int OwnedLandmines => storedDiskCharges;
        public int DiskCapacity => diskCapacity;
        public int StoredDiskCharges => storedDiskCharges;
        public int NextDiskCapacityCost => diskCapacity >= MaximumDiskCapacity
            ? 0
            : currencyCost * (diskCapacity + 1);
        public float RechargeSecondsPerCharge => rechargeSecondsPerCharge;
        public bool IsDiskUnlocked => diskCapacity > 0;
        public bool IsDiskRecharging => diskCapacity > 0 && storedDiskCharges < diskCapacity;
        public float DiskRechargeProgress => !IsDiskRecharging
            ? 1f
            : Mathf.Clamp01(1f - (nextDiskChargeAt - Time.time) / Mathf.Max(.1f, rechargeSecondsPerCharge));
        public string NextDiskCapacityLabel => diskCapacity switch
        {
            0 => "OBLIVION DISK I",
            1 => "OBLIVION DISK II",
            2 => "OBLIVION DISK III",
            _ => "OBLIVION DISK MAX"
        };

        private void Awake()
        {
            currency = GetComponent<PlayerCurrency>();
            playerHealth = GetComponent<PlayerHealth>();
            playerMana = GetComponent<PlayerMana>();
            mastery = GetComponent<PlayerResourceMastery>();
            playerCollider = GetComponent<Collider2D>();
            LoadDiskCapacity();
            fieldNetwork = GetComponent<FieldNetwork>();
            if (fieldNetwork == null) fieldNetwork = gameObject.AddComponent<FieldNetwork>();
            fieldNetwork.Configure(
                FieldOwnerTeam.Player,
                enemyLayers,
                maximumActiveMines,
                maximumLinkDistance,
                fieldBaseStrength,
                fieldDistanceOffset,
                fieldDistanceExponent);
            fieldNetwork.SetLinkVisualPrefab(fieldLinkVisualPrefab);
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

            HandleMineInput();
            UpdateDiskRecharge();
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
                    return storedDiskCharges;
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
            if (diskCapacity >= MaximumDiskCapacity
                || currency == null
                || !currency.TrySpend(NextDiskCapacityCost))
            {
                return false;
            }

            diskCapacity++;
            PlayerPrefs.SetInt(DiskCapacityKey, diskCapacity);
            PlayerPrefs.Save();
            // Capacity purchases are permanent equipment upgrades and arrive
            // ready to use; subsequent placements always recharge sequentially.
            storedDiskCharges = diskCapacity;
            nextDiskChargeAt = 0f;
            NotifyDiskStateChanged();
            return true;
        }

        public bool TryPlaceLandmine()
        {
            PruneInactiveMines();
            if (storedDiskCharges <= 0 || activeMines.Count >= maximumActiveMines)
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
            mine.ConfigurePresentation(diskVisualPrefab, localPulseVisualPrefab,
                overloadVisualPrefab, sacrificeExplosionVisualPrefab);
            mine.Arm(gameObject, context);
            mine.ConfigureFieldNode(
                fieldNetwork,
                FieldOwnerTeam.Player,
                mineHealth,
                mineEnergy,
                mineLifetime,
                ++placementSequence);
            mine.Removed -= HandleMineRemoved;
            mine.Removed += HandleMineRemoved;
            activeMines.Add(mine);
            storedDiskCharges--;
            if (storedDiskCharges < diskCapacity && nextDiskChargeAt <= 0f)
            {
                nextDiskChargeAt = Time.time + rechargeSecondsPerCharge;
            }
            NotifyDiskStateChanged();
            return true;
        }

        private void HandleMineInput()
        {
            if (GameInput.PlaceLandminePressed)
            {
                trackingMineInput = true;
                sacrificeTriggered = false;
                mineInputStartedAt = Time.time;
            }

            if (trackingMineInput && !sacrificeTriggered && GameInput.PlaceLandmineHeld
                && Time.time >= mineInputStartedAt + sacrificeHoldThreshold)
            {
                sacrificeTriggered = TrySacrificeWeakestMine();
            }

            if (trackingMineInput && GameInput.PlaceLandmineReleased)
            {
                if (!sacrificeTriggered) TryPlaceLandmine();
                trackingMineInput = false;
            }
        }

        public bool TrySacrificeWeakestMine()
        {
            PruneInactiveMines();
            PlayerLandmine selected = null;
            float lowest = float.MaxValue;
            for (int index = 0; index < activeMines.Count; index++)
            {
                PlayerLandmine candidate = activeMines[index];
                if (candidate == null || !candidate.IsActiveFieldNode) continue;
                float score = candidate.SurvivabilityFraction;
                if (selected == null || score < lowest - .0001f
                    || (Mathf.Abs(score - lowest) <= .0001f && candidate.CreationOrder > selected.CreationOrder))
                {
                    selected = candidate;
                    lowest = score;
                }
            }
            if (selected == null) return false;
            selected.Sacrifice();
            return true;
        }

        private void HandleMineRemoved(PlayerLandmine mine)
        {
            if (mine != null) mine.Removed -= HandleMineRemoved;
            activeMines.Remove(mine);
        }

        private void PruneInactiveMines()
        {
            for (int index = activeMines.Count - 1; index >= 0; index--)
            {
                PlayerLandmine mine = activeMines[index];
                if (mine != null && mine.gameObject.activeInHierarchy) continue;
                if (mine != null) mine.Removed -= HandleMineRemoved;
                activeMines.RemoveAt(index);
            }
        }

        public void ResetRunInventory()
        {
            trackingMineInput = false;
            sacrificeTriggered = false;
            for (int index = activeMines.Count - 1; index >= 0; index--)
            {
                PlayerLandmine mine = activeMines[index];
                if (mine == null) continue;
                mine.Removed -= HandleMineRemoved;
                Destroy(mine.gameObject);
            }
            activeMines.Clear();
            placementSequence = 0UL;
            ownedHealthPotions = 0;
            ownedManaPotions = 0;
            // Oblivion Disk is permanent equipment. A run reset clears placed
            // nodes but restores the earned capacity rather than deleting it.
            storedDiskCharges = diskCapacity;
            nextDiskChargeAt = 0f;
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.HealthPotion, 0);
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.ManaPotion, 0);
            NotifyDiskStateChanged();
        }

        private void LoadDiskCapacity()
        {
            diskCapacity = Mathf.Clamp(PlayerPrefs.GetInt(DiskCapacityKey, 0), 0, MaximumDiskCapacity);
            storedDiskCharges = diskCapacity;
            nextDiskChargeAt = 0f;
        }

        private void UpdateDiskRecharge()
        {
            if (!IsDiskRecharging || Time.time < nextDiskChargeAt)
            {
                return;
            }

            storedDiskCharges = Mathf.Min(diskCapacity, storedDiskCharges + 1);
            nextDiskChargeAt = storedDiskCharges < diskCapacity
                ? Time.time + rechargeSecondsPerCharge
                : 0f;
            NotifyDiskStateChanged();
        }

        private void NotifyDiskStateChanged()
        {
            InventoryChanged?.Invoke(storedDiskCharges);
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.Landmine, storedDiskCharges);
            DiskChargeStateChanged?.Invoke();
        }
    }
}
