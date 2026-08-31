using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Progression;
using Cave.Pickups;
using Cave.UI;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    public enum SwarmComposition
    {
        GroundOnly,
        AirOnly,
        Mixed
    }

    [DisallowMultipleComponent]
    public sealed class SwarmCaller : MonoBehaviour, IEnemySkillEvolutionReceiver, IEnemyInterruptible
    {
        [SerializeField] private GameObject swarmPrefab;
        [SerializeField] private GameObject groundSwarmPrefab;
        [SerializeField] private GameObject airSwarmPrefab;
        [SerializeField] private SwarmComposition composition = SwarmComposition.Mixed;
        [SerializeField] private bool useSharedSettings = true;
        [SerializeField] private StrategicCombatSettings sharedSettings;
        [SerializeField] private bool requireDifficultyEligibility = true;

        [Header("Summon Overrides")]
        [SerializeField, Min(1)] private int spawnCount = 3;
        [SerializeField, Min(0f)] private float spawnInterval = 0.25f;
        [SerializeField, Min(1)] private int maximumActiveSummons = 6;
        [SerializeField, Min(0.1f)] private float summonCooldown = 10f;
        [SerializeField, Min(0f)] private float summonWindup = 0.35f;
        [SerializeField, Min(0f)] private float spawnRadius = 1.5f;
        [SerializeField, Min(0f)] private float minimumSpawnSeparation = 0.5f;
        [SerializeField, Min(0f)] private float airSpawnHeight = 2f;
        [SerializeField] private LayerMask groundLayers;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 4f;
        [SerializeField] private Color summonTelegraphColor = new Color(1f, 0.55f, 0.15f, 0.85f);

        [Header("Skill Evolution")]
        [SerializeField, Min(0)] private int evolutionOneAdditionalSummons = 1;
        [SerializeField, Range(0.2f, 1f)] private float evolutionOneCooldownMultiplier = 0.85f;
        [SerializeField] private bool airSummonsRequireEvolutionTwo = true;
        [SerializeField, Range(1f, 2f)] private float evolutionTwoTelegraphScale = 1.25f;

        [Header("Necromancer Summon Burst")]
        [SerializeField, Min(1)] private int minimumSummonBurst = 3;
        [SerializeField, Min(1)] private int maximumSummonBurst = 5;

        private readonly HashSet<Damageable> activeSummons = new HashSet<Damageable>();
        private readonly List<Damageable> staleSummons = new List<Damageable>();
        private WorldDifficultyManager difficultyManager;
        private bool isSummoning;
        private float nextSummonTime;
        private LineRenderer telegraph;
        private Material telegraphMaterial;
        private int summonSequence;
        private EnemyEvolutionStage evolutionStage;
        private bool brainControlled;
        private bool useNecromancerBaseOverrides;
        private bool summonWizardEyes;
        private EncounterGroup skeletonEncounterGroup;
        private int necromancerMaximumActive = 7;
        private int necromancerDesiredGenerals = 2;
        private int necromancerDesiredLessers = 5;
        private GameObject necromancerGeneralPrefab;
        private float necromancerInitialWindup = 0.75f;
        private float necromancerInitialDeploymentInterval = 0.35f;
        private float necromancerReplacementWindup = 0.5f;
        private float necromancerReplacementCooldown = 2f;
        private float necromancerSpawnRadius = 1.5f;
        private float necromancerMinimumSeparation = 0.75f;
        private Coroutine summonRoutine;
        private int formationSequence;

        [Header("Necromancer Formation (Read Only)")]
        [SerializeField] private bool initialDeploymentActive;
        [SerializeField] private bool initialDeploymentComplete;
        [SerializeField, Min(0)] private int activeGeneralCount;
        [SerializeField, Min(0)] private int activeLesserCount;
        [SerializeField, Min(0f)] private float nextReplacementCooldown;
        [SerializeField] private SkeletonInheritance strongestGeneral;
        [SerializeField, Min(0)] private int strongestGeneralWitnessedDeaths;
        [SerializeField] private SkeletonInheritance anchorGeneral;
        [SerializeField] private SkeletonInheritance assaultGeneral;
        [SerializeField, Min(0f)] private float anchorDistanceToNecromancer;
        [SerializeField, Min(0f)] private float assaultDistanceToLesserFormation;
        [SerializeField] private Damageable currentBestHealTarget;
        [SerializeField] private float currentBestHealTargetScore;

        private int SpawnCount
        {
            get
            {
                int baseCount = useSharedSettings && sharedSettings != null
                    ? sharedSettings.SwarmSpawnCount
                    : spawnCount;
                return evolutionStage >= EnemyEvolutionStage.EvolutionOne
                    ? baseCount + evolutionOneAdditionalSummons
                    : baseCount;
            }
        }
        private float SpawnInterval => useNecromancerBaseOverrides
            ? necromancerInitialDeploymentInterval
            : useSharedSettings && sharedSettings != null
                ? sharedSettings.SwarmSpawnInterval
                : spawnInterval;
        private int MaximumActive => useNecromancerBaseOverrides
            ? necromancerMaximumActive
            : useSharedSettings && sharedSettings != null
                ? sharedSettings.MaximumActiveSummons
                : maximumActiveSummons;
        private int ActiveLimit => useNecromancerBaseOverrides
            ? DesiredFormationTotal
            : MaximumActive;
        private float Cooldown
        {
            get
            {
                if (useNecromancerBaseOverrides)
                {
                    return necromancerReplacementCooldown;
                }

                float baseCooldown = useSharedSettings && sharedSettings != null
                    ? sharedSettings.SummonCooldown
                    : summonCooldown;
                return evolutionStage >= EnemyEvolutionStage.EvolutionOne
                    ? baseCooldown * evolutionOneCooldownMultiplier
                    : baseCooldown;
            }
        }
        private float SpawnRadius => useNecromancerBaseOverrides
            ? necromancerSpawnRadius
            : useSharedSettings && sharedSettings != null
                ? sharedSettings.SummonSpawnRadius
                : spawnRadius;
        private float SummonWindup => useNecromancerBaseOverrides
            ? initialDeploymentComplete
                ? necromancerReplacementWindup
                : necromancerInitialWindup
            : summonWindup;
        private float MinimumSpawnSeparation => useNecromancerBaseOverrides
            ? necromancerMinimumSeparation
            : minimumSpawnSeparation;

        public int ActiveSummonCount => activeSummons.Count;
        public int ActiveGeneralCount => activeGeneralCount;
        public int ActiveLesserCount => activeLesserCount;
        public int DesiredFormationTotal => useNecromancerBaseOverrides
            ? Mathf.Min(
                necromancerMaximumActive,
                necromancerDesiredGenerals + necromancerDesiredLessers)
            : MaximumActive;
        public bool IsInitialDeploymentActive => initialDeploymentActive;
        public bool IsInitialDeploymentComplete => initialDeploymentComplete;
        public float NextSummonCooldownRemaining => Mathf.Max(0f, nextSummonTime - Time.time);
        public SkeletonInheritance StrongestGeneral => strongestGeneral;
        public bool IsSummoning => isSummoning;
        public bool ShouldPrioritizeSummon => isSummoning
            || (HasAvailablePrefab()
                && Time.time >= nextSummonTime
                && activeSummons.Count < ActiveLimit
                && IsEligible());
        public bool IsReady => !isSummoning
            && HasAvailablePrefab()
            && Time.time >= nextSummonTime
            && activeSummons.Count < ActiveLimit
            && IsEligible();

        internal Damageable FindBestOwnedSkeletonForHeal(
            float radius,
            float generalRankBonus,
            float witnessedDeathWeight,
            float resolvedStrengthWeight,
            float generalEmergencyHealthFraction,
            float generalEmergencyBonus,
            float anchorPreservationBonus,
            float assaultCombatBonus,
            out float bestScore)
        {
            RemoveInactiveSummons();
            float maximumDistanceSquared = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            Damageable selected = null;
            float selectedScore = float.NegativeInfinity;
            float selectedDistanceSquared = float.PositiveInfinity;
            foreach (Damageable summon in activeSummons)
            {
                if (summon == null
                    || summon.CurrentHealth <= 0
                    || summon.CurrentHealth >= summon.MaximumHealth
                    || !summon.gameObject.activeInHierarchy)
                {
                    continue;
                }

                SkeletonInheritance inheritance = summon.GetComponent<SkeletonInheritance>();
                if (inheritance == null || inheritance.Summoner != gameObject)
                {
                    continue;
                }

                float distanceSquared = ((Vector2)summon.transform.position
                    - (Vector2)transform.position).sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared)
                {
                    continue;
                }

                float healthFraction = summon.CurrentHealth
                    / (float)Mathf.Max(1, summon.MaximumHealth);
                float injurySeverity = 1f - healthFraction;
                float strategicValue = inheritance.CalculateSupportValue(
                    generalRankBonus,
                    witnessedDeathWeight,
                    resolvedStrengthWeight);
                if (inheritance.GeneralRole == SkeletonGeneralRole.Anchor)
                {
                    strategicValue += Mathf.Max(0f, anchorPreservationBonus);
                }
                else if (inheritance.GeneralRole == SkeletonGeneralRole.Assault)
                {
                    strategicValue += Mathf.Max(0f, assaultCombatBonus);
                }

                float emergencyBonus = inheritance.Rank == SkeletonRank.General
                    && healthFraction <= generalEmergencyHealthFraction
                        ? generalEmergencyBonus + strategicValue * 0.5f
                        : 0f;
                float score = injurySeverity * strategicValue + emergencyBonus;
                if (score > selectedScore
                    || (Mathf.Approximately(score, selectedScore)
                        && distanceSquared < selectedDistanceSquared))
                {
                    selected = summon;
                    selectedScore = score;
                    selectedDistanceSquared = distanceSquared;
                }
            }

            currentBestHealTarget = selected;
            currentBestHealTargetScore = selected != null ? selectedScore : 0f;
            bestScore = currentBestHealTargetScore;
            return selected;
        }

        private GameObject GroundSwarmPrefab => useNecromancerBaseOverrides
            ? groundSwarmPrefab
            : groundSwarmPrefab != null
                ? groundSwarmPrefab
                : useSharedSettings && sharedSettings != null && sharedSettings.GroundSwarmPrefab != null
                    ? sharedSettings.GroundSwarmPrefab
                    : swarmPrefab;
        private GameObject AirSwarmPrefab => airSwarmPrefab != null
            ? airSwarmPrefab
            : useSharedSettings && sharedSettings != null
                ? sharedSettings.AirSwarmPrefab
                : null;
        private GameObject EffectiveAirSwarmPrefab => airSummonsRequireEvolutionTwo
            && evolutionStage < EnemyEvolutionStage.EvolutionTwo
                ? null
                : AirSwarmPrefab;

        private void Awake()
        {
            CreateTelegraph();
        }

        internal void ConfigureIfMissing(
            StrategicCombatSettings settings,
            WorldDifficultyManager worldDifficulty)
        {
            if (sharedSettings == null)
            {
                sharedSettings = settings;
            }

            difficultyManager = worldDifficulty;
            UpdateTelegraphRadius();
        }

        public void ConfigureWizardEyePair(
            StrategicCombatSettings settings,
            WorldDifficultyManager worldDifficulty,
            GameObject configuredEyePrefab,
            float configuredCooldown)
        {
            useNecromancerBaseOverrides = false;
            summonWizardEyes = true;
            useSharedSettings = false;
            sharedSettings = settings;
            difficultyManager = worldDifficulty;
            composition = SwarmComposition.AirOnly;
            airSwarmPrefab = configuredEyePrefab != null
                ? configuredEyePrefab
                : settings != null
                    ? settings.AirSwarmPrefab
                    : airSwarmPrefab;
            requireDifficultyEligibility = false;
            airSummonsRequireEvolutionTwo = false;
            spawnCount = 2;
            spawnInterval = 0f;
            maximumActiveSummons = 2;
            summonCooldown = Mathf.Clamp(configuredCooldown, 120f, 180f);
            summonWindup = 0f;
            evolutionOneAdditionalSummons = 0;
            brainControlled = true;
            UpdateTelegraphRadius();
        }

        public void ConfigureNecromancerFormation(
            GameObject skeletonPrefab,
            GameObject generalSkeletonPrefab,
            int maximumSummons,
            int desiredGenerals,
            int desiredLessers,
            float initialWindup,
            float initialDeploymentInterval,
            float replacementWindup,
            float replacementCooldown,
            float radius,
            float spawnSeparation)
        {
            useNecromancerBaseOverrides = true;
            summonWizardEyes = false;
            composition = SwarmComposition.GroundOnly;
            groundSwarmPrefab = skeletonPrefab;
            necromancerGeneralPrefab = generalSkeletonPrefab != null
                ? generalSkeletonPrefab
                : skeletonPrefab;

            necromancerMaximumActive = Mathf.Max(1, maximumSummons);
            necromancerDesiredGenerals = Mathf.Clamp(
                desiredGenerals,
                0,
                necromancerMaximumActive);
            necromancerDesiredLessers = Mathf.Clamp(
                desiredLessers,
                0,
                necromancerMaximumActive - necromancerDesiredGenerals);
            necromancerInitialWindup = Mathf.Max(0f, initialWindup);
            necromancerInitialDeploymentInterval = Mathf.Max(0f, initialDeploymentInterval);
            necromancerReplacementWindup = Mathf.Max(0f, replacementWindup);
            necromancerReplacementCooldown = Mathf.Max(0.1f, replacementCooldown);
            necromancerSpawnRadius = Mathf.Max(0f, radius);
            necromancerMinimumSeparation = Mathf.Clamp(
                spawnSeparation,
                0f,
                necromancerSpawnRadius);
            if (groundLayers.value == 0)
            {
                groundLayers = LayerMask.GetMask("Ground");
            }

            ResolveSkeletonEncounterGroup(true)?.ConfigureSkeletonFormation(
                necromancerDesiredGenerals);
            initialDeploymentComplete = activeSummons.Count >= DesiredFormationTotal;
            RefreshFormationDebug();
            UpdateTelegraphRadius();
        }

        private void Update()
        {
            RemoveInactiveSummons();
            RefreshFormationDebug();
            if (brainControlled)
            {
                return;
            }

            TrySummon();
        }

        public bool TrySummon()
        {
            RemoveInactiveSummons();
            if (!IsReady)
            {
                return false;
            }

            isSummoning = true;
            summonRoutine = StartCoroutine(SummonWave());
            return true;
        }

        public bool InterruptInitialDeployment()
        {
            if (!initialDeploymentActive || summonRoutine == null)
            {
                return false;
            }

            StopCoroutine(summonRoutine);
            summonRoutine = null;
            isSummoning = false;
            initialDeploymentActive = false;
            if (telegraph != null)
            {
                telegraph.enabled = false;
            }

            nextSummonTime = Time.time + necromancerInitialDeploymentInterval;
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        public void Interrupt()
        {
            if (summonRoutine == null)
            {
                return;
            }

            StopCoroutine(summonRoutine);
            summonRoutine = null;
            isSummoning = false;
            initialDeploymentActive = false;
            if (telegraph != null)
            {
                telegraph.enabled = false;
            }

            nextSummonTime = Mathf.Max(nextSummonTime, Time.time + 1.5f);
        }

        private bool IsEligible()
        {
            return useNecromancerBaseOverrides
                || !requireDifficultyEligibility
                || difficultyManager == null
                || sharedSettings == null
                || difficultyManager.DifficultyTier >= sharedSettings.SwarmMinimumTier;
        }

        private IEnumerator SummonWave()
        {
            bool rapidInitialDeployment = useNecromancerBaseOverrides
                && !initialDeploymentComplete;
            initialDeploymentActive = rapidInitialDeployment;
            telegraph.enabled = true;
            if (evolutionStage == EnemyEvolutionStage.EvolutionTwo)
            {
                Cave.Combat.AreaPulseEffect.Create(
                    transform.position,
                    SpawnRadius * evolutionTwoTelegraphScale,
                    summonTelegraphColor,
                    0.35f);
            }
            yield return new WaitForSeconds(SummonWindup);

            int availableSlots = Mathf.Max(0, ActiveLimit - activeSummons.Count);
            int count = ResolveSummonCount(availableSlots);
            for (int index = 0; index < count; index++)
            {
                if (!SpawnOne())
                {
                    break;
                }

                if (SpawnInterval > 0f && index + 1 < count)
                {
                    yield return new WaitForSeconds(SpawnInterval);
                }
            }

            telegraph.enabled = false;
            isSummoning = false;
            initialDeploymentActive = false;
            bool formationFilled = activeSummons.Count >= DesiredFormationTotal;
            initialDeploymentComplete = initialDeploymentComplete || formationFilled;
            nextSummonTime = Time.time + (useNecromancerBaseOverrides && !formationFilled
                ? necromancerInitialDeploymentInterval
                : Cooldown);
            summonRoutine = null;
            RefreshFormationDebug();
        }

        private int ResolveSummonCount(int availableSlots)
        {
            if (availableSlots <= 0)
            {
                return 0;
            }

            if (!useNecromancerBaseOverrides)
            {
                return Mathf.Min(SpawnCount, availableSlots);
            }

            int minimum = Mathf.Max(1, minimumSummonBurst);
            int maximum = Mathf.Max(minimum, maximumSummonBurst);
            int rolledBurst = Random.Range(minimum, maximum + 1);
            return Mathf.Min(rolledBurst, availableSlots);
        }

        private bool SpawnOne()
        {
            bool isAirSwarm;
            SkeletonRank intendedSkeletonRank = SkeletonRank.Lesser;
            EncounterGroup intendedSkeletonGroup = null;
            GameObject selectedPrefab;

            if (useNecromancerBaseOverrides && composition == SwarmComposition.GroundOnly)
            {
                isAirSwarm = false;
                intendedSkeletonGroup = ResolveSkeletonEncounterGroup(true);
                intendedSkeletonGroup?.EnsureGeneralSlots();

                intendedSkeletonRank = intendedSkeletonGroup != null
                    && intendedSkeletonGroup.ActiveSkeletonGenerals < necromancerDesiredGenerals
                        ? SkeletonRank.General
                        : SkeletonRank.Lesser;

                selectedPrefab = intendedSkeletonRank == SkeletonRank.General
                    && necromancerGeneralPrefab != null
                        ? necromancerGeneralPrefab
                        : GroundSwarmPrefab;
            }
            else
            {
                selectedPrefab = SelectPrefab(out isAirSwarm);
            }

            if (selectedPrefab == null)
            {
                return false;
            }

            Vector2 spawnPosition = FindSeparatedSpawnPosition(isAirSwarm);

            GameObject spawned = Instantiate(
                selectedPrefab,
                spawnPosition,
                Quaternion.identity);
            Damageable damageable = RealizeSwarmCombatant(spawned, isAirSwarm);
            if (damageable == null)
            {
                Destroy(spawned);
                return false;
            }

            EnemyArchetypeProfile profile = spawned.GetComponent<EnemyArchetypeProfile>();
            if (profile == null)
            {
                profile = spawned.AddComponent<EnemyArchetypeProfile>();
            }

            profile.AddRuntimeArchetype(
                EnemyArchetype.Swarm
                | (isAirSwarm ? EnemyArchetype.Ranged : EnemyArchetype.Melee));
            EnemySwarm swarm = spawned.GetComponent<EnemySwarm>();
            if (swarm == null)
            {
                swarm = spawned.AddComponent<EnemySwarm>();
            }

            swarm.ConfigureIfMissing(sharedSettings);
            if (!isAirSwarm)
            {
                SkeletonInheritance inheritance = spawned.GetComponent<SkeletonInheritance>();
                EncounterGroup group = intendedSkeletonGroup ?? ResolveSkeletonEncounterGroup(true);
                group?.EnsureGeneralSlots();

                SkeletonRank rank = useNecromancerBaseOverrides
                    ? intendedSkeletonRank
                    : group != null
                        && group.ActiveSkeletonGenerals < necromancerDesiredGenerals
                            ? SkeletonRank.General
                            : SkeletonRank.Lesser;

                inheritance?.AssignSummoner(
                    gameObject,
                    group,
                    rank,
                    ++formationSequence);
            }

            if (spawned.GetComponent<EnemyStatusEffects>() == null)
            {
                spawned.AddComponent<EnemyStatusEffects>();
            }

            if (spawned.GetComponent<EnemyStagger>() == null)
            {
                spawned.AddComponent<EnemyStagger>();
            }

            if (difficultyManager != null)
            {
                EnemyDifficultyScaler scaler = spawned.GetComponent<EnemyDifficultyScaler>();
                if (scaler == null)
                {
                    scaler = spawned.AddComponent<EnemyDifficultyScaler>();
                }

                scaler.Configure(difficultyManager);
            }

            activeSummons.Add(damageable);
            RefreshFormationDebug();
            damageable.Died += () =>
            {
                if (activeSummons.Remove(damageable))
                {
                    nextSummonTime = Mathf.Max(nextSummonTime, Time.time + Cooldown);
                    ResolveSkeletonEncounterGroup(false)?.EnsureGeneralSlots();
                    RefreshFormationDebug();
                }

                if (damageable != null)
                {
                    Destroy(damageable.gameObject);
                }
            };
            return true;
        }

        private Vector2 FindSeparatedSpawnPosition(bool isAirSwarm)
        {
            const int placementAttempts = 6;
            Vector2 fallback = transform.position;
            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                Vector2 offsetDirection = Random.insideUnitCircle;
                if (offsetDirection.sqrMagnitude <= 0.001f)
                {
                    offsetDirection = Vector2.right;
                }

                float offsetDistance = Random.Range(MinimumSpawnSeparation, SpawnRadius);
                Vector2 offset = offsetDirection.normalized * offsetDistance;
                if (isAirSwarm)
                {
                    offset.y = Mathf.Abs(offset.y) + airSpawnHeight;
                }

                Vector2 candidate = (Vector2)transform.position + offset;
                if (!isAirSwarm)
                {
                    candidate = ResolveGroundSpawnPosition(candidate);
                }

                fallback = candidate;
                if (IsSeparatedFromActiveSummons(candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private bool IsSeparatedFromActiveSummons(Vector2 candidate)
        {
            float requiredSeparationSquared = MinimumSpawnSeparation * MinimumSpawnSeparation;
            foreach (Damageable activeSummon in activeSummons)
            {
                if (activeSummon != null
                    && ((Vector2)activeSummon.transform.position - candidate).sqrMagnitude
                        < requiredSeparationSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector2 ResolveGroundSpawnPosition(Vector2 candidate)
        {
            if (groundLayers.value == 0)
            {
                return candidate;
            }

            RaycastHit2D hit = Physics2D.Raycast(
                candidate + Vector2.up * groundProbeHeight,
                Vector2.down,
                groundProbeDistance,
                groundLayers);
            if (hit.collider != null)
            {
                return hit.point + Vector2.up * 0.05f;
            }

            RaycastHit2D casterGround = Physics2D.Raycast(
                (Vector2)transform.position + Vector2.up * groundProbeHeight,
                Vector2.down,
                groundProbeDistance,
                groundLayers);
            return casterGround.collider != null
                ? casterGround.point + Vector2.up * 0.05f
                : (Vector2)transform.position;
        }

        private bool HasAvailablePrefab()
        {
            switch (composition)
            {
                case SwarmComposition.GroundOnly:
                    return GroundSwarmPrefab != null;
                case SwarmComposition.AirOnly:
                    return EffectiveAirSwarmPrefab != null;
                default:
                    return GroundSwarmPrefab != null || EffectiveAirSwarmPrefab != null;
            }
        }

        private GameObject SelectPrefab(out bool isAirSwarm)
        {
            GameObject ground = GroundSwarmPrefab;
            GameObject air = EffectiveAirSwarmPrefab;
            if (composition == SwarmComposition.AirOnly)
            {
                isAirSwarm = true;
                return air;
            }

            if (composition == SwarmComposition.GroundOnly)
            {
                isAirSwarm = false;
                return ground;
            }

            bool chooseAir = air != null && (ground == null || summonSequence++ % 2 == 1);
            isAirSwarm = chooseAir;
            return chooseAir ? air : ground;
        }

        public void ApplyEvolution(EnemyEvolutionStage stage)
        {
            evolutionStage = stage;
            UpdateTelegraphRadius();
        }

        private Damageable RealizeSwarmCombatant(GameObject spawned, bool isAirSwarm)
        {
            if (spawned == null)
            {
                return null;
            }

            int damageableLayer = LayerMask.NameToLayer("Damageable");
            if (damageableLayer >= 0)
            {
                spawned.layer = damageableLayer;
            }

            Rigidbody2D body = spawned.GetComponent<Rigidbody2D>();
            bool bodyWasAdded = body == null;
            if (bodyWasAdded)
            {
                body = spawned.AddComponent<Rigidbody2D>();
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            body.freezeRotation = true;
            if (bodyWasAdded)
            {
                body.gravityScale = isAirSwarm ? 0f : 3f;
            }

            Collider2D combatCollider = spawned.GetComponentInChildren<Collider2D>();
            if (combatCollider == null)
            {
                BoxCollider2D boxCollider = spawned.AddComponent<BoxCollider2D>();
                SpriteRenderer spriteRenderer = spawned.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    Bounds spriteBounds = spriteRenderer.sprite.bounds;
                    boxCollider.offset = spriteBounds.center;
                    boxCollider.size = Vector2.Scale(
                        spriteBounds.size,
                        isAirSwarm ? new Vector2(0.7f, 0.7f) : new Vector2(0.65f, 0.9f));
                }

                boxCollider.isTrigger = isAirSwarm;
                combatCollider = boxCollider;
            }

            Damageable damageable = spawned.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = spawned.AddComponent<Damageable>();
            }

            if (spawned.GetComponent<EnemyArchetypeProfile>() == null)
            {
                spawned.AddComponent<EnemyArchetypeProfile>();
            }

            if (spawned.GetComponent<KnockbackReceiver>() == null)
            {
                spawned.AddComponent<KnockbackReceiver>();
            }

            if (spawned.GetComponent<EnemyDamageModifiers>() == null)
            {
                spawned.AddComponent<EnemyDamageModifiers>();
            }

            EnemyDropper dropper = spawned.GetComponent<EnemyDropper>();
            if (dropper == null)
            {
                dropper = spawned.AddComponent<EnemyDropper>();
            }

            dropper.UseSharedSettings(
                Resources.Load<EnemyDropSettings>("EnemyDropSettings"));
            dropper.ConfigureDifficulty(difficultyManager);

            if (isAirSwarm)
            {
                FlyingSwarmController flying = spawned.GetComponent<FlyingSwarmController>();
                if (flying == null)
                {
                    flying = spawned.AddComponent<FlyingSwarmController>();
                }

                flying.Configure(useSharedSettings ? sharedSettings : null);
                if (summonWizardEyes
                    && spawned.GetComponent<EyeBrain>() == null)
                {
                    // Wizard's evolved AirOnly caller owns the Eye pair. The
                    // authored prefab stays untouched; the additive brain replaces
                    // only its generic swarm movement at runtime.
                    spawned.AddComponent<EyeBrain>();
                }
            }
            else
            {
                if (spawned.GetComponent<EnemyController>() == null)
                {
                    spawned.AddComponent<EnemyController>();
                }

                if (spawned.GetComponent<EnemyMeleeCombat>() == null)
                {
                    spawned.AddComponent<EnemyMeleeCombat>();
                }

                if (spawned.GetComponent<SkeletonInheritance>() == null)
                {
                    spawned.AddComponent<SkeletonInheritance>();
                }

                if (spawned.GetComponent<GeneralShardReward>() == null)
                {
                    spawned.AddComponent<GeneralShardReward>();
                }

                if (spawned.GetComponent<GeneralExperienceIndicator>() == null)
                {
                    spawned.AddComponent<GeneralExperienceIndicator>();
                }

                if (spawned.GetComponent<SkeletonBrain>() == null)
                {
                    spawned.AddComponent<SkeletonBrain>();
                }
            }

            return damageable;
        }

        private EncounterGroup ResolveSkeletonEncounterGroup(bool createIfMissing)
        {
            if (skeletonEncounterGroup == null)
            {
                skeletonEncounterGroup = GetComponent<EncounterGroup>();
                if (skeletonEncounterGroup == null && createIfMissing)
                {
                    skeletonEncounterGroup = gameObject.AddComponent<EncounterGroup>();
                }
            }

            return skeletonEncounterGroup;
        }

        private void RemoveInactiveSummons()
        {
            staleSummons.Clear();
            foreach (Damageable summon in activeSummons)
            {
                if (summon == null || !summon.gameObject.activeInHierarchy)
                {
                    staleSummons.Add(summon);
                }
            }

            foreach (Damageable stale in staleSummons)
            {
                if (activeSummons.Remove(stale))
                {
                    nextSummonTime = Mathf.Max(nextSummonTime, Time.time + Cooldown);
                }
            }

            if (staleSummons.Count > 0)
            {
                ResolveSkeletonEncounterGroup(false)?.EnsureGeneralSlots();
                RefreshFormationDebug();
            }
        }

        private void RefreshFormationDebug()
        {
            EncounterGroup group = ResolveSkeletonEncounterGroup(false);
            activeGeneralCount = group != null ? group.ActiveSkeletonGenerals : 0;
            activeLesserCount = group != null ? group.ActiveLesserSkeletons : 0;
            nextReplacementCooldown = NextSummonCooldownRemaining;
            strongestGeneral = group != null ? group.StrongestGeneral : null;
            strongestGeneralWitnessedDeaths = strongestGeneral != null
                ? strongestGeneral.WitnessedDeaths
                : 0;
            anchorGeneral = group != null ? group.AnchorGeneral : null;
            assaultGeneral = group != null ? group.AssaultGeneral : null;
            anchorDistanceToNecromancer = anchorGeneral != null
                ? Vector2.Distance(anchorGeneral.transform.position, transform.position)
                : 0f;
            assaultDistanceToLesserFormation = 0f;
            if (group != null && assaultGeneral != null)
            {
                group.TryGetLesserFormationReference(
                    assaultGeneral.transform.position,
                    1f,
                    out _,
                    out assaultDistanceToLesserFormation);
            }
        }

        private void CreateTelegraph()
        {
            GameObject visual = new GameObject("Swarm Summon Telegraph");
            visual.transform.SetParent(transform, false);
            telegraph = visual.AddComponent<LineRenderer>();
            telegraphMaterial = new Material(Shader.Find("Sprites/Default"));
            telegraph.material = telegraphMaterial;
            telegraph.useWorldSpace = false;
            telegraph.loop = true;
            telegraph.positionCount = 32;
            telegraph.startWidth = 0.1f;
            telegraph.endWidth = 0.1f;
            telegraph.startColor = summonTelegraphColor;
            telegraph.endColor = summonTelegraphColor;
            telegraph.sortingOrder = 6;
            telegraph.enabled = false;
            UpdateTelegraphRadius();
        }

        private void UpdateTelegraphRadius()
        {
            if (telegraph == null)
            {
                return;
            }

            for (int index = 0; index < telegraph.positionCount; index++)
            {
                float angle = index / (float)telegraph.positionCount * Mathf.PI * 2f;
                telegraph.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * SpawnRadius);
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            summonRoutine = null;
            isSummoning = false;
            initialDeploymentActive = false;
            if (telegraph != null)
            {
                telegraph.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (telegraphMaterial != null)
            {
                Destroy(telegraphMaterial);
            }
        }
    }
}
