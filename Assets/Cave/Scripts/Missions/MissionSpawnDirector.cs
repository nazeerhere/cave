using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using Cave.World;
using UnityEngine;

namespace Cave.Missions
{
    [Serializable]
    public sealed class MissionEnemyCatalogEntry
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private EnemyArchetype roles = EnemyArchetype.Melee;
        [SerializeField] private EnemyMobSize mobSize = EnemyMobSize.Normal;
        [SerializeField] private EnemyLocomotion locomotion = EnemyLocomotion.Ground;
        [SerializeField, Min(0)] private int requiredWorldLevel;
        [SerializeField] private bool bountyEligible = true;
        [SerializeField] private bool corruptedVariant;

        public GameObject Prefab => prefab;
        public EnemyArchetype Roles => roles;
        public EnemyMobSize MobSize => mobSize;
        public EnemyLocomotion Locomotion => locomotion;
        public int RequiredWorldLevel => requiredWorldLevel;
        public bool BountyEligible => bountyEligible;
        public bool CorruptedVariant => corruptedVariant;

        public void Configure(GameObject value, EnemyArchetype roleFlags, EnemyMobSize size, EnemyLocomotion movement, int requiredLevel, bool bountyAllowed, bool isCorruptedVariant)
        {
            prefab = value;
            roles = roleFlags;
            mobSize = size;
            locomotion = movement;
            requiredWorldLevel = Mathf.Max(0, requiredLevel);
            bountyEligible = bountyAllowed;
            corruptedVariant = isCorruptedVariant;
        }
    }

    /// <summary>
    /// The sole population authority for a mission map.  Map components supply
    /// valid places; game mode chooses policy; this component executes it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionSpawnDirector : MonoBehaviour
    {
        [Header("Authored Map Data")]
        [SerializeField] private EnemyEncounterZone[] encounterZones = Array.Empty<EnemyEncounterZone>();
        [SerializeField] private MissionEnemyCatalogEntry[] enemyCatalog = Array.Empty<MissionEnemyCatalogEntry>();

        [Header("Bounded Policy")]
        [SerializeField, Min(1)] private int globalActiveCap = 12;
        [SerializeField, Min(0)] private int purgeInitialDefendersPerSource = 1;
        [SerializeField, Min(0)] private int purgeReinforcementBudgetPerSource = 3;
        [SerializeField, Min(0.1f)] private float purgeReinforcementInterval = 6f;
        [SerializeField, Min(1)] private int containmentWaveCount = 4;
        [SerializeField, Min(1)] private int containmentWaveSize = 2;
        [SerializeField, Min(0.1f)] private float containmentWaveInterval = 7f;

        [Header("Runtime Debug (Read Only)")]
        [SerializeField] private string activeMission;
        [SerializeField] private string activePolicy;
        [SerializeField] private int currentWorldLevel;
        [SerializeField] private int eligibleEnemyCount;
        [SerializeField] private int activeZoneCount;
        [SerializeField] private int validSocketCount;
        [SerializeField] private int currentMissionEnemyCount;
        [SerializeField] private float nextSpawnAt;

        private readonly List<MissionSpawnIdentity> activeIdentities = new List<MissionSpawnIdentity>(16);
        private readonly List<SourcePressure> activeSourcePressure = new List<SourcePressure>(4);
        private readonly Collider2D[] socketOverlapBuffer = new Collider2D[12];
        private MissionRunContext context;
        private System.Random random;
        private int nextLifeId;
        private int classicRequiredAlive;
        private bool classicActive;
        private bool containmentActive;
        private int containmentWavesRemaining;
        private float nextContainmentWaveAt;
        private Transform playerTransform;
        private EnemySpawnSocket lastSelectedSocket;

        public event Action PopulationCleared;
        public event Action<MissionSpawnIdentity> EnemySpawned;
        public event Action<MissionSpawnIdentity> EnemyDied;
        public event Action<MissionSpawnIdentity> ReplacementSpawned;
        public bool IsInitialized => context != null;
        public int ActiveMissionEnemyCount => activeIdentities.Count;
        public int GlobalActiveCap => globalActiveCap;

        public void Configure(EnemyEncounterZone[] zones, MissionEnemyCatalogEntry[] catalog)
        {
            encounterZones = zones ?? Array.Empty<EnemyEncounterZone>();
            enemyCatalog = catalog ?? Array.Empty<MissionEnemyCatalogEntry>();
        }

        public void Initialize(MissionRunContext runContext)
        {
            if (context == runContext && random != null) return;
            StopAllPolicies();
            if (context != null) context.StateChanged -= HandleMissionStateChanged;
            context = runContext;
            if (context != null) context.StateChanged += HandleMissionStateChanged;
            random = new System.Random(context != null ? context.RunSeed : 0);
            nextLifeId = 0;
            CachePlayer();
            CacheZones();
            RefreshDebugState();
        }

        public void BeginClassicSweep()
        {
            if (!CanOperate()) return;
            classicActive = true;
            activePolicy = "Classic Sweep";
            classicRequiredAlive = 0;
            for (int index = 0; index < encounterZones.Length; index++)
            {
                EnemyEncounterZone zone = encounterZones[index];
                if (zone == null) continue;
                for (int population = 0; population < zone.InitialPopulation; population++)
                {
                    if (TrySpawn(zone, MissionSpawnUsage.Normal, false, false, false, null, out _)) classicRequiredAlive++;
                }
            }

            if (classicRequiredAlive == 0) PopulationCleared?.Invoke();
        }

        public void BeginCorruptionPurge()
        {
            // Sources register independently as the mission controller spawns them.
            classicActive = false;
            activePolicy = "Corruption Purge";
        }

        public void RegisterCorruptionSource(CorruptionSource source, Transform sourceSocket)
        {
            if (!CanOperate() || source == null || FindPressure(source) != null) return;
            EnemyEncounterZone zone = FindNearestZone(sourceSocket != null ? sourceSocket.position : source.transform.position);
            if (zone == null) return;
            SourcePressure pressure = new SourcePressure(source, zone, purgeReinforcementBudgetPerSource);
            activeSourcePressure.Add(pressure);
            source.Destroyed += HandleCorruptionSourceDestroyed;
            for (int index = 0; index < purgeInitialDefendersPerSource; index++)
            {
                if (TrySpawn(zone, MissionSpawnUsage.CorruptionDefense, false, true, false, source, out _)) pressure.ActiveDefenders++;
            }
        }

        public bool BeginCorruptBounty(out GameObject bountyEnemy)
        {
            bountyEnemy = null;
            if (!CanOperate()) return false;
            activePolicy = "Corrupt Bounty";
            // Bounty runs retain ordinary, permanent encounters.  The marked
            // target is an additional mission identity, never a replacement.
            for (int index = 0; index < encounterZones.Length; index++)
            {
                EnemyEncounterZone zone = encounterZones[index];
                if (zone == null) continue;
                for (int population = 0; population < zone.InitialPopulation; population++)
                {
                    TrySpawn(zone, MissionSpawnUsage.Normal, false, false, false, null, out _);
                }
            }

            for (int index = 0; index < encounterZones.Length; index++)
            {
                EnemyEncounterZone zone = encounterZones[index];
                if (zone == null || !zone.ReinforcementEligible) continue;
                if (TrySpawn(zone, MissionSpawnUsage.Bounty, false, true, false, null, out MissionSpawnIdentity identity))
                {
                    bountyEnemy = identity.gameObject;
                    return true;
                }
            }

            return false;
        }

        public void BeginContainment()
        {
            if (!CanOperate()) return;
            containmentActive = true;
            activePolicy = "Containment";
            containmentWavesRemaining = containmentWaveCount;
            nextContainmentWaveAt = Time.time;
        }

        public void StopContainment()
        {
            containmentActive = false;
            containmentWavesRemaining = 0;
        }

        public void NotifyIdentityDestroyed(MissionSpawnIdentity identity)
        {
            if (identity == null) return;
            activeIdentities.Remove(identity);
            identity.Zone?.UnregisterEnemy(identity);
        }

        private void Update()
        {
            if (!CanOperate()) return;
            ProcessPurgeReinforcements();
            ProcessContainmentWaves();
            currentMissionEnemyCount = activeIdentities.Count;
            nextSpawnAt = containmentActive ? nextContainmentWaveAt : 0f;
            for (int index = 0; index < activeSourcePressure.Count; index++)
            {
                SourcePressure pressure = activeSourcePressure[index];
                if (pressure != null && (nextSpawnAt <= 0f || pressure.NextSpawnAt < nextSpawnAt))
                {
                    nextSpawnAt = pressure.NextSpawnAt;
                }
            }
        }

        private void ProcessPurgeReinforcements()
        {
            for (int index = 0; index < activeSourcePressure.Count; index++)
            {
                SourcePressure pressure = activeSourcePressure[index];
                if (pressure.Source == null || pressure.SourceDestroyed || pressure.BudgetRemaining <= 0
                    || pressure.ActiveDefenders > 0 || Time.time < pressure.NextSpawnAt)
                {
                    continue;
                }

                if (TrySpawn(pressure.Zone, MissionSpawnUsage.CorruptionDefense, true, false, true, pressure.Source, out _))
                {
                    pressure.ActiveDefenders++;
                    pressure.BudgetRemaining--;
                }
                pressure.NextSpawnAt = Time.time + purgeReinforcementInterval;
            }
        }

        private void ProcessContainmentWaves()
        {
            if (!containmentActive || containmentWavesRemaining <= 0 || Time.time < nextContainmentWaveAt) return;
            int spawned = 0;
            for (int index = 0; index < containmentWaveSize && activeIdentities.Count < globalActiveCap; index++)
            {
                EnemyEncounterZone zone = PickZoneWithUsage(MissionSpawnUsage.Containment);
                if (zone != null && TrySpawn(zone, MissionSpawnUsage.Containment, true, false, false, null, out _)) spawned++;
            }

            containmentWavesRemaining--;
            nextContainmentWaveAt = Time.time + containmentWaveInterval;
            if (spawned == 0 && containmentWavesRemaining <= 0) containmentActive = false;
        }

        private bool TrySpawn(
            EnemyEncounterZone zone,
            MissionSpawnUsage usage,
            bool reinforcement,
            bool objective,
            bool sourceLocal,
            CorruptionSource pressureSource,
            out MissionSpawnIdentity spawnedIdentity)
        {
            spawnedIdentity = null;
            if (playerTransform == null) CachePlayer();
            if (zone == null
                || !zone.IsSpawnActive
                || activeIdentities.Count >= globalActiveCap
                || zone.ActiveCount >= zone.ActiveCap) return false;
            EnemySpawnSocket socket = PickSocket(zone, usage, reinforcement);
            if (socket == null) return false;
            MissionEnemyCatalogEntry entry = PickCatalogEntry(socket, usage);
            if (entry == null) return false;

            GameObject spawned = Instantiate(entry.Prefab, socket.transform.position, socket.transform.rotation);
            spawned.name = entry.Prefab.name + " [Mission " + zone.ZoneId + "]";
            MissionSpawnIdentity identity = spawned.GetComponent<MissionSpawnIdentity>();
            if (identity == null) identity = spawned.AddComponent<MissionSpawnIdentity>();
            bool bounty = usage == MissionSpawnUsage.Bounty;
            identity.Bind(this, zone, socket, context.RunSeed, ++nextLifeId, objective, reinforcement, bounty, pressureSource);
            identity.Died += HandleIdentityDied;
            activeIdentities.Add(identity);
            spawnedIdentity = identity;
            lastSelectedSocket = socket;
            currentMissionEnemyCount = activeIdentities.Count;
            EnemySpawned?.Invoke(identity);
            if (reinforcement) ReplacementSpawned?.Invoke(identity);
            return true;
        }

        private void HandleIdentityDied(MissionSpawnIdentity identity)
        {
            if (identity == null) return;
            identity.Died -= HandleIdentityDied;
            activeIdentities.Remove(identity);
            identity.Zone?.UnregisterEnemy(identity);
            EnemyDied?.Invoke(identity);
            if (classicActive && identity.IsObjectiveEnemy == false)
            {
                classicRequiredAlive = Mathf.Max(0, classicRequiredAlive - 1);
                if (classicRequiredAlive == 0) PopulationCleared?.Invoke();
            }

            SourcePressure pressure = FindPressure(identity.PressureSource);
            if (pressure != null)
            {
                pressure.ActiveDefenders = Mathf.Max(0, pressure.ActiveDefenders - 1);
                pressure.NextSpawnAt = Time.time + purgeReinforcementInterval;
            }
        }

        private EnemySpawnSocket PickSocket(EnemyEncounterZone zone, MissionSpawnUsage usage, bool reinforcement)
        {
            EnemySpawnSocket selected = null;
            EnemySpawnSocket fallback = null;
            int selectedCount = 0;
            validSocketCount = 0;
            for (int index = 0; index < zone.Sockets.Count; index++)
            {
                EnemySpawnSocket socket = zone.Sockets[index];
                if (socket == null
                    || (reinforcement && !socket.ReinforcementEligible)
                    || (socket.MissionUsage & usage) == 0
                    || (usage == MissionSpawnUsage.Bounty && !socket.BountyEligible)
                    || !socket.IsSafeToSpawn(playerTransform, socketOverlapBuffer))
                {
                    continue;
                }

                validSocketCount++;
                if (socket == lastSelectedSocket)
                {
                    fallback = socket;
                    continue;
                }

                selectedCount++;
                if (random.Next(selectedCount) == 0) selected = socket;
            }
            return selected ?? fallback;
        }

        private MissionEnemyCatalogEntry PickCatalogEntry(EnemySpawnSocket socket, MissionSpawnUsage usage)
        {
            MissionEnemyCatalogEntry selected = null;
            int tier = WorldDifficultyManager.CurrentDifficultyTier;
            currentWorldLevel = tier;
            eligibleEnemyCount = 0;
            for (int index = 0; index < enemyCatalog.Length; index++)
            {
                MissionEnemyCatalogEntry entry = enemyCatalog[index];
                if (entry == null || entry.RequiredWorldLevel > tier || !socket.Supports(entry, usage, false)) continue;
                if (usage == MissionSpawnUsage.Bounty && (!entry.BountyEligible || !entry.CorruptedVariant)) continue;
                eligibleEnemyCount++;
                if (random.Next(eligibleEnemyCount) == 0) selected = entry;
            }
            return selected;
        }

        private EnemyEncounterZone PickZoneWithUsage(MissionSpawnUsage usage)
        {
            EnemyEncounterZone selected = null;
            int selectedCount = 0;
            for (int index = 0; index < encounterZones.Length; index++)
            {
                EnemyEncounterZone zone = encounterZones[index];
                if (zone == null || !zone.IsSpawnActive || zone.ActiveCount >= zone.ActiveCap) continue;
                for (int socketIndex = 0; socketIndex < zone.Sockets.Count; socketIndex++)
                {
                    EnemySpawnSocket socket = zone.Sockets[socketIndex];
                    if (socket != null && socket.IsSpawnEnabled && (socket.MissionUsage & usage) != 0)
                    {
                        selectedCount++;
                        if (random.Next(selectedCount) == 0) selected = zone;
                        break;
                    }
                }
            }
            return selected;
        }

        private EnemyEncounterZone FindNearestZone(Vector3 position)
        {
            EnemyEncounterZone closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int index = 0; index < encounterZones.Length; index++)
            {
                EnemyEncounterZone candidate = encounterZones[index];
                if (candidate == null) continue;
                float distance = (candidate.transform.position - position).sqrMagnitude;
                if (distance < closestDistance) { closest = candidate; closestDistance = distance; }
            }
            return closest;
        }

        private SourcePressure FindPressure(CorruptionSource source)
        {
            if (source == null) return null;
            for (int index = 0; index < activeSourcePressure.Count; index++)
            {
                if (activeSourcePressure[index].Source == source) return activeSourcePressure[index];
            }
            return null;
        }

        private void HandleCorruptionSourceDestroyed(CorruptionSource source)
        {
            SourcePressure pressure = FindPressure(source);
            if (pressure == null) return;
            pressure.SourceDestroyed = true;
            source.Destroyed -= HandleCorruptionSourceDestroyed;
        }

        private void CacheZones()
        {
            for (int index = 0; index < encounterZones.Length; index++)
            {
                EnemyEncounterZone zone = encounterZones[index];
                if (zone == null) continue;
                for (int socketIndex = 0; socketIndex < zone.Sockets.Count; socketIndex++) zone.Sockets[socketIndex]?.Configure(zone, zone.Sockets[socketIndex].MobSize, zone.Sockets[socketIndex].Locomotion, zone.Sockets[socketIndex].AllowedRoles, zone.Sockets[socketIndex].MissionUsage, zone.Sockets[socketIndex].BountyEligible, zone.Sockets[socketIndex].ReinforcementEligible, zone.Sockets[socketIndex].MinimumPlayerDistance, zone.Sockets[socketIndex].PreferOffscreen);
            }
        }

        private void CachePlayer()
        {
            PlayerHealth health = FindObjectOfType<PlayerHealth>();
            playerTransform = health != null ? health.transform : null;
        }

        private bool CanOperate()
        {
            // The director only has authority while the controller's active
            // objective is running.  Completion/extraction transitions clear
            // policies through StateChanged and must never admit a late spawn.
            return context != null && context.State == MissionLifecycleState.Active;
        }

        private void StopAllPolicies()
        {
            classicActive = false;
            containmentActive = false;
            containmentWavesRemaining = 0;
            for (int index = 0; index < activeSourcePressure.Count; index++)
            {
                if (activeSourcePressure[index].Source != null) activeSourcePressure[index].Source.Destroyed -= HandleCorruptionSourceDestroyed;
            }
            activeSourcePressure.Clear();
            for (int index = activeIdentities.Count - 1; index >= 0; index--)
            {
                MissionSpawnIdentity identity = activeIdentities[index];
                if (identity == null) continue;
                identity.Died -= HandleIdentityDied;
                identity.Unbind();
            }

            activeIdentities.Clear();
            lastSelectedSocket = null;
            currentMissionEnemyCount = 0;
            validSocketCount = 0;
        }

        private void HandleMissionStateChanged(MissionLifecycleState state)
        {
            if (state == MissionLifecycleState.PrimaryObjectiveComplete
                || state == MissionLifecycleState.Extraction
                || state == MissionLifecycleState.Success
                || state == MissionLifecycleState.Failure
                || state == MissionLifecycleState.None)
            {
                StopAllPolicies();
            }
        }

        private void RefreshDebugState()
        {
            activeMission = context != null ? context.SelectedMode.ToString() : string.Empty;
            activePolicy = string.Empty;
            currentWorldLevel = WorldDifficultyManager.CurrentDifficultyTier;
            activeZoneCount = 0;
            for (int index = 0; index < encounterZones.Length; index++)
            {
                if (encounterZones[index] != null && encounterZones[index].IsSpawnActive) activeZoneCount++;
            }
            currentMissionEnemyCount = activeIdentities.Count;
            nextSpawnAt = 0f;
        }

        private void OnDestroy()
        {
            if (context != null) context.StateChanged -= HandleMissionStateChanged;
            StopAllPolicies();
        }

        private sealed class SourcePressure
        {
            public readonly CorruptionSource Source;
            public readonly EnemyEncounterZone Zone;
            public int BudgetRemaining;
            public int ActiveDefenders;
            public float NextSpawnAt;
            public bool SourceDestroyed;

            public SourcePressure(CorruptionSource source, EnemyEncounterZone zone, int budget)
            {
                Source = source;
                Zone = zone;
                BudgetRemaining = budget;
            }
        }
    }
}
