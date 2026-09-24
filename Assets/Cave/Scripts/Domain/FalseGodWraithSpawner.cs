using System.Collections.Generic;
using UnityEngine;

namespace Cave.Domain
{
    /// <summary>Boss-local, bounded Wraith manifestation from a resolved Shadow Dash impact.</summary>
    [DisallowMultipleComponent]
    public sealed class FalseGodWraithSpawner : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float shadowDashSpawnChance = 0.18f;
        [SerializeField, Min(0f)] private float shadowDashSpawnCooldown = 12f;
        private readonly List<ClaimWraith> spawned = new List<ClaimWraith>(4);
        private GameObject prefab;
        private float nextSpawnTime;

        public bool TryManifestFromShadowDash(Vector2 position)
        {
            if (Time.time < nextSpawnTime || Random.value > shadowDashSpawnChance) return false;
            Vector2 spawnPosition = position + new Vector2(Random.value < 0.5f ? -0.8f : 0.8f, 0f);
            if (Physics2D.OverlapCircle(spawnPosition, 0.2f) != null) return false;
            if (prefab == null) prefab = Resources.Load<GameObject>("Domain/ClaimWraith/ClaimWraith");
            if (prefab == null) return false;
            GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
            ClaimWraith wraith = instance.GetComponent<ClaimWraith>();
            if (wraith != null) { wraith.MarkSpawnedByBuapahShadowDash(); spawned.Add(wraith); }
            nextSpawnTime = Time.time + shadowDashSpawnCooldown;
            return wraith != null;
        }

        private void OnDisable()
        {
            for (int index = spawned.Count - 1; index >= 0; index--) if (spawned[index] != null) Destroy(spawned[index].gameObject);
            spawned.Clear();
        }
    }
}
