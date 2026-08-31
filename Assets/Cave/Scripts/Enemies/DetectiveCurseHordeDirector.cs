using System.Collections.Generic;
using Cave.Combat;
using Cave.Player;
using Cave.World;
using UnityEngine;

namespace Cave.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCurseController))]
    public sealed class DetectiveCurseHordeDirector : MonoBehaviour
    {
        private const string PrefabLibraryResourceName = "DetectiveCursePrefabLibrary";
        [Header("Horde Detection")]
        [SerializeField, Min(3)] private int hostileThreshold = 3;
        [SerializeField, Min(1f)] private float encounterRadius = 14f;
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

        [Header("Detective Participation")]
        [SerializeField] private GameObject detectivePrefab;
        [SerializeField, Min(1)] private int maximumRealDetectives = 5;
        [SerializeField, Min(1f)] private float spawnDistance = 6f;

        [Header("Safe Spawn Placement")]
        [SerializeField, Min(1)] private int placementAttempts = 8;
        [SerializeField, Min(0.1f)] private float placementSearchRadius = 4f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 4f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 10f;
        [SerializeField, Min(0.1f)] private float playerClearance = 1.25f;
        [SerializeField, Min(0.1f)] private float mobClearance = 0.8f;
        [SerializeField, Min(0.1f)] private float retryDelayAfterFailure = 1f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Current Encounter (Read Only)")]
        [SerializeField, Min(0)] private int qualifyingHostileCount;
        [SerializeField, Min(0)] private int realDetectivesPresent;
        [SerializeField, Min(0)] private int requiredDetectives;
        [SerializeField] private bool hordeActive;
        [SerializeField] private bool participationApplied;
        [SerializeField] private bool spawnPending;
        [SerializeField] private float lastSpawnAttempt;
        [SerializeField, TextArea] private string lastSpawnFailureReason;

        private readonly List<Damageable> hostiles = new List<Damageable>();
        private PlayerCurseController curses;
        private float nextRefreshTime;
        private float nextRetryTime;

        public int QualifyingHostileCount => qualifyingHostileCount;
        public bool IsQualifyingHorde => hordeActive;

        private void Awake()
        {
            curses = GetComponent<PlayerCurseController>();
        }

        private void Update()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + refreshInterval;
            qualifyingHostileCount = HostileMobQuery.CountRealHostiles(
                transform.position,
                encounterRadius,
                hostiles);
            bool qualifies = qualifyingHostileCount >= hostileThreshold;
            if (!qualifies)
            {
                hordeActive = false;
                participationApplied = false;
                spawnPending = false;
                realDetectivesPresent = 0;
                requiredDetectives = 0;
                return;
            }

            hordeActive = true;
            if (curses == null || !curses.DetectivesCurseActive)
            {
                participationApplied = false;
                spawnPending = false;
                return;
            }

            EvaluateDetectiveGuarantee();
        }

        public void UseDetectivePrefabIfMissing(GameObject prefab)
        {
            if (detectivePrefab == null)
            {
                detectivePrefab = prefab;
            }
        }

        private void EvaluateDetectiveGuarantee()
        {
            ResolveDetectivePrefab();
            if (detectivePrefab == null)
            {
                MarkSpawnFailure("Detective prefab is not configured.");
                return;
            }

            DetectiveEncounterCoordinator coordinator = DetectiveEncounterCoordinator.GetOrCreate();
            requiredDetectives = Mathf.Min(
                maximumRealDetectives,
                curses.GuaranteedRealDetectives);
            realDetectivesPresent = CountRealDetectivesInEncounter();
            spawnPending = realDetectivesPresent < requiredDetectives;
            participationApplied = !spawnPending;
            if (!spawnPending || Time.time < nextRetryTime)
            {
                return;
            }

            if (detectivePrefab.GetComponent<Damageable>() == null)
            {
                MarkSpawnFailure("Detective prefab has no Damageable component.");
                return;
            }

            int missing = requiredDetectives - realDetectivesPresent;
            int remainingCap = Mathf.Max(0, maximumRealDetectives - coordinator.RealDetectiveCount);
            missing = Mathf.Min(missing, remainingCap);
            if (missing <= 0)
            {
                MarkSpawnFailure("Existing real Detective cap prevents a new spawn.");
                return;
            }

            int successfulSpawns = 0;
            for (int index = 0; index < missing; index++)
            {
                if (TrySpawnDetective(coordinator, index))
                {
                    successfulSpawns++;
                }
            }

            realDetectivesPresent = CountRealDetectivesInEncounter();
            spawnPending = realDetectivesPresent < requiredDetectives;
            participationApplied = !spawnPending;
            if (spawnPending)
            {
                MarkSpawnFailure(successfulSpawns > 0
                    ? "Not enough safe positions to complete the Detective guarantee."
                    : lastSpawnFailureReason);
            }
            else
            {
                lastSpawnFailureReason = string.Empty;
            }
        }

        private bool TrySpawnDetective(DetectiveEncounterCoordinator coordinator, int index)
        {
            lastSpawnAttempt = Time.time;
            if (!TryFindSafeSpawnPosition(index, out Vector2 position))
            {
                lastSpawnFailureReason = "No valid grounded Detective spawn position.";
                return false;
            }

            GameObject spawned = Instantiate(detectivePrefab, position, Quaternion.identity);
            DetectiveIdentity identity = spawned.GetComponent<DetectiveIdentity>();
            if (identity == null)
            {
                identity = spawned.AddComponent<DetectiveIdentity>();
            }

            identity.ConfigureReal(gameObject, coordinator);
            WorldDifficultyManager difficulty = FindObjectOfType<WorldDifficultyManager>();
            if (difficulty != null)
            {
                spawned.GetComponent<EnemyDifficultyScaler>()?.Configure(difficulty);
            }

            return true;
        }

        private int CountRealDetectivesInEncounter()
        {
            int count = 0;
            float radiusSquared = encounterRadius * encounterRadius;
            foreach (DetectiveIdentity identity in FindObjectsOfType<DetectiveIdentity>())
            {
                if (identity == null
                    || !identity.IsRealDetective
                    || !identity.CanAct
                    || ((Vector2)identity.transform.position - (Vector2)transform.position).sqrMagnitude
                        > radiusSquared)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private bool TryFindSafeSpawnPosition(int spawnIndex, out Vector2 position)
        {
            Vector2 encounterCenter = ResolveEncounterCenter();
            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                float side = (attempt + spawnIndex) % 2 == 0 ? 1f : -1f;
                float horizontal = spawnDistance
                    + Random.Range(-placementSearchRadius, placementSearchRadius) * 0.5f;
                Vector2 probeOrigin = encounterCenter
                    + Vector2.right * side * Mathf.Max(1f, horizontal)
                    + Vector2.up * groundProbeHeight;
                RaycastHit2D[] hits = Physics2D.RaycastAll(
                    probeOrigin,
                    Vector2.down,
                    groundProbeHeight + groundProbeDistance,
                    groundLayers);
                foreach (RaycastHit2D hit in hits)
                {
                    Collider2D ground = hit.collider;
                    if (!IsSupportedGround(ground))
                    {
                        continue;
                    }

                    Vector2 candidate = new Vector2(hit.point.x, ground.bounds.max.y + 0.05f);
                    if (IsSafeSpawnPosition(candidate, ground))
                    {
                        position = candidate;
                        return true;
                    }
                }
            }

            position = default;
            return false;
        }

        private Vector2 ResolveEncounterCenter()
        {
            if (hostiles.Count == 0)
            {
                return transform.position;
            }

            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Damageable hostile in hostiles)
            {
                if (hostile != null && hostile.gameObject.activeInHierarchy)
                {
                    sum += (Vector2)hostile.transform.position;
                    count++;
                }
            }

            return count > 0 ? sum / count : (Vector2)transform.position;
        }

        private static bool IsSupportedGround(Collider2D candidate)
        {
            return candidate != null
                && !candidate.isTrigger
                && candidate.GetComponentInParent<Damageable>() == null
                && candidate.GetComponentInParent<PlayerHealth>() == null
                && candidate.GetComponentInParent<DeathBoundary>() == null;
        }

        private bool IsSafeSpawnPosition(Vector2 candidate, Collider2D supportingGround)
        {
            foreach (Collider2D overlap in Physics2D.OverlapCircleAll(
                candidate,
                Mathf.Max(playerClearance, mobClearance)))
            {
                if (overlap == null || overlap.isTrigger || overlap == supportingGround)
                {
                    continue;
                }

                if (overlap.GetComponentInParent<DeathBoundary>() != null)
                {
                    return false;
                }

                if (overlap.GetComponentInParent<PlayerHealth>() != null
                    && Vector2.Distance(candidate, overlap.ClosestPoint(candidate)) < playerClearance)
                {
                    return false;
                }

                if (overlap.GetComponentInParent<Damageable>() != null
                    && Vector2.Distance(candidate, overlap.ClosestPoint(candidate)) < mobClearance)
                {
                    return false;
                }

                if (overlap.bounds.Contains(candidate))
                {
                    return false;
                }
            }

            return true;
        }

        private void MarkSpawnFailure(string reason)
        {
            participationApplied = false;
            spawnPending = true;
            lastSpawnFailureReason = string.IsNullOrEmpty(reason)
                ? "Detective guarantee spawn did not complete."
                : reason;
            nextRetryTime = Time.time + retryDelayAfterFailure;
        }

        private void ResolveDetectivePrefab()
        {
            if (detectivePrefab != null)
            {
                return;
            }

            DetectiveCursePrefabLibrary library = Resources.Load<DetectiveCursePrefabLibrary>(
                PrefabLibraryResourceName);
            if (library != null && library.DetectivePrefab != null)
            {
                detectivePrefab = library.DetectivePrefab;
                return;
            }

            foreach (NecromancerDetectiveSummoner summoner in
                FindObjectsOfType<NecromancerDetectiveSummoner>(true))
            {
                if (summoner.DetectivePrefab != null)
                {
                    detectivePrefab = summoner.DetectivePrefab;
                    return;
                }
            }
        }

        private void OnValidate()
        {
            hostileThreshold = Mathf.Max(3, hostileThreshold);
            maximumRealDetectives = Mathf.Max(1, maximumRealDetectives);
            placementAttempts = Mathf.Max(1, placementAttempts);
            placementSearchRadius = Mathf.Max(0.1f, placementSearchRadius);
            groundProbeHeight = Mathf.Max(0.1f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(0.1f, groundProbeDistance);
            playerClearance = Mathf.Max(0.1f, playerClearance);
            mobClearance = Mathf.Max(0.1f, mobClearance);
            retryDelayAfterFailure = Mathf.Max(0.1f, retryDelayAfterFailure);
        }
    }
}
