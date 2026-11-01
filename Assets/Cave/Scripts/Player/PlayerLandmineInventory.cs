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
        internal const float BalancedMineEnergy = 24f;
        internal const float BalancedMineLifetime = 216f;
        internal const float BalancedTriggerRadius = 1.95f;
        internal const float BalancedLocalPulseRadius = 3.6f;
        internal const float BalancedBlastRadius = 6.6f;
        internal const float BalancedMaximumLinkDistance = 18f;

        private const string DiskCapacityKey = "Cave.OblivionDisk.Capacity";
        private const int MaximumDiskCapacity = 3;

        [Header("Shop / Inventory")]
        [SerializeField, Min(0)] private int currencyCost = 18;
        [SerializeField, Min(0.1f)] private float rechargeSecondsPerCharge = 12f;
        [SerializeField, Min(0)] private int ownedHealthPotions;
        [SerializeField, Min(0)] private int ownedManaPotions;

        [Serializable]
        private struct PotionStack
        {
            [SerializeField, Min(1)] private int quantity;
            [SerializeField, Min(1)] private int potencyBasisPoints;
            [SerializeField, Min(0)] private long acquiredSequence;

            public PotionStack(int quantity, int potencyBasisPoints, long acquiredSequence)
            {
                this.quantity = Mathf.Max(1, quantity);
                this.potencyBasisPoints = Mathf.Max(1, potencyBasisPoints);
                this.acquiredSequence = Math.Max(0L, acquiredSequence);
            }

            public int Quantity => quantity;
            public int PotencyBasisPoints => potencyBasisPoints;
            public long AcquiredSequence => acquiredSequence;
            public float Potency => potencyBasisPoints / 10000f;
        }

        // Counts above remain serialized migration fields for existing player
        // instances. Mechanical ownership lives in these purchase snapshots.
        [SerializeField] private List<PotionStack> healthPotionStacks = new List<PotionStack>();
        [SerializeField] private List<PotionStack> manaPotionStacks = new List<PotionStack>();
        [SerializeField, Min(0)] private long nextPotionAcquisitionSequence;

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
        [SerializeField, Min(0.1f)] private float mineEnergy = BalancedMineEnergy;
        [SerializeField, Min(0f)] private float mineLifetime = BalancedMineLifetime;
        [SerializeField, Min(0.1f)] private float maximumLinkDistance = BalancedMaximumLinkDistance;
        [SerializeField, Min(0.01f)] private float fieldBaseStrength = 12f;
        [SerializeField, Min(0.01f)] private float fieldDistanceOffset = 1f;
        [SerializeField, Min(0.1f)] private float fieldDistanceExponent = 1.25f;

        [Header("Fallback Mine Tuning")]
        [SerializeField, Min(0f)] private float armingDelay = 0.65f;
        [SerializeField, Min(0.1f)] private float triggerRadius = BalancedTriggerRadius;
        [SerializeField, Min(0.1f)] private float localPulseRadius = BalancedLocalPulseRadius;
        [SerializeField, Min(0.1f)] private float blastRadius = BalancedBlastRadius;
        [SerializeField, Min(0)] private int damage = 2;
        [SerializeField, Min(0f)] private float knockback = 11f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        private PlayerCurrency currency;
        private PlayerHealth playerHealth;
        private PlayerMana playerMana;
        private PlayerResourceMastery mastery;
        private PlayerPermanentProgression progression;
        private Collider2D playerCollider;
        private FieldNetwork fieldNetwork;
        private readonly List<PlayerLandmine> activeMines = new List<PlayerLandmine>(3);
        private ulong placementSequence;
        private int diskCapacity;
        private int storedDiskCharges;
        private float nextDiskChargeAt;

        public event Action<int> InventoryChanged;
        public event Action<PlayerConsumableType, int> ConsumableQuantityChanged;
        public event Action DiskChargeStateChanged;
        /// <summary>Emitted after an ordinary disk has been fully configured and registered with its real FieldNetwork.</summary>
        public event Action<PlayerLandmine> LandminePlaced;

        public int CurrencyCost => NextDiskCapacityCost;
        public int OwnedHealthPotions => CountPotions(healthPotionStacks);
        public int OwnedManaPotions => CountPotions(manaPotionStacks);
        public float NextHealthPotionRestorePercent => ResolveNextPotionPotency(
            healthPotionStacks, healthPotionRestorePercent);
        public float NextManaPotionRestorePercent => ResolveNextPotionPotency(
            manaPotionStacks, manaPotionRestorePercent);
        public int OwnedLandmines => storedDiskCharges;
        public int DiskCapacity => diskCapacity;
        public int StoredDiskCharges => storedDiskCharges;
        public int NextDiskCapacityCost => diskCapacity >= MaximumDiskCapacity
            ? 0
            : currencyCost * (diskCapacity + 1);
        public float RechargeSecondsPerCharge => rechargeSecondsPerCharge;
        public float SacrificeHoldThreshold => sacrificeHoldThreshold;
        public bool IsDiskUnlocked => diskCapacity > 0;
        public bool IsDiskRecharging => diskCapacity > 0 && storedDiskCharges < diskCapacity;
        public float FieldLinkDistance => maximumLinkDistance;
        public FieldNetwork FieldNetwork => fieldNetwork;
        public IReadOnlyList<PlayerLandmine> ActiveMines => activeMines.AsReadOnly();
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
            progression = GetComponent<PlayerPermanentProgression>();
            playerCollider = GetComponent<Collider2D>();
            MigrateLegacyPotionCounts();
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

            UpdateDiskRecharge();
        }

        public int GetOwnedCount(PlayerConsumableType type)
        {
            switch (type)
            {
                case PlayerConsumableType.HealthPotion:
                    return OwnedHealthPotions;
                case PlayerConsumableType.ManaPotion:
                    return OwnedManaPotions;
                default:
                    return storedDiskCharges;
            }
        }

        public void AddHealthPotion()
        {
            AddHealthPotion(healthPotionRestorePercent);
        }

        public void AddHealthPotion(float potency)
        {
            AddPotion(healthPotionStacks, potency);
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.HealthPotion,
                OwnedHealthPotions);
        }

        public void AddManaPotion()
        {
            AddManaPotion(manaPotionRestorePercent);
        }

        public void AddManaPotion(float potency)
        {
            AddPotion(manaPotionStacks, potency);
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.ManaPotion,
                OwnedManaPotions);
        }

        public bool TryUseHealthPotion()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (OwnedHealthPotions <= 0 || playerHealth == null)
            {
                return false;
            }

            float potency;
            if (!TryPeekPotion(healthPotionStacks, healthPotionRestorePercent, out potency)) return false;

            int restoreAmount = Mathf.Max(
                1,
                Mathf.CeilToInt(playerHealth.MaxHealth * potency));
            if (!playerHealth.RestoreHealth(restoreAmount))
            {
                return false;
            }

            ConsumeNextPotion(healthPotionStacks);
            ResolveProgression()?.NotifyHealthPotionConsumed();
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.HealthPotion,
                OwnedHealthPotions);
            return true;
        }

        public bool TryUseManaPotion()
        {
            if (playerMana == null)
            {
                playerMana = GetComponent<PlayerMana>();
            }

            if (OwnedManaPotions <= 0 || playerMana == null)
            {
                return false;
            }

            float potency;
            if (!TryPeekPotion(manaPotionStacks, manaPotionRestorePercent, out potency)) return false;

            float restoreAmount = Mathf.Max(
                0.01f,
                playerMana.MaximumMana * potency);
            if (!playerMana.RestoreMana(restoreAmount))
            {
                return false;
            }

            ConsumeNextPotion(manaPotionStacks);
            ResolveProgression()?.NotifyManaPotionConsumed();
            ConsumableQuantityChanged?.Invoke(
                PlayerConsumableType.ManaPotion,
                OwnedManaPotions);
            return true;
        }

        public int GetNextDiskCapacityCost(int diskUpgradeRanks)
        {
            if (NextDiskCapacityCost <= 0)
            {
                return 0;
            }

            return ResolveDiskCapacityPrice(NextDiskCapacityCost, diskUpgradeRanks);
        }

        public static int ResolveDiskCapacityPrice(int originalPrice, int diskUpgradeRanks)
        {
            return Mathf.CeilToInt(Mathf.Max(0, originalPrice)
                * (1f + .25f * Mathf.Max(0, diskUpgradeRanks)));
        }

        public bool TryPurchaseLandmine()
        {
            return TryPurchaseLandmine(0);
        }

        public bool TryPurchaseLandmine(int diskUpgradeRanks)
        {
            if (diskCapacity >= MaximumDiskCapacity
                || currency == null
                || !currency.TrySpend(GetNextDiskCapacityCost(diskUpgradeRanks)))
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
                mine.ConfigureEffectiveRanges(
                    triggerRadius,
                    localPulseRadius,
                    blastRadius,
                    enemyLayers);
            }
            else
            {
                GameObject mineObject = new GameObject("Player Landmine");
                mineObject.transform.position = position;
                mine = mineObject.AddComponent<PlayerLandmine>();
                mine.ConfigureFallback(
                    armingDelay,
                    triggerRadius,
                    localPulseRadius,
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
            PlayerPermanentProgression permanentProgression = ResolveProgression();
            if (permanentProgression != null)
            {
                mine.ConfigureProgressionUpgrades(
                    permanentProgression.DiskDamageBonus,
                    permanentProgression.DiskAreaMultiplier,
                    permanentProgression.DiskPullStrengthMultiplier);
            }
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
            LandminePlaced?.Invoke(mine);
            storedDiskCharges--;
            if (storedDiskCharges < diskCapacity && nextDiskChargeAt <= 0f)
            {
                nextDiskChargeAt = Time.time + rechargeSecondsPerCharge;
            }
            NotifyDiskStateChanged();
            return true;
        }

        private PlayerPermanentProgression ResolveProgression()
        {
            if (progression == null)
            {
                progression = GetComponent<PlayerPermanentProgression>();
            }

            return progression;
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
            for (int index = activeMines.Count - 1; index >= 0; index--)
            {
                PlayerLandmine mine = activeMines[index];
                if (mine == null) continue;
                mine.Removed -= HandleMineRemoved;
                Destroy(mine.gameObject);
            }
            activeMines.Clear();
            placementSequence = 0UL;
            healthPotionStacks.Clear();
            manaPotionStacks.Clear();
            ownedHealthPotions = 0;
            ownedManaPotions = 0;
            // Capacity is permanent equipment, while its ready disks are
            // current-run inventory. Start a new run empty and let the
            // existing sequential recharge restore charges normally.
            storedDiskCharges = 0;
            nextDiskChargeAt = diskCapacity > 0
                ? Time.time + rechargeSecondsPerCharge
                : 0f;
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.HealthPotion, 0);
            ConsumableQuantityChanged?.Invoke(PlayerConsumableType.ManaPotion, 0);
            NotifyDiskStateChanged();
        }

        /// <summary>Read-only UI description of purchase-time potion stacks.</summary>
        public string DescribePotionStacks(PlayerConsumableType type)
        {
            List<PotionStack> source = type == PlayerConsumableType.HealthPotion
                ? healthPotionStacks
                : type == PlayerConsumableType.ManaPotion
                    ? manaPotionStacks
                    : null;
            if (source == null || source.Count == 0) return "—";

            List<PotionStack> ordered = new List<PotionStack>(source);
            ordered.Sort(ComparePotionStacks);
            string result = string.Empty;
            for (int index = 0; index < ordered.Count; index++)
            {
                PotionStack stack = ordered[index];
                if (stack.Quantity <= 0) continue;
                if (result.Length > 0) result += "  •  ";
                result += Mathf.RoundToInt(stack.Potency * 100f) + "% ×" + stack.Quantity;
            }
            return result.Length == 0 ? "—" : result;
        }

        private void LoadDiskCapacity()
        {
            diskCapacity = Mathf.Clamp(PlayerPrefs.GetInt(DiskCapacityKey, 0), 0, MaximumDiskCapacity);
            storedDiskCharges = diskCapacity;
            nextDiskChargeAt = 0f;
        }

        private void MigrateLegacyPotionCounts()
        {
            if (healthPotionStacks == null) healthPotionStacks = new List<PotionStack>();
            if (manaPotionStacks == null) manaPotionStacks = new List<PotionStack>();
            if (healthPotionStacks.Count == 0 && ownedHealthPotions > 0)
            {
                healthPotionStacks.Add(new PotionStack(
                    ownedHealthPotions,
                    ToBasisPoints(healthPotionRestorePercent),
                    nextPotionAcquisitionSequence++));
            }
            if (manaPotionStacks.Count == 0 && ownedManaPotions > 0)
            {
                manaPotionStacks.Add(new PotionStack(
                    ownedManaPotions,
                    ToBasisPoints(manaPotionRestorePercent),
                    nextPotionAcquisitionSequence++));
            }
            SyncLegacyPotionCounts();
        }

        private void AddPotion(List<PotionStack> stacks, float potency)
        {
            if (stacks == null) return;
            stacks.Add(new PotionStack(1, ToBasisPoints(potency), nextPotionAcquisitionSequence++));
            SyncLegacyPotionCounts();
        }

        private static int CountPotions(List<PotionStack> stacks)
        {
            if (stacks == null) return 0;
            int count = 0;
            for (int index = 0; index < stacks.Count; index++)
            {
                count += Mathf.Max(0, stacks[index].Quantity);
            }
            return count;
        }

        private static int ComparePotionStacks(PotionStack left, PotionStack right)
        {
            int potency = left.PotencyBasisPoints.CompareTo(right.PotencyBasisPoints);
            return potency != 0 ? potency : left.AcquiredSequence.CompareTo(right.AcquiredSequence);
        }

        private static int ToBasisPoints(float potency)
        {
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(potency) * 10000f), 1, 10000);
        }

        private static float ResolveNextPotionPotency(List<PotionStack> stacks, float fallback)
        {
            float potency;
            return TryPeekPotion(stacks, fallback, out potency) ? potency : Mathf.Clamp01(fallback);
        }

        private static bool TryPeekPotion(List<PotionStack> stacks, float fallback, out float potency)
        {
            potency = Mathf.Clamp01(fallback);
            if (stacks == null || stacks.Count == 0) return false;
            int selected = -1;
            for (int index = 0; index < stacks.Count; index++)
            {
                if (stacks[index].Quantity <= 0) continue;
                if (selected < 0 || ComparePotionStacks(stacks[index], stacks[selected]) < 0) selected = index;
            }
            if (selected < 0) return false;
            potency = stacks[selected].Potency;
            return true;
        }

        private void ConsumeNextPotion(List<PotionStack> stacks)
        {
            if (stacks == null) return;
            int selected = -1;
            for (int index = 0; index < stacks.Count; index++)
            {
                if (stacks[index].Quantity <= 0) continue;
                if (selected < 0 || ComparePotionStacks(stacks[index], stacks[selected]) < 0) selected = index;
            }
            if (selected < 0) return;
            PotionStack consumed = stacks[selected];
            if (consumed.Quantity <= 1) stacks.RemoveAt(selected);
            else stacks[selected] = new PotionStack(
                consumed.Quantity - 1,
                consumed.PotencyBasisPoints,
                consumed.AcquiredSequence);
            SyncLegacyPotionCounts();
        }

        private void SyncLegacyPotionCounts()
        {
            ownedHealthPotions = CountPotions(healthPotionStacks);
            ownedManaPotions = CountPotions(manaPotionStacks);
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
