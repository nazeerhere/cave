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
        [Header("Horde Detection")]
        [SerializeField, Min(3)] private int hostileThreshold = 3;
        [SerializeField, Min(1f)] private float encounterRadius = 14f;
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

        [Header("Detective Participation")]
        [SerializeField] private GameObject detectivePrefab;
        [SerializeField, Min(1)] private int maximumRealDetectives = 5;
        [SerializeField, Min(1f)] private float spawnDistance = 6f;

        [Header("Current Encounter (Read Only)")]
        [SerializeField, Min(0)] private int qualifyingHostileCount;
        [SerializeField] private bool hordeActive;
        [SerializeField] private bool participationApplied;

        private readonly List<Damageable> hostiles = new List<Damageable>();
        private PlayerCurseController curses;
        private float nextRefreshTime;

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
                return;
            }

            hordeActive = true;
            if (curses == null || !curses.DetectivesCurseActive || participationApplied)
            {
                return;
            }

            participationApplied = TryGuaranteeDetectives();
        }

        public void UseDetectivePrefabIfMissing(GameObject prefab)
        {
            if (detectivePrefab == null)
            {
                detectivePrefab = prefab;
            }
        }

        private bool TryGuaranteeDetectives()
        {
            ResolveDetectivePrefab();
            if (detectivePrefab == null)
            {
                return false;
            }

            DetectiveEncounterCoordinator coordinator = DetectiveEncounterCoordinator.GetOrCreate();
            int desired = Mathf.Min(
                maximumRealDetectives,
                curses.GuaranteedRealDetectives);
            int localRealDetectives = 0;
            foreach (Damageable hostile in hostiles)
            {
                DetectiveIdentity identity = hostile != null
                    ? hostile.GetComponent<DetectiveIdentity>()
                    : null;
                if (identity != null && identity.IsRealDetective)
                {
                    localRealDetectives++;
                }
            }

            int missing = Mathf.Min(
                Mathf.Max(0, desired - localRealDetectives),
                Mathf.Max(0, maximumRealDetectives - coordinator.RealDetectiveCount));
            for (int index = 0; index < missing; index++)
            {
                float side = index % 2 == 0 ? 1f : -1f;
                Vector2 position = (Vector2)transform.position
                    + new Vector2(side * (spawnDistance + index * 0.65f), 0.5f);
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
            }

            return true;
        }

        private void ResolveDetectivePrefab()
        {
            if (detectivePrefab != null)
            {
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
        }
    }
}
