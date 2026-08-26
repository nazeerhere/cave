using System.Collections;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    public sealed class NecromancerDetectiveSummoner : MonoBehaviour, IEnemyInterruptible
    {
        [Header("Specialist Summoning")]
        [SerializeField] private GameObject detectivePrefab;
        [SerializeField, Min(0)] private int unlockWorldTier = 8;
        [SerializeField, Min(1)] private int maximumActiveDetectives = 3;
        [SerializeField, Min(1)] private int detectivesPerSummon = 1;
        [SerializeField, Min(0f)] private float summonWindup = 0.75f;
        [SerializeField, Min(0.1f)] private float summonCooldown = 12f;
        [SerializeField, Min(0f)] private float spawnRadius = 2.5f;
        [SerializeField] private Color telegraphColor = new Color(0.25f, 0.85f, 1f, 0.9f);

        [Header("Current Specialists (Read Only)")]
        [SerializeField, Min(0)] private int activeRealDetectives;
        [SerializeField] private bool isSummoning;

        private readonly HashSet<Damageable> activeDetectives = new HashSet<Damageable>();
        private readonly List<Damageable> staleDetectives = new List<Damageable>();
        private WorldDifficultyManager difficultyManager;
        private DetectiveEncounterCoordinator coordinator;
        private Coroutine summonRoutine;
        private float nextSummonTime;

        public int ActiveRealDetectives => activeRealDetectives;
        public GameObject DetectivePrefab => detectivePrefab;
        public bool IsBusy => isSummoning;
        public bool IsReady => !isSummoning
            && detectivePrefab != null
            && Time.time >= nextSummonTime
            && ActiveWorldTier >= unlockWorldTier
            && activeRealDetectives < maximumActiveDetectives;
        public bool IsConfigured => detectivePrefab != null;

        private int ActiveWorldTier => difficultyManager != null
            ? difficultyManager.DifficultyTier
            : 0;

        public void Configure(
            GameObject prefab,
            WorldDifficultyManager manager,
            int unlockTier,
            int maximumActive,
            int perSummon,
            float windup,
            float cooldown,
            float radius)
        {
            if (detectivePrefab == null)
            {
                detectivePrefab = prefab;
            }

            difficultyManager = manager;
            unlockWorldTier = Mathf.Max(0, unlockTier);
            maximumActiveDetectives = Mathf.Max(1, maximumActive);
            detectivesPerSummon = Mathf.Max(1, perSummon);
            summonWindup = Mathf.Max(0f, windup);
            summonCooldown = Mathf.Max(0.1f, cooldown);
            spawnRadius = Mathf.Max(0f, radius);
            coordinator = DetectiveEncounterCoordinator.GetOrCreate();
        }

        private void Update()
        {
            RemoveInactiveDetectives();
        }

        public bool TrySummon()
        {
            RemoveInactiveDetectives();
            if (!IsReady)
            {
                return false;
            }

            isSummoning = true;
            summonRoutine = StartCoroutine(SummonRoutine());
            return true;
        }

        private IEnumerator SummonRoutine()
        {
            CombatShapeEffect.Create(
                transform.position,
                CombatShape.Diamond,
                spawnRadius,
                telegraphColor,
                Mathf.Max(0.2f, summonWindup));
            yield return new WaitForSeconds(summonWindup);

            int count = Mathf.Min(
                ResolveRequestedSummonCount(),
                maximumActiveDetectives - activeDetectives.Count);
            for (int index = 0; index < count; index++)
            {
                SpawnDetective(index, count);
            }

            isSummoning = false;
            summonRoutine = null;
            nextSummonTime = Time.time + summonCooldown;
            RefreshCount();
        }

        private void SpawnDetective(int index, int count)
        {
            float normalized = count <= 1 ? 0f : index / (float)(count - 1) - 0.5f;
            Vector2 offset = new Vector2(normalized * spawnRadius, 0f);
            float tacticalBias = coordinator != null
                ? coordinator.TacticalHorizontalBias
                : 0f;
            if (count <= 1)
            {
                offset.x = Random.Range(-spawnRadius * 0.35f, spawnRadius * 0.35f)
                    + tacticalBias * spawnRadius * 0.65f;
            }
            else
            {
                offset.x += tacticalBias * spawnRadius * 0.35f;
            }

            offset.x = Mathf.Clamp(offset.x, -spawnRadius, spawnRadius);

            GameObject spawned = Instantiate(
                detectivePrefab,
                (Vector2)transform.position + offset,
                Quaternion.identity);
            DetectiveIdentity identity = spawned.GetComponent<DetectiveIdentity>();
            if (identity == null)
            {
                identity = spawned.AddComponent<DetectiveIdentity>();
            }

            identity.ConfigureReal(gameObject, coordinator);
            Damageable damageable = spawned.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = spawned.AddComponent<Damageable>();
            }

            activeDetectives.Add(damageable);
            damageable.Died += () =>
            {
                activeDetectives.Remove(damageable);
                RefreshCount();
                if (damageable != null)
                {
                    Destroy(damageable.gameObject);
                }
            };

            EnemyDifficultyScaler scaler = spawned.GetComponent<EnemyDifficultyScaler>();
            if (scaler != null && difficultyManager != null)
            {
                scaler.Configure(difficultyManager);
            }
        }

        private int ResolveRequestedSummonCount()
        {
            int requested = detectivesPerSummon;
            PlayerCurseController curse = PlayerCurseController.Active;
            DetectiveCurseHordeDirector horde = curse != null
                ? curse.GetComponent<DetectiveCurseHordeDirector>()
                : null;
            if (curse != null
                && curse.DetectivesCurseActive
                && horde != null
                && horde.IsQualifyingHorde)
            {
                int guaranteed = Mathf.Min(
                    maximumActiveDetectives,
                    curse.GuaranteedRealDetectives);
                requested = Mathf.Max(requested, guaranteed - activeDetectives.Count);
            }

            return Mathf.Max(1, requested);
        }

        private void RemoveInactiveDetectives()
        {
            staleDetectives.Clear();
            foreach (Damageable detective in activeDetectives)
            {
                if (detective == null || !detective.gameObject.activeInHierarchy)
                {
                    staleDetectives.Add(detective);
                }
            }

            foreach (Damageable stale in staleDetectives)
            {
                activeDetectives.Remove(stale);
            }

            RefreshCount();
        }

        private void RefreshCount()
        {
            activeRealDetectives = activeDetectives.Count;
        }

        public void Interrupt()
        {
            if (!isSummoning || summonRoutine == null)
            {
                return;
            }

            StopCoroutine(summonRoutine);
            summonRoutine = null;
            isSummoning = false;
            nextSummonTime = Time.time + summonCooldown * 0.35f;
        }

        private void OnDisable()
        {
            if (summonRoutine != null)
            {
                StopCoroutine(summonRoutine);
                summonRoutine = null;
            }

            isSummoning = false;
        }
    }
}
