using System.Collections.Generic;
using UnityEngine;

namespace Cave.Missions
{
    /// <summary>Groups authored sockets and runtime population without selecting mission rules.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyEncounterZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;
        [SerializeField, Min(0)] private int initialPopulation = 1;
        [SerializeField, Min(1)] private int activeCap = 3;
        [SerializeField] private bool reinforcementEligible = true;
        [SerializeField] private bool spawnEnabled = true;
        [SerializeField] private List<EnemySpawnSocket> sockets = new List<EnemySpawnSocket>(8);

        private readonly HashSet<MissionSpawnIdentity> livingEnemies = new HashSet<MissionSpawnIdentity>();

        public string ZoneId => zoneId;
        public int InitialPopulation => initialPopulation;
        public int ActiveCap => activeCap;
        public bool ReinforcementEligible => reinforcementEligible;
        public bool IsSpawnActive => spawnEnabled && isActiveAndEnabled;
        public int ActiveCount => livingEnemies.Count;
        public IList<EnemySpawnSocket> Sockets => sockets;

        public void Configure(string id, int configuredInitialPopulation, int configuredActiveCap, bool allowsReinforcement)
        {
            zoneId = id;
            initialPopulation = Mathf.Max(0, configuredInitialPopulation);
            activeCap = Mathf.Max(1, configuredActiveCap);
            reinforcementEligible = allowsReinforcement;
        }

        public void RegisterSocket(EnemySpawnSocket socket)
        {
            if (socket != null && !sockets.Contains(socket)) sockets.Add(socket);
        }

        public void UnregisterSocket(EnemySpawnSocket socket) { sockets.Remove(socket); }
        internal void RegisterEnemy(MissionSpawnIdentity identity) { if (identity != null) livingEnemies.Add(identity); }
        internal void UnregisterEnemy(MissionSpawnIdentity identity) { livingEnemies.Remove(identity); }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(zoneId)) zoneId = gameObject.name;
            for (int index = sockets.Count - 1; index >= 0; index--)
            {
                if (sockets[index] == null) sockets.RemoveAt(index);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsSpawnActive ? new Color(0.25f, 0.75f, 1f, 0.75f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.45f);
            for (int index = 0; index < sockets.Count; index++)
            {
                if (sockets[index] != null)
                {
                    Gizmos.DrawLine(transform.position, sockets[index].transform.position);
                }
            }
        }
    }
}
