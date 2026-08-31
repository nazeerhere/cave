using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Project-owned runtime reference for Detective's Curse spawns.</summary>
    [CreateAssetMenu(menuName = "Cave/Enemies/Detective Curse Prefab Library")]
    public sealed class DetectiveCursePrefabLibrary : ScriptableObject
    {
        [SerializeField] private GameObject detectivePrefab;

        public GameObject DetectivePrefab => detectivePrefab;
    }
}
