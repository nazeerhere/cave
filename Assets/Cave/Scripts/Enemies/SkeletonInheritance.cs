using System;
using Cave.Combat;
using UnityEngine;

namespace Cave.Enemies
{
    public enum SkeletonRank
    {
        Lesser,
        General
    }

    public enum SkeletonGeneralRole
    {
        None,
        Anchor,
        Assault
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class SkeletonInheritance : MonoBehaviour
    {
        [Header("Group Identity")]
        [SerializeField] private EncounterGroup encounterGroup;

        [Header("Summon Ownership (Read Only)")]
        [SerializeField] private GameObject summoner;
        [SerializeField] private SkeletonRank rank = SkeletonRank.Lesser;
        [SerializeField, Min(0)] private int formationSequence;
        [SerializeField] private SkeletonGeneralRole generalRole;

        [Header("Uncapped Growth Per Witnessed Death")]
        [SerializeField, Min(0f)] private float damageGrowthPerDeath = 0.10f;
        [SerializeField, Min(0f)] private float healthGrowthPerDeath = 0.06f;
        [SerializeField, Min(0f)] private float moveSpeedGrowthPerDeath = 0.02f;
        [SerializeField, Min(0f)] private float attackSpeedGrowthPerDeath = 0.03f;

        [Header("Learned Behavior Thresholds")]
        [SerializeField, Min(0)] private int blockUnlockDeaths = 1;
        [SerializeField, Min(0)] private int improvedBlockDeaths = 2;
        [SerializeField, Min(0)] private int guardBreakUnlockDeaths = 3;
        [SerializeField, Min(0)] private int improvedGuardBreakDeaths = 4;

        [Header("Learned Block Tuning")]
        [SerializeField, Range(0f, 1f)] private float learnedBlockChance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float improvedBlockChance = 0.4f;
        [SerializeField, Min(0.01f)] private float blockDuration = 0.18f;
        [SerializeField, Min(0f)] private float blockRecovery = 0.3f;
        [SerializeField, Min(0f)] private float blockCooldown = 0.9f;

        [Header("Current Inheritance (Read Only)")]
        [SerializeField, Min(0)] private int witnessedDeaths;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private float healthMultiplier = 1f;
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private float attackSpeedMultiplier = 1f;
        [SerializeField] private bool blockUnlocked;
        [SerializeField] private bool improvedBlockUnlocked;
        [SerializeField] private bool guardBreakUnlocked;
        [SerializeField] private bool improvedGuardBreakUnlocked;

        private Damageable damageable;
        private EnemyController movement;
        private EnemyMeleeCombat melee;
        private EnemyDefenseController defense;
        private EnemyDamageModifiers damageModifiers;
        private PhysicsMaterial2D runtimeBodyMaterial;
        private int worldScaledBaseMaximumHealth;
        private bool deathReported;

        public event Action InheritanceChanged;
        public event Action<SkeletonRank> RankChanged;
        public event Action<SkeletonGeneralRole> GeneralRoleChanged;

        public int WitnessedDeaths => witnessedDeaths;
        public GameObject Summoner => summoner;
        public SkeletonRank Rank => rank;
        public int FormationSequence => formationSequence;
        public SkeletonGeneralRole GeneralRole => generalRole;
        public EncounterGroup EncounterGroup => encounterGroup;
        public float DamageMultiplier => damageMultiplier;
        public float HealthMultiplier => healthMultiplier;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;
        public bool BlockUnlocked => blockUnlocked;
        public bool ImprovedBlockUnlocked => improvedBlockUnlocked;
        public bool GuardBreakUnlocked => guardBreakUnlocked;
        public bool ImprovedGuardBreakUnlocked => improvedGuardBreakUnlocked;
        public float ResolvedStrengthScore => damageMultiplier
            + healthMultiplier
            + moveSpeedMultiplier
            + attackSpeedMultiplier;
        public bool CanWitnessGroupDeath => isActiveAndEnabled
            && !deathReported
            && damageable != null
            && damageable.CurrentHealth > 0;

        public float CalculateSupportValue(
            float generalRankBonus,
            float witnessedDeathWeight,
            float resolvedStrengthWeight)
        {
            float inheritedStrength = Mathf.Max(0f, ResolvedStrengthScore - 4f);
            return 1f
                + (rank == SkeletonRank.General ? Mathf.Max(0f, generalRankBonus) : 0f)
                + witnessedDeaths * Mathf.Max(0f, witnessedDeathWeight)
                + inheritedStrength * Mathf.Max(0f, resolvedStrengthWeight);
        }

        private void Awake()
        {
            CacheDependencies();
            ConfigureBodyCollision();
            witnessedDeaths = 0;
            deathReported = false;
            worldScaledBaseMaximumHealth = damageable != null
                ? Mathf.Max(1, damageable.MaximumHealth)
                : 1;
            Recalculate(false);
        }

        private void Start()
        {
            CacheDependencies();
            ResolveConfiguredGroup();
            IgnoreAlliedActorCollisions();
            Recalculate(false);
        }

        private void OnEnable()
        {
            CacheDependencies();
            if (deathReported && damageable != null && damageable.CurrentHealth > 0)
            {
                ResetForNewLife();
            }

            SubscribeToDeath();
            ResolveConfiguredGroup();
            encounterGroup?.RegisterSkeleton(this);
            IgnoreAlliedActorCollisions();
            Recalculate(false);
        }

        internal void AssignEncounterGroup(EncounterGroup group)
        {
            if (encounterGroup == group)
            {
                encounterGroup?.RegisterSkeleton(this);
                return;
            }

            encounterGroup?.UnregisterSkeleton(this);
            encounterGroup = group;
            if (isActiveAndEnabled)
            {
                encounterGroup?.RegisterSkeleton(this);
            }
        }

        internal void AssignSummoner(
            GameObject owner,
            EncounterGroup group,
            SkeletonRank assignedRank,
            int assignedFormationSequence)
        {
            bool rankChanged = rank != assignedRank;
            summoner = owner;
            rank = assignedRank;
            generalRole = SkeletonGeneralRole.None;
            formationSequence = Mathf.Max(0, assignedFormationSequence);
            AssignEncounterGroup(group);
            ApplyRankToBrain();
            IgnoreAlliedActorCollisions();
            if (rankChanged)
            {
                RankChanged?.Invoke(rank);
            }
        }

        internal void SetRank(SkeletonRank newRank)
        {
            if (rank == newRank)
            {
                ApplyRankToBrain();
                return;
            }

            rank = newRank;
            if (rank != SkeletonRank.General)
            {
                SetGeneralRole(SkeletonGeneralRole.None);
            }

            ApplyRankToBrain();
            RankChanged?.Invoke(rank);
            encounterGroup?.NotifySkeletonRankChanged();
        }

        internal void SetGeneralRole(SkeletonGeneralRole role)
        {
            SkeletonGeneralRole resolved = rank == SkeletonRank.General
                ? role
                : SkeletonGeneralRole.None;
            if (generalRole == resolved)
            {
                ApplyRankToBrain();
                return;
            }

            generalRole = resolved;
            ApplyRankToBrain();
            GeneralRoleChanged?.Invoke(generalRole);
        }

        internal void ReceiveWitnessedDeath()
        {
            if (!CanWitnessGroupDeath)
            {
                return;
            }

            witnessedDeaths++;
            Recalculate(true);
            InheritanceChanged?.Invoke();
        }

        internal void SetWorldScaledBaseMaximumHealth(int maximumHealth, bool restoreToFull)
        {
            CacheDependencies();
            worldScaledBaseMaximumHealth = Mathf.Max(1, maximumHealth);
            int inheritedMaximum = ResolveInheritedMaximumHealth();
            if (restoreToFull)
            {
                damageable.SetRuntimeMaximumHealth(inheritedMaximum, true);
            }
            else
            {
                damageable.SetRuntimeMaximumHealthPreservingRatio(inheritedMaximum);
            }
        }

        internal void ResetForNewLife()
        {
            witnessedDeaths = 0;
            deathReported = false;
            Recalculate(false);
            InheritanceChanged?.Invoke();
        }

        private void HandleDied()
        {
            if (deathReported)
            {
                return;
            }

            deathReported = true;
            encounterGroup?.ReportSkeletonDeath(this);
        }

        private void Recalculate(bool preserveHealthFraction)
        {
            CacheDependencies();
            damageMultiplier = 1f + witnessedDeaths * damageGrowthPerDeath;
            healthMultiplier = 1f + witnessedDeaths * healthGrowthPerDeath;
            moveSpeedMultiplier = 1f + witnessedDeaths * moveSpeedGrowthPerDeath;
            attackSpeedMultiplier = 1f + witnessedDeaths * attackSpeedGrowthPerDeath;

            blockUnlocked = witnessedDeaths >= blockUnlockDeaths;
            improvedBlockUnlocked = witnessedDeaths >= improvedBlockDeaths;
            guardBreakUnlocked = witnessedDeaths >= guardBreakUnlockDeaths;
            improvedGuardBreakUnlocked = witnessedDeaths >= improvedGuardBreakDeaths;

            if (defense == null && gameObject.activeInHierarchy)
            {
                defense = gameObject.AddComponent<EnemyDefenseController>();
            }

            movement?.SetInheritanceSpeedMultiplier(moveSpeedMultiplier);
            melee?.SetRuntimeInheritanceAttackSpeedMultiplier(attackSpeedMultiplier);
            melee?.ConfigureSkeletonGuardBreak(guardBreakUnlocked);
            damageModifiers?.SetInheritanceDamageMultiplier(damageMultiplier);

            float blockChance = blockUnlocked
                ? improvedBlockUnlocked ? improvedBlockChance : learnedBlockChance
                : 0f;
            defense?.ConfigureSkeletonBlock(
                blockChance,
                blockDuration,
                blockRecovery,
                blockCooldown);
            ApplyRankToBrain();

            if (damageable == null || worldScaledBaseMaximumHealth <= 0)
            {
                return;
            }

            int inheritedMaximum = ResolveInheritedMaximumHealth();
            if (preserveHealthFraction)
            {
                damageable.SetRuntimeMaximumHealthPreservingRatio(inheritedMaximum);
            }
            else if (damageable.MaximumHealth != inheritedMaximum)
            {
                damageable.SetRuntimeMaximumHealth(inheritedMaximum, false);
            }
        }

        private int ResolveInheritedMaximumHealth()
        {
            return Mathf.Max(
                1,
                Mathf.RoundToInt(worldScaledBaseMaximumHealth * healthMultiplier));
        }

        private void CacheDependencies()
        {
            if (damageable == null)
            {
                damageable = GetComponent<Damageable>();
            }

            if (movement == null)
            {
                movement = GetComponent<EnemyController>();
            }

            if (melee == null)
            {
                melee = GetComponent<EnemyMeleeCombat>();
            }

            if (defense == null)
            {
                defense = GetComponent<EnemyDefenseController>();
            }

            if (damageModifiers == null)
            {
                damageModifiers = GetComponent<EnemyDamageModifiers>();
                if (damageModifiers == null && gameObject.activeInHierarchy)
                {
                    damageModifiers = gameObject.AddComponent<EnemyDamageModifiers>();
                }
            }
        }

        private void ResolveConfiguredGroup()
        {
            if (encounterGroup == null)
            {
                encounterGroup = GetComponentInParent<EncounterGroup>();
            }
        }

        private void ApplyRankToBrain()
        {
            SkeletonBrain brain = GetComponent<SkeletonBrain>();
            brain?.ApplyRankAndRole(rank, generalRole);
        }

        private void SubscribeToDeath()
        {
            if (damageable == null)
            {
                return;
            }

            damageable.Died -= HandleDied;
            damageable.Died += HandleDied;
        }

        private void IgnoreAlliedActorCollisions()
        {
            Collider2D[] skeletonColliders = GetComponentsInChildren<Collider2D>(true);
            if (skeletonColliders.Length == 0)
            {
                return;
            }

            foreach (EnemyArchetypeProfile ally in FindObjectsOfType<EnemyArchetypeProfile>())
            {
                if (ally == null || ally.gameObject == gameObject)
                {
                    continue;
                }

                IgnoreColliderPairs(
                    skeletonColliders,
                    ally.GetComponentsInChildren<Collider2D>(true));
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            EnemyArchetypeProfile ally = collision.collider != null
                ? collision.collider.GetComponentInParent<EnemyArchetypeProfile>()
                : null;
            if (ally == null || ally.gameObject == gameObject)
            {
                return;
            }

            IgnoreColliderPairs(
                GetComponentsInChildren<Collider2D>(true),
                ally.GetComponentsInChildren<Collider2D>(true));
        }

        private void ConfigureBodyCollision()
        {
            foreach (Collider2D bodyCollider in GetComponentsInChildren<Collider2D>(true))
            {
                if (bodyCollider == null
                    || bodyCollider.isTrigger
                    || bodyCollider.sharedMaterial != null)
                {
                    continue;
                }

                if (runtimeBodyMaterial == null)
                {
                    runtimeBodyMaterial = new PhysicsMaterial2D("Skeleton Runtime No Friction")
                    {
                        friction = 0f,
                        bounciness = 0f
                    };
                }

                bodyCollider.sharedMaterial = runtimeBodyMaterial;
            }
        }

        private static void IgnoreColliderPairs(
            Collider2D[] skeletonColliders,
            Collider2D[] allyColliders)
        {
            foreach (Collider2D skeletonCollider in skeletonColliders)
            {
                if (skeletonCollider == null)
                {
                    continue;
                }

                foreach (Collider2D allyCollider in allyColliders)
                {
                    if (allyCollider != null && allyCollider != skeletonCollider)
                    {
                        Physics2D.IgnoreCollision(
                            skeletonCollider,
                            allyCollider,
                            true);
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (damageable != null)
            {
                damageable.Died -= HandleDied;
            }

            encounterGroup?.UnregisterSkeleton(this);
        }

        private void OnDestroy()
        {
            encounterGroup?.UnregisterSkeleton(this);
            if (runtimeBodyMaterial != null)
            {
                Destroy(runtimeBodyMaterial);
            }
        }

        private void OnValidate()
        {
            improvedBlockDeaths = Mathf.Max(blockUnlockDeaths, improvedBlockDeaths);
            guardBreakUnlockDeaths = Mathf.Max(improvedBlockDeaths, guardBreakUnlockDeaths);
            improvedGuardBreakDeaths = Mathf.Max(
                guardBreakUnlockDeaths,
                improvedGuardBreakDeaths);
        }
    }
}
