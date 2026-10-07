#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>
    /// Keeps the existing standard projectile as the authored source and
    /// creates the separately inspectable Heavy prefab once. It does not alter
    /// scene or player overrides; launchers retain their safe standard fallback
    /// until a serialized heavy reference is assigned in the editor.
    /// </summary>
    public static class ProjectilePrefabAssetBuilder
    {
        private const string StandardPath = "Assets/Cave/Prefabs/Projectiles/PlayerProjectile.prefab";
        private const string HeavyPath = "Assets/Cave/Prefabs/Projectiles/HeavyPlayerProjectile.prefab";

        [InitializeOnLoadMethod]
        private static void EnsureHeavyPrefabAfterCompile()
        {
            EnsureHeavyPrefab();
        }

        [MenuItem("Tools/Cave/Assets/Ensure Heavy Projectile Prefab")]
        private static void EnsureHeavyPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HeavyPath) != null) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StandardPath) == null)
            {
                Debug.LogError("[Cave] Cannot create Heavy projectile prefab: standard source is missing.");
                return;
            }

            if (!AssetDatabase.CopyAsset(StandardPath, HeavyPath))
            {
                Debug.LogError("[Cave] Heavy projectile prefab copy failed.");
                return;
            }

            AssetDatabase.ImportAsset(HeavyPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Cave] Created editable Heavy Player Projectile prefab.");
        }
    }
}
#endif
