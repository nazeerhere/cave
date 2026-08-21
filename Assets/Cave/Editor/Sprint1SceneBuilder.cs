using Cave.CameraSystem;
using Cave.Combat;
using Cave.Player;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class Sprint1SceneBuilder
    {
        private const string Sprint1ScenePath = "Assets/Cave/Scenes/Sprint1_Movement.unity";
        private const string Sprint1PrefabPath = "Assets/Cave/Prefabs/Player.prefab";
        private const string Sprint2ScenePath = "Assets/Cave/Scenes/Sprint2_Combat.unity";
        private const string Sprint2PrefabPath = "Assets/Cave/Prefabs/Player_Sprint2.prefab";
        private const int GroundLayer = 8;
        private const int DamageableLayer = 9;

        [MenuItem("Tools/Cave/Destructive Rebuild/Sprint 1 Test Scene")]
        public static void BuildSprint1TestScene()
        {
            ConfigureGroundLayer();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Sprint1_Movement";

            GameObject environment = new GameObject("Environment");
            CreatePlatform(environment.transform, "Ground", new Vector2(0f, -2f), new Vector2(24f, 1f));
            CreatePlatform(environment.transform, "Platform Left", new Vector2(-6f, 0.5f), new Vector2(4f, 0.5f));
            CreatePlatform(environment.transform, "Platform Right", new Vector2(6f, 1.5f), new Vector2(4f, 0.5f));

            GameObject player = CreatePlayer();
            CreateCamera(player.transform);

            PrefabUtility.SaveAsPrefabAssetAndConnect(player, Sprint1PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, Sprint1ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Sprint1ScenePath, true) };
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log("Cave Sprint 1 scene built successfully at " + Sprint1ScenePath);
        }

        [MenuItem("Tools/Cave/Destructive Rebuild/Sprint 2 Combat Scene")]
        public static void BuildSprint2TestScene()
        {
            ConfigureProjectLayers();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Sprint2_Combat";

            GameObject environment = new GameObject("Environment");
            CreatePlatform(environment.transform, "Ground", new Vector2(0f, -2f), new Vector2(24f, 1f));
            CreatePlatform(environment.transform, "Platform Left", new Vector2(-6f, 0.5f), new Vector2(4f, 0.5f));
            CreatePlatform(environment.transform, "Platform Right", new Vector2(6f, 1.5f), new Vector2(4f, 0.5f));

            GameObject boundaries = new GameObject("Boundaries");
            CreateDeathBoundary(boundaries.transform);

            GameObject player = CreateSprint2Player();

            GameObject combatTest = new GameObject("Combat Test");
            CreateDamageableDummy(combatTest.transform);

            CreateCamera(player.transform);

            PrefabUtility.SaveAsPrefabAssetAndConnect(player, Sprint2PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, Sprint2ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Sprint1ScenePath, true),
                new EditorBuildSettingsScene(Sprint2ScenePath, true)
            };

            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log("Cave Sprint 2 scene built successfully at " + Sprint2ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildSprint1TestScene();
            AssetDatabase.SaveAssets();
        }

        public static void BuildSprint2FromCommandLine()
        {
            BuildSprint2TestScene();
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreatePlayer()
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0f, 0f);

            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(1f, 1.8f);
            renderer.color = new Color(0.25f, 0.7f, 1f);
            renderer.sortingOrder = 1;

            BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 1.8f);

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            PlayerController controller = player.AddComponent<PlayerController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("groundLayer").intValue = 1 << GroundLayer;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        private static GameObject CreateSprint2Player()
        {
            GameObject player = CreatePlayer();
            player.AddComponent<PlayerRespawn>();

            GameObject swordPivot = new GameObject("Sword Pivot");
            swordPivot.transform.SetParent(player.transform, false);

            GameObject sword = new GameObject("Sword");
            sword.transform.SetParent(swordPivot.transform, false);
            sword.transform.localPosition = new Vector3(1.05f, 0f, 0f);

            SpriteRenderer swordRenderer = sword.AddComponent<SpriteRenderer>();
            swordRenderer.sprite = GetPlaceholderSprite();
            swordRenderer.drawMode = SpriteDrawMode.Sliced;
            swordRenderer.size = new Vector2(1.4f, 0.25f);
            swordRenderer.color = new Color(0.95f, 0.85f, 0.3f);
            swordRenderer.sortingOrder = 2;

            BoxCollider2D swordCollider = sword.AddComponent<BoxCollider2D>();
            swordCollider.size = new Vector2(1.4f, 0.25f);
            swordCollider.isTrigger = true;
            swordCollider.enabled = false;

            SpinSwordAttack attack = player.AddComponent<SpinSwordAttack>();
            SerializedObject serializedAttack = new SerializedObject(attack);
            serializedAttack.FindProperty("swordPivot").objectReferenceValue = swordPivot.transform;
            serializedAttack.FindProperty("swordVisual").objectReferenceValue = sword;
            serializedAttack.FindProperty("attackCollider").objectReferenceValue = swordCollider;
            serializedAttack.FindProperty("damageableLayers").intValue = 1 << DamageableLayer;
            serializedAttack.ApplyModifiedPropertiesWithoutUndo();

            sword.SetActive(true);

            return player;
        }

        private static GameObject CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(target.position.x, target.position.y + 1f, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.075f);

            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            SerializedObject serializedFollow = new SerializedObject(follow);
            serializedFollow.FindProperty("target").objectReferenceValue = target;
            serializedFollow.ApplyModifiedPropertiesWithoutUndo();

            return cameraObject;
        }

        private static void CreatePlatform(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject platform = new GameObject(name);
            platform.transform.SetParent(parent);
            platform.transform.position = position;
            platform.layer = GroundLayer;

            SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = new Color(0.35f, 0.32f, 0.38f);

            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static void CreateDamageableDummy(Transform parent)
        {
            GameObject dummy = new GameObject("Damageable Dummy");
            dummy.transform.SetParent(parent);
            dummy.transform.position = new Vector3(3f, -0.9f, 0f);
            dummy.layer = DamageableLayer;

            SpriteRenderer renderer = dummy.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(1.2f, 1.2f);
            renderer.color = new Color(0.9f, 0.35f, 0.35f);
            renderer.sortingOrder = 1;

            BoxCollider2D collider = dummy.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 1.2f);
            dummy.AddComponent<Damageable>();
        }

        private static void CreateDeathBoundary(Transform parent)
        {
            GameObject boundary = new GameObject("Death Boundary");
            boundary.transform.SetParent(parent);
            boundary.transform.position = new Vector3(0f, -9f, 0f);

            BoxCollider2D collider = boundary.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(40f, 2f);
            collider.isTrigger = true;

            boundary.AddComponent<DeathBoundary>();
        }

        private static Sprite GetPlaceholderSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void ConfigureGroundLayer()
        {
            ConfigureLayer(GroundLayer, "Ground");
        }

        private static void ConfigureProjectLayers()
        {
            ConfigureLayer(GroundLayer, "Ground");
            ConfigureLayer(DamageableLayer, "Damageable");
        }

        private static void ConfigureLayer(int layerIndex, string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);

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
    }
}
