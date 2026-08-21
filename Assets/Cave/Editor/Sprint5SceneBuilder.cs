using Cave.Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class Sprint5SceneBuilder
    {
        private const string Sprint1ScenePath = "Assets/Cave/Scenes/Sprint1_Movement.unity";
        private const string Sprint2ScenePath = "Assets/Cave/Scenes/Sprint2_Combat.unity";
        private const string Sprint3ScenePath = "Assets/Cave/Scenes/Sprint3_Enemies.unity";
        private const string Sprint4ScenePath = "Assets/Cave/Scenes/Sprint4_RangedCombat.unity";
        private const string Sprint5ScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";
        private const string WarningPrefabPath = "Assets/Cave/Prefabs/BombWarning.prefab";
        private const string ExplosionPrefabPath = "Assets/Cave/Prefabs/BombExplosion.prefab";
        private const string BombPrefabPath = "Assets/Cave/Prefabs/Bomb.prefab";

        private const int DefaultLayer = 0;
        private const int GroundLayer = 8;
        private const int DamageableLayer = 9;
        private const int ProjectileLayer = 10;
        private const int HazardLayer = 11;

        [MenuItem("Tools/Cave/Destructive Rebuild/Sprint 5 Hazard Scene")]
        public static void BuildSprint5TestScene()
        {
            ConfigureLayer(HazardLayer, "Hazard");
            ConfigureHazardLayerCollisions();

            HazardWarning warningPrefab = CreateWarningPrefab();
            BombExplosion explosionPrefab = CreateExplosionPrefab();
            BombProjectile bombPrefab = CreateBombPrefab(explosionPrefab);

            Scene scene = EditorSceneManager.OpenScene(Sprint4ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Sprint5ScenePath);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                throw new System.InvalidOperationException("Sprint 4 scene does not contain a Player object.");
            }

            GameObject hazards = new GameObject("Hazards");
            GameObject spawnerObject = new GameObject("Bomb Spawner");
            spawnerObject.transform.SetParent(hazards.transform, false);

            BombSpawner spawner = spawnerObject.AddComponent<BombSpawner>();
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("bombPrefab").objectReferenceValue = bombPrefab;
            serializedSpawner.FindProperty("warningPrefab").objectReferenceValue = warningPrefab;
            serializedSpawner.FindProperty("hazardContainer").objectReferenceValue = hazards.transform;
            serializedSpawner.FindProperty("player").objectReferenceValue = player.transform;
            serializedSpawner.FindProperty("environmentLayers").intValue = 1 << GroundLayer;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = spawnerObject;
            EditorGUIUtility.PingObject(spawnerObject);
            Debug.Log("Cave Sprint 5 scene built successfully at " + Sprint5ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildSprint5TestScene();
        }

        private static HazardWarning CreateWarningPrefab()
        {
            GameObject warning = new GameObject("Bomb Warning");
            warning.layer = HazardLayer;

            SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(1.4f, 0.35f);
            renderer.color = new Color(1f, 0.85f, 0.1f, 0.55f);
            renderer.sortingOrder = 3;

            warning.AddComponent<HazardWarning>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(warning, WarningPrefabPath);
            Object.DestroyImmediate(warning);
            return prefab.GetComponent<HazardWarning>();
        }

        private static BombExplosion CreateExplosionPrefab()
        {
            GameObject explosion = new GameObject("Bomb Explosion");
            explosion.layer = HazardLayer;

            SpriteRenderer renderer = explosion.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(4f, 4f);
            renderer.color = new Color(1f, 0.25f, 0.05f, 0.5f);
            renderer.sortingOrder = 5;

            explosion.AddComponent<BombExplosion>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(explosion, ExplosionPrefabPath);
            Object.DestroyImmediate(explosion);
            return prefab.GetComponent<BombExplosion>();
        }

        private static BombProjectile CreateBombPrefab(BombExplosion explosionPrefab)
        {
            GameObject bomb = new GameObject("Bomb");
            bomb.layer = HazardLayer;

            SpriteRenderer renderer = bomb.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.6f, 0.6f);
            renderer.color = new Color(0.18f, 0.18f, 0.22f);
            renderer.sortingOrder = 4;

            CircleCollider2D collider = bomb.AddComponent<CircleCollider2D>();
            collider.radius = 0.3f;

            Rigidbody2D body = bomb.AddComponent<Rigidbody2D>();
            body.gravityScale = 2.5f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            BombProjectile projectile = bomb.AddComponent<BombProjectile>();
            SerializedObject serializedProjectile = new SerializedObject(projectile);
            serializedProjectile.FindProperty("explosionPrefab").objectReferenceValue = explosionPrefab;
            serializedProjectile.FindProperty("environmentLayers").intValue = 1 << GroundLayer;
            serializedProjectile.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bomb, BombPrefabPath);
            Object.DestroyImmediate(bomb);
            return prefab.GetComponent<BombProjectile>();
        }

        private static Sprite GetPlaceholderSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void ConfigureHazardLayerCollisions()
        {
            Physics2D.IgnoreLayerCollision(HazardLayer, DefaultLayer, true);
            Physics2D.IgnoreLayerCollision(HazardLayer, DamageableLayer, true);
            Physics2D.IgnoreLayerCollision(HazardLayer, ProjectileLayer, true);
            Physics2D.IgnoreLayerCollision(HazardLayer, HazardLayer, true);
            Physics2D.IgnoreLayerCollision(HazardLayer, GroundLayer, false);
        }

        private static void ConfigureLayer(int layerIndex, string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(layerIndex);

            if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == layerName)
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            throw new System.InvalidOperationException(
                "Layer " + layerIndex + " is already named '" + layer.stringValue
                + "'. Assign an unused layer to " + layerName + " before building.");
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Sprint1ScenePath, true),
                new EditorBuildSettingsScene(Sprint2ScenePath, true),
                new EditorBuildSettingsScene(Sprint3ScenePath, true),
                new EditorBuildSettingsScene(Sprint4ScenePath, true),
                new EditorBuildSettingsScene(Sprint5ScenePath, true)
            };
        }
    }
}
