using System;
using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class FinalCombatContentBuilder
    {
        private const string ProjectilePrefabPath = "Assets/Cave/Prefabs/PlayerProjectile.prefab";
        private const string CombatSettingsPath = "Assets/Cave/Resources/PlayerCombatSettings.asset";
        private const int ProjectileLayer = 10;
        private const int GroundLayer = 8;
        private const int DamageableLayer = 9;

        [MenuItem("Tools/Cave/Create Final Combat Assets (New Only)")]
        public static void CreateAssets()
        {
            RefuseToOverwriteExistingAssets();

            PlayerProjectile projectilePrefab = CreateProjectilePrefab();
            PlayerCombatSettings combatSettings = ScriptableObject.CreateInstance<PlayerCombatSettings>();
            SerializedObject serializedSettings = new SerializedObject(combatSettings);
            serializedSettings.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(combatSettings, CombatSettingsPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created final combat PlayerProjectile prefab and settings without modifying a scene or existing prefab.");
        }

        public static void BuildFromCommandLine()
        {
            CreateAssets();
        }

        private static PlayerProjectile CreateProjectilePrefab()
        {
            GameObject projectileObject = new GameObject("Player Projectile");
            projectileObject.layer = ProjectileLayer;

            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.45f, 0.25f);
            renderer.color = new Color(0.25f, 0.9f, 1f, 1f);
            renderer.sortingOrder = 5;

            CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            projectileCollider.radius = 0.2f;

            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            PlayerProjectile projectile = projectileObject.AddComponent<PlayerProjectile>();
            SerializedObject serializedProjectile = new SerializedObject(projectile);
            serializedProjectile.FindProperty("environmentLayers").intValue = 1 << GroundLayer;
            serializedProjectile.FindProperty("damageableLayers").intValue = 1 << DamageableLayer;
            serializedProjectile.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(projectileObject, ProjectilePrefabPath);
            UnityEngine.Object.DestroyImmediate(projectileObject);
            return prefab.GetComponent<PlayerProjectile>();
        }

        private static void RefuseToOverwriteExistingAssets()
        {
            string[] paths = { ProjectilePrefabPath, CombatSettingsPath };
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    throw new InvalidOperationException("Final combat builder refused to overwrite existing asset: " + path);
                }
            }
        }
    }
}
