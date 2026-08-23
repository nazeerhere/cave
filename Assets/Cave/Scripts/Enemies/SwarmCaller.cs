using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Progression;
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
    public sealed class SwarmCaller : MonoBehaviour, IEnemySkillEvolutionReceiver
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
        [SerializeField, Min(0f)] private float spawnRadius = 1.5f;
        [SerializeField, Min(0f)] private float airSpawnHeight = 2f;
        [SerializeField] private Color summonTelegraphColor = new Color(1f, 0.55f, 0.15f, 0.85f);

        [Header("Skill Evolution")]
        [SerializeField, Min(0)] private int evolutionOneAdditionalSummons = 1;
        [SerializeField, Range(0.2f, 1f)] private float evolutionOneCooldownMultiplier = 0.85f;
        [SerializeField] private bool airSummonsRequireEvolutionTwo = true;
        [SerializeField, Range(1f, 2f)] private float evolutionTwoTelegraphScale = 1.25f;

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
        private float SpawnInterval => useSharedSettings && sharedSettings != null
            ? sharedSettings.SwarmSpawnInterval
            : spawnInterval;
        private int MaximumActive => useSharedSettings && sharedSettings != null
            ? sharedSettings.MaximumActiveSummons
            : maximumActiveSummons;
        private float Cooldown
        {
            get
            {
                float baseCooldown = useSharedSettings && sharedSettings != null
                    ? sharedSettings.SummonCooldown
                    : summonCooldown;
                return evolutionStage >= EnemyEvolutionStage.EvolutionOne
                    ? baseCooldown * evolutionOneCooldownMultiplier
                    : baseCooldown;
            }
        }
        private float SpawnRadius => useSharedSettings && sharedSettings != null
            ? sharedSettings.SummonSpawnRadius
            : spawnRadius;

        public int ActiveSummonCount => activeSummons.Count;
        public bool IsSummoning => isSummoning;
        public bool ShouldPrioritizeSummon => isSummoning
            || (HasAvailablePrefab()
                && Time.time >= nextSummonTime
                && activeSummons.Count < MaximumActive
                && IsEligible());
        public bool IsReady => !isSummoning
            && HasAvailablePrefab()
            && Time.time >= nextSummonTime
            && activeSummons.Count < MaximumActive
            && IsEligible();

        private GameObject GroundSwarmPrefab => groundSwarmPrefab != null
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

        private void Update()
        {
            RemoveInactiveSummons();
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

            StartCoroutine(SummonWave());
            return true;
        }

        public void SetBrainControlled(bool controlled)
        {
            brainControlled = controlled;
        }

        private bool IsEligible()
        {
            return !requireDifficultyEligibility
                || difficultyManager == null
                || sharedSettings == null
                || difficultyManager.DifficultyTier >= sharedSettings.SwarmMinimumTier;
        }

        private IEnumerator SummonWave()
        {
            isSummoning = true;
            telegraph.enabled = true;
            if (evolutionStage == EnemyEvolutionStage.EvolutionTwo)
            {
                Cave.Combat.AreaPulseEffect.Create(
                    transform.position,
                    SpawnRadius * evolutionTwoTelegraphScale,
                    summonTelegraphColor,
                    0.35f);
            }
            yield return new WaitForSeconds(0.35f);

            int availableSlots = Mathf.Max(0, MaximumActive - activeSummons.Count);
            int count = Mathf.Min(SpawnCount, availableSlots);
            for (int index = 0; index < count; index++)
            {
                SpawnOne();
                if (SpawnInterval > 0f && index + 1 < count)
                {
                    yield return new WaitForSeconds(SpawnInterval);
                }
            }

            telegraph.enabled = false;
            isSummoning = false;
            nextSummonTime = Time.time + Cooldown;
        }

        private void SpawnOne()
        {
            GameObject selectedPrefab = SelectPrefab(out bool isAirSwarm);
            if (selectedPrefab == null)
            {
                return;
            }

            Vector2 offset = Random.insideUnitCircle * SpawnRadius;
            if (isAirSwarm)
            {
                offset.y = Mathf.Abs(offset.y) + airSpawnHeight;
            }

            GameObject spawned = Instantiate(
                selectedPrefab,
                (Vector2)transform.position + offset,
                Quaternion.identity);
            Damageable damageable = RealizeSwarmCombatant(spawned, isAirSwarm);
            if (damageable == null)
            {
                Destroy(spawned);
                return;
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
            damageable.Died += () =>
            {
                activeSummons.Remove(damageable);
                if (damageable != null)
                {
                    Destroy(damageable.gameObject);
                }
            };
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

            if (isAirSwarm)
            {
                FlyingSwarmController flying = spawned.GetComponent<FlyingSwarmController>();
                if (flying == null)
                {
                    flying = spawned.AddComponent<FlyingSwarmController>();
                }

                flying.Configure(useSharedSettings ? sharedSettings : null);
            }
            else
            {
                if (spawned.GetComponent<EnemyController>() == null)
                {
                    spawned.AddComponent<EnemyController>();
                }

                if (spawned.GetComponentInChildren<EnemyContactDamage>(true) == null)
                {
                    spawned.AddComponent<EnemyContactDamage>();
                }
            }

            return damageable;
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
                activeSummons.Remove(stale);
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
            isSummoning = false;
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
