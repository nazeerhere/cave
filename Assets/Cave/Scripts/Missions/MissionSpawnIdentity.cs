using System;
using Cave.Combat;
using Cave.Enemies;
using UnityEngine;

namespace Cave.Missions
{
    /// <summary>Runtime provenance for a director-created enemy; enemy AI remains unaware of mission policy.</summary>
    [DisallowMultipleComponent]
    public sealed class MissionSpawnIdentity : MonoBehaviour
    {
        private MissionSpawnDirector director;
        private EnemyEncounterZone zone;
        private EnemySpawnSocket socket;
        private Damageable damageable;
        private int runSeed;
        private int lifeId;
        private bool objectiveEnemy;
        private bool reinforcement;
        private bool bountyTarget;
        private CorruptionSource pressureSource;

        public EnemyEncounterZone Zone => zone;
        public EnemySpawnSocket Socket => socket;
        public int RunSeed => runSeed;
        public int LifeId => lifeId;
        public bool IsObjectiveEnemy => objectiveEnemy;
        public bool IsReinforcement => reinforcement;
        public bool IsBountyTarget => bountyTarget;
        public CorruptionSource PressureSource => pressureSource;

        public event Action<MissionSpawnIdentity> Died;

        internal void Bind(
            MissionSpawnDirector owner,
            EnemyEncounterZone owningZone,
            EnemySpawnSocket originalSocket,
            int ownerRunSeed,
            int ownerLifeId,
            bool isObjectiveEnemy,
            bool isReinforcement,
            bool isBountyTarget,
            CorruptionSource source)
        {
            director = owner;
            zone = owningZone;
            socket = originalSocket;
            runSeed = ownerRunSeed;
            lifeId = ownerLifeId;
            objectiveEnemy = isObjectiveEnemy;
            reinforcement = isReinforcement;
            bountyTarget = isBountyTarget;
            pressureSource = source;
            damageable = GetComponent<Damageable>();
            if (damageable != null)
            {
                damageable.Died -= HandleDeath;
                damageable.Died += HandleDeath;
            }

            GetComponent<EnemyRespawner>()?.SetMissionManaged(true);
            zone?.RegisterEnemy(this);
        }

        /// <summary>
        /// Releases mission ownership without destroying the actor. This is used
        /// when a mission ends so ordinary scene cleanup and enemy lifecycle
        /// remain authoritative, while the director cannot retain stale actors.
        /// </summary>
        internal void Unbind()
        {
            if (damageable != null)
            {
                damageable.Died -= HandleDeath;
            }

            zone?.UnregisterEnemy(this);
            director = null;
            zone = null;
            socket = null;
            pressureSource = null;
        }

        private void HandleDeath() { Died?.Invoke(this); }

        private void OnDestroy()
        {
            if (damageable != null) damageable.Died -= HandleDeath;
            zone?.UnregisterEnemy(this);
            director?.NotifyIdentityDestroyed(this);
        }
    }
}
