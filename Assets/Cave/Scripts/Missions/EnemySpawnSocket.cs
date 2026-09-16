using System;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Missions
{
    public enum EnemyMobSize { Small, Normal, Large, Any }
    public enum EnemyLocomotion { Ground, Flying, Any }

    [Flags]
    public enum MissionSpawnUsage
    {
        None = 0,
        Normal = 1 << 0,
        CorruptionDefense = 1 << 1,
        Bounty = 1 << 2,
        Containment = 1 << 3
    }

    /// <summary>A map-authored valid enemy location. It does not own an enemy.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawnSocket : MonoBehaviour
    {
        [SerializeField] private EnemyEncounterZone zone;
        [SerializeField] private string socketId;
        [SerializeField] private bool spawnEnabled = true;
        [SerializeField] private EnemyMobSize mobSize = EnemyMobSize.Any;
        [SerializeField] private EnemyLocomotion locomotion = EnemyLocomotion.Ground;
        [SerializeField] private EnemyArchetype allowedRoles = EnemyArchetype.Melee | EnemyArchetype.Ranged;
        [SerializeField] private MissionSpawnUsage missionUsage = MissionSpawnUsage.Normal;
        [SerializeField] private bool bountyEligible;
        [SerializeField] private bool reinforcementEligible;
        [SerializeField, Min(0f)] private float minimumPlayerDistance = 5f;
        [SerializeField] private bool preferOffscreen = true;
        [Header("Safety")]
        [SerializeField, Min(0f)] private float occupancyRadius = 0.35f;
        [SerializeField] private LayerMask safetyBlockingLayers = (1 << 8) | (1 << 9);
        [SerializeField] private bool blockingTriggers;

        public EnemyEncounterZone Zone => zone;
        public string SocketId => string.IsNullOrEmpty(socketId) ? gameObject.name : socketId;
        public bool IsSpawnEnabled => spawnEnabled && isActiveAndEnabled
            && zone != null && zone.IsSpawnActive;
        public EnemyMobSize MobSize => mobSize;
        public EnemyLocomotion Locomotion => locomotion;
        public EnemyArchetype AllowedRoles => allowedRoles;
        public MissionSpawnUsage MissionUsage => missionUsage;
        public bool BountyEligible => bountyEligible;
        public bool ReinforcementEligible => reinforcementEligible;
        public float MinimumPlayerDistance => minimumPlayerDistance;
        public bool PreferOffscreen => preferOffscreen;
        public float OccupancyRadius => occupancyRadius;

        public void Configure(
            EnemyEncounterZone owningZone,
            EnemyMobSize configuredSize,
            EnemyLocomotion configuredLocomotion,
            EnemyArchetype configuredRoles,
            MissionSpawnUsage configuredUsage,
            bool isBountyEligible,
            bool isReinforcementEligible,
            float minimumDistance,
            bool prefersOffscreen)
        {
            zone = owningZone;
            if (string.IsNullOrEmpty(socketId)) socketId = gameObject.name;
            mobSize = configuredSize;
            locomotion = configuredLocomotion;
            allowedRoles = configuredRoles;
            missionUsage = configuredUsage;
            bountyEligible = isBountyEligible;
            reinforcementEligible = isReinforcementEligible;
            minimumPlayerDistance = Mathf.Max(0f, minimumDistance);
            preferOffscreen = prefersOffscreen;
            zone?.RegisterSocket(this);
        }

        /// <summary>
        /// Runs only at a spawn opportunity. The director supplies its reusable
        /// overlap buffer so socket safety never allocates or searches the scene.
        /// </summary>
        public bool IsSafeToSpawn(Transform player, Collider2D[] overlapBuffer)
        {
            if (!IsSpawnEnabled)
            {
                return false;
            }

            if (player != null && minimumPlayerDistance > 0f
                && ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude
                    < minimumPlayerDistance * minimumPlayerDistance)
            {
                return false;
            }

            if (occupancyRadius <= 0f || safetyBlockingLayers.value == 0 || overlapBuffer == null)
            {
                return true;
            }

            int overlaps = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                occupancyRadius,
                overlapBuffer,
                safetyBlockingLayers);
            if (overlaps >= overlapBuffer.Length)
            {
                // A saturated fixed buffer means we cannot prove the socket is
                // clear. Reject conservatively instead of allocating a larger
                // array during a spawn attempt.
                return false;
            }
            for (int index = 0; index < overlaps; index++)
            {
                Collider2D overlap = overlapBuffer[index];
                overlapBuffer[index] = null;
                if (overlap == null || (!blockingTriggers && overlap.isTrigger))
                {
                    continue;
                }

                // Ground colliders reject terrain penetration; Damageable
                // colliders reject occupied enemy sockets. Both layers are
                // authored project layers, not a global actor search.
                return false;
            }

            return true;
        }

        public bool Supports(MissionEnemyCatalogEntry entry, MissionSpawnUsage requestedUsage, bool requiresReinforcement)
        {
            if (entry == null || entry.Prefab == null
                || (missionUsage & requestedUsage) == 0
                || (requiresReinforcement && !reinforcementEligible)
                || (requestedUsage == MissionSpawnUsage.Bounty && !bountyEligible))
            {
                return false;
            }

            bool roleMatches = allowedRoles == EnemyArchetype.None
                || (allowedRoles & entry.Roles) != 0;
            bool sizeMatches = mobSize == EnemyMobSize.Any || entry.MobSize == EnemyMobSize.Any || mobSize == entry.MobSize;
            bool locomotionMatches = locomotion == EnemyLocomotion.Any
                || entry.Locomotion == EnemyLocomotion.Any
                || locomotion == entry.Locomotion;
            return roleMatches && sizeMatches && locomotionMatches;
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(socketId)) socketId = gameObject.name;
        }

        private void OnEnable() { ResolveZone()?.RegisterSocket(this); }
        private void OnDisable() { zone?.UnregisterSocket(this); }
        private void OnValidate()
        {
            ResolveZone();
            if (string.IsNullOrEmpty(socketId)) socketId = gameObject.name;
        }

        private EnemyEncounterZone ResolveZone()
        {
            if (zone == null)
            {
                zone = GetComponentInParent<EnemyEncounterZone>();
            }

            return zone;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsSpawnEnabled ? new Color(0.2f, 0.95f, 0.5f, 0.9f) : new Color(0.9f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, occupancyRadius));
            Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.45f);
        }
    }
}
