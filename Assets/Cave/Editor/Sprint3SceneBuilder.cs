using Cave.Combat;
using Cave.Enemies;
using Cave.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class Sprint3SceneBuilder
    {
        private const string Sprint1ScenePath = "Assets/Cave/Scenes/Sprint1_Movement.unity";
        private const string Sprint2ScenePath = "Assets/Cave/Scenes/Sprint2_Combat.unity";
        private const string Sprint3ScenePath = "Assets/Cave/Scenes/Sprint3_Enemies.unity";
        private const int DamageableLayer = 9;

        [MenuItem("Tools/Cave/Destructive Rebuild/Sprint 3 Enemy Scene")]
        public static void BuildSprint3TestScene()
        {
            Scene scene = EditorSceneManager.OpenScene(Sprint2ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Sprint3ScenePath);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                throw new System.InvalidOperationException("Sprint 2 scene does not contain a Player object.");
            }

            if (player.GetComponent<PlayerHealth>() == null)
            {
                player.AddComponent<PlayerHealth>();
            }

            GameObject enemies = new GameObject("Enemies");
            CreateEnemy(enemies.transform, "Enemy 01", new Vector2(-5f, -0.9f), true, new Color(0.35f, 0.85f, 0.45f));
            CreateEnemy(enemies.transform, "Enemy 02", new Vector2(7.5f, -0.9f), false, new Color(0.85f, 0.5f, 0.25f));

            EditorSceneManager.SaveScene(scene);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = enemies.transform.GetChild(0).gameObject;
            EditorGUIUtility.PingObject(Selection.activeGameObject);

            Debug.Log("Cave Sprint 3 scene built successfully at " + Sprint3ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildSprint3TestScene();
            AssetDatabase.SaveAssets();
        }

        private static void CreateEnemy(
            Transform parent,
            string name,
            Vector2 position,
            bool startMovingRight,
            Color color)
        {
            GameObject enemy = new GameObject(name);
            enemy.transform.SetParent(parent);
            enemy.transform.position = position;
            enemy.layer = DamageableLayer;

            SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.9f, 1.2f);
            renderer.color = color;
            renderer.sortingOrder = 1;

            BoxCollider2D collider = enemy.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 1.2f);

            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            EnemyController controller = enemy.AddComponent<EnemyController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("startMovingRight").boolValue = startMovingRight;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            enemy.AddComponent<EnemyContactDamage>();
            enemy.AddComponent<Damageable>();
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Sprint1ScenePath, true),
                new EditorBuildSettingsScene(Sprint2ScenePath, true),
                new EditorBuildSettingsScene(Sprint3ScenePath, true)
            };
        }
    }
}
