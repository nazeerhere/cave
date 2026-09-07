using System.Collections.Generic;
using System.Collections;
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
        [SerializeField, Min(0.5f)] private float hordeStateProbeInterval = 1f;

        [Header("Detective Participation")]
        [SerializeField] private GameObject detectivePrefab;
        [SerializeField, Min(1)] private int maximumRealDetectives = 5;
        [SerializeField, Range(1, 3)] private int maximumSpawnAttemptsPerHorde = 2;
        [SerializeField, Min(1f)] private float spawnDistance = 6f;

        [Header("Safe Spawn Placement")]
        [SerializeField, Min(1)] private int placementAttempts = 8;
        [SerializeField, Min(0.1f)] private float placementSearchRadius = 4f;
        [SerializeField, Min(0.1f)] private float groundProbeHeight = 4f;
        [SerializeField, Min(0.1f)] private float groundProbeDistance = 10f;
        [SerializeField, Min(0.1f)] private float playerClearance = 1.25f;
        [SerializeField, Min(0.1f)] private float mobClearance = 0.8f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Current Encounter (Read Only)")]
        [SerializeField, Min(0)] private int qualifyingHostileCount;
        [SerializeField, Min(0)] private int realDetectivesPresent;
        [SerializeField, Min(0)] private int requiredDetectives;
        [SerializeField] private bool hordeActive;
        [SerializeField] private bool participationApplied;
        [SerializeField] private bool spawnOpportunityHandled;
        [SerializeField, Min(0)] private int spawnAttemptsThisHorde;
        [SerializeField] private float lastSpawnAttempt;
        [SerializeField, TextArea] private string lastSpawnFailureReason;

        private readonly List<Damageable> hostiles = new List<Damageable>();
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
        private readonly Collider2D[] overlapResults = new Collider2D[32];
        private PlayerCurseController curses;
        private DetectiveEncounterCoordinator coordinator;
        private WorldDifficultyManager difficultyManager;
        private Coroutine hordeStateMonitor;
        private WaitForSeconds hordeStateProbeWait;
        private bool prefabResolutionAttempted;
        private bool prefabResolutionDisabled;
        private bool missingPrefabWarningIssued;

        public int QualifyingHostileCount => qualifyingHostileCount;
        public bool IsQualifyingHorde => hordeActive;

        private void Awake()
        {
            curses = GetComponent<PlayerCurseController>();
        }

        private void OnEnable()
        {
            if (curses == null)
            {
                curses = GetComponent<PlayerCurseController>();
            }

            if (curses != null)
            {
                curses.CurseStateChanged -= HandleCurseStateChanged;
                curses.CurseStateChanged += HandleCurseStateChanged;
            }

            StartHordeStateMonitor();
        }

        private void OnDisable()
        {
            if (curses != null)
            {
                curses.CurseStateChanged -= HandleCurseStateChanged;
            }

            if (hordeStateMonitor != null)
            {
                StopCoroutine(hordeStateMonitor);
                hordeStateMonitor = null;
            }
        }

        private void StartHordeStateMonitor()
        {
            if (hordeStateMonitor == null)
            {
                hordeStateMonitor = StartCoroutine(MonitorHordeState());
            }
        }

        private IEnumerator MonitorHordeState()
        {
            RefreshHordeState();
            while (true)
            {
                if (hordeStateProbeWait == null)
                {
                    hordeStateProbeWait = new WaitForSeconds(hordeStateProbeInterval);
                }

                yield return hordeStateProbeWait;
                RefreshHordeState();
            }
        }

        private void HandleCurseStateChanged()
        {
            // A curse selection is a genuine state transition, so this is the one
            // immediate probe allowed outside the coarse horde monitor.
            RefreshHordeState();
        }

        private void RefreshHordeState()
        {
            if (curses == null || !curses.DetectivesCurseActive)
            {
                EndHorde();
                return;
            }

            qualifyingHostileCount = HostileMobQuery.CountRealHostiles(
                transform.position,
                encounterRadius,
                hostiles);
            if (qualifyingHostileCount < hostileThreshold)
            {
                EndHorde();
                return;
            }

            if (!hordeActive)
            {
                BeginHorde();
            }
        }

        private void BeginHorde()
        {
            hordeActive = true;
            participationApplied = false;
            spawnOpportunityHandled = false;
            spawnAttemptsThisHorde = 0;
            requiredDetectives = 0;
            realDetectivesPresent = 0;
            lastSpawnFailureReason = string.Empty;
            EvaluateDetectiveSpawnOpportunity();
        }

        private void EndHorde()
        {
            hordeActive = false;
            participationApplied = false;
            spawnOpportunityHandled = false;
            spawnAttemptsThisHorde = 0;
            qualifyingHostileCount = 0;
            realDetectivesPresent = 0;
            requiredDetectives = 0;
            lastSpawnFailureReason = string.Empty;
        }

        public void UseDetectivePrefabIfMissing(GameObject prefab)
        {
            if (!HasValidDetectivePrefab(detectivePrefab)
                && HasValidDetectivePrefab(prefab))
            {
                detectivePrefab = prefab;
                prefabResolutionDisabled = false;
                missingPrefabWarningIssued = false;
            }
        }

        private void EvaluateDetectiveSpawnOpportunity()
        {
            if (spawnOpportunityHandled)
            {
                return;
            }

            // An opportunity is consumed even when it cannot produce a Detective.
            // This deliberately prevents a failed placement from becoming an
            // expensive horde-long retry loop.
            spawnOpportunityHandled = true;
            if (!TryResolveDetectivePrefab())
            {
                CompleteSpawnOpportunity("Detective prefab is not configured.");
                return;
            }

            coordinator = ResolveCoordinator();
            if (coordinator == null)
            {
                CompleteSpawnOpportunity("Detective encounter coordinator is unavailable.");
                return;
            }

            requiredDetectives = Mathf.Min(
                maximumRealDetectives,
                curses.GuaranteedRealDetectives);
            realDetectivesPresent = coordinator.RealDetectiveCount;
            int missing = Mathf.Max(0, requiredDetectives - realDetectivesPresent);
            if (missing == 0)
            {
                CompleteSpawnOpportunity(string.Empty);
                return;
            }

            if (!HasValidDetectivePrefab(detectivePrefab))
            {
                CompleteSpawnOpportunity("Detective prefab has no Damageable component.");
                return;
            }

            int remainingCap = Mathf.Max(0, maximumRealDetectives - coordinator.RealDetectiveCount);
            missing = Mathf.Min(missing, remainingCap);
            if (missing <= 0)
            {
                CompleteSpawnOpportunity("Existing real Detective cap prevents a new spawn.");
                return;
            }

            int successfulSpawns = 0;
            int attemptsAllowed = Mathf.Min(maximumSpawnAttemptsPerHorde, missing);
            for (int index = 0; index < attemptsAllowed; index++)
            {
                spawnAttemptsThisHorde++;
                if (TrySpawnDetective(coordinator, index))
                {
                    successfulSpawns++;
                }
            }

            realDetectivesPresent = coordinator.RealDetectiveCount;
            bool completed = realDetectivesPresent >= requiredDetectives;
            CompleteSpawnOpportunity(completed
                ? string.Empty
                : successfulSpawns > 0
                    ? "Not enough safe positions to complete the Detective guarantee."
                    : lastSpawnFailureReason);
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
            if (difficultyManager == null)
            {
                difficultyManager = FindObjectOfType<WorldDifficultyManager>();
            }

            if (difficultyManager != null)
            {
                spawned.GetComponent<EnemyDifficultyScaler>()?.Configure(difficultyManager);
            }

            return true;
        }

        private DetectiveEncounterCoordinator ResolveCoordinator()
        {
            if (coordinator == null)
            {
                coordinator = DetectiveEncounterCoordinator.GetOrCreate();
            }

            return coordinator;
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
                int hitCount = Physics2D.RaycastNonAlloc(
                    probeOrigin,
                    Vector2.down,
                    groundHits,
                    groundProbeHeight + groundProbeDistance,
                    groundLayers);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit2D hit = groundHits[hitIndex];
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
            int overlapCount = Physics2D.OverlapCircleNonAlloc(
                candidate,
                Mathf.Max(playerClearance, mobClearance),
                overlapResults);
            if (overlapCount >= overlapResults.Length)
            {
                // A full non-alloc buffer means the position is too crowded to
                // prove safe. Rejecting it is safer than allocating a larger query.
                return false;
            }

            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider2D overlap = overlapResults[overlapIndex];
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

        private void CompleteSpawnOpportunity(string failureReason)
        {
            participationApplied = string.IsNullOrEmpty(failureReason);
            lastSpawnFailureReason = failureReason ?? string.Empty;
        }

        private bool TryResolveDetectivePrefab()
        {
            if (HasValidDetectivePrefab(detectivePrefab))
            {
                return true;
            }

            detectivePrefab = null;
            if (prefabResolutionDisabled)
            {
                return false;
            }

            if (!prefabResolutionAttempted)
            {
                prefabResolutionAttempted = true;
                DetectiveCursePrefabLibrary library =
                    Resources.Load<DetectiveCursePrefabLibrary>(PrefabLibraryResourceName);
                if (library != null
                    && library.TryGetDetectivePrefab(out GameObject libraryPrefab)
                    && HasValidDetectivePrefab(libraryPrefab))
                {
                    detectivePrefab = libraryPrefab;
                    return true;
                }

                foreach (NecromancerDetectiveSummoner summoner in
                    FindObjectsOfType<NecromancerDetectiveSummoner>(true))
                {
                    if (summoner != null && HasValidDetectivePrefab(summoner.DetectivePrefab))
                    {
                        detectivePrefab = summoner.DetectivePrefab;
                        return true;
                    }
                }
            }

            prefabResolutionDisabled = true;
            if (!missingPrefabWarningIssued)
            {
                missingPrefabWarningIssued = true;
                Debug.LogWarning(
                    "Detective's Curse cannot guarantee Detective spawns because its optional "
                    + "prefab reference is missing or stale. Spawn retries are disabled until "
                    + "a valid prefab is supplied.",
                    this);
            }

            return false;
        }

        private static bool IsUsablePrefab(GameObject candidate)
        {
            if (ReferenceEquals(candidate, null))
            {
                return false;
            }

            try
            {
                return candidate != null && candidate.GetInstanceID() != 0;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private static bool HasValidDetectivePrefab(GameObject candidate)
        {
            if (!IsUsablePrefab(candidate))
            {
                return false;
            }

            try
            {
                return candidate.GetComponent<Damageable>() != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private void OnValidate()
        {
            hostileThreshold = Mathf.Max(3, hostileThreshold);
            hordeStateProbeInterval = Mathf.Max(0.5f, hordeStateProbeInterval);
            maximumRealDetectives = Mathf.Max(1, maximumRealDetectives);
            maximumSpawnAttemptsPerHorde = Mathf.Clamp(maximumSpawnAttemptsPerHorde, 1, 3);
            placementAttempts = Mathf.Max(1, placementAttempts);
            placementSearchRadius = Mathf.Max(0.1f, placementSearchRadius);
            groundProbeHeight = Mathf.Max(0.1f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(0.1f, groundProbeDistance);
            playerClearance = Mathf.Max(0.1f, playerClearance);
            mobClearance = Mathf.Max(0.1f, mobClearance);
            hordeStateProbeWait = null;
        }
    }
}
