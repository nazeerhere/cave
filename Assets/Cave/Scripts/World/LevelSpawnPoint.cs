using UnityEngine;

namespace Cave.World
{
    [DisallowMultipleComponent]
    public sealed class LevelSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnIdentifier = "Default";

        public string SpawnIdentifier => spawnIdentifier;

        public void Configure(string identifier)
        {
            spawnIdentifier = string.IsNullOrWhiteSpace(identifier) ? "Default" : identifier;
        }
    }
}
