using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>
    /// Explicit per-prefab mapping to a hand-authored corrupted replacement.
    /// It never creates, edits, or derives visual assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredCorruptedPrefabBinding : MonoBehaviour
    {
        [SerializeField] private GameObject authoredCorruptedPrefab;
        [SerializeField] private bool isBossMapping;
        [SerializeField] private bool grantGeneralShardOnFinalDeath;

        public GameObject AuthoredCorruptedPrefab => authoredCorruptedPrefab;
        public bool IsBossMapping => isBossMapping;
        public bool GrantGeneralShardOnFinalDeath => grantGeneralShardOnFinalDeath;
        public bool HasValidMapping => authoredCorruptedPrefab != null;
    }
}
