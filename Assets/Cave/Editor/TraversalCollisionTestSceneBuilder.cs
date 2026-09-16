using Cave.CameraSystem;
using Cave.Player;
using Cave.World;
using Cave.World.Traversal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    /// <summary>
    /// Builds an isolated traversal fixture. It intentionally does not modify
    /// production maps, prefab assets, or the build settings.
    /// </summary>
    public static class TraversalCollisionTestSceneBuilder
    {
        private const string TestScenePath = "Assets/Cave/Scenes/TraversalCollisionTest.unity";
        private const int GroundLayer = 8;

        [MenuItem("Tools/Cave/Traversal/Build Collision Test Scene")]
        public static void BuildCollisionTestScene()
        {
            ConfigureGroundLayer();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TraversalCollisionTest";

            GameObject environment = new GameObject("Traversal Test Environment");
            CreatePlatform(environment.transform, "Ground (Unrelated Terrain)", new Vector2(0f, -2.5f), new Vector2(26f, 1f), new Color(0.28f, 0.28f, 0.34f));
            CreateOneWayPlatform(environment.transform, "One Way Platform (Pass Below / Stand Above)", new Vector2(-6f, 0f), new Vector2(4f, 0.35f));

            GameObject junctionRoot = new GameObject("Up Down Junction Test");
            junctionRoot.transform.SetParent(environment.transform, false);
            Collider2D upperRouteBlocker = CreatePlatform(
                junctionRoot.transform,
                "Upper Route Blocking Collider",
                new Vector2(2.25f, 1.4f),
                new Vector2(0.35f, 2.15f),
                new Color(0.7f, 0.4f, 0.25f));
            Collider2D lowerRouteBlocker = CreatePlatform(
                junctionRoot.transform,
                "Lower Route Blocking Collider",
                new Vector2(2.25f, -1.35f),
                new Vector2(0.35f, 1.25f),
                new Color(0.35f, 0.5f, 0.8f));

            GameObject junction = new GameObject("Path Junction (Aim Up or Down to Commit)");
            junction.transform.SetParent(junctionRoot.transform, false);
            junction.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            BoxCollider2D junctionTrigger = junction.AddComponent<BoxCollider2D>();
            junctionTrigger.isTrigger = true;
            junctionTrigger.size = new Vector2(3.8f, 4.5f);
            PathJunction2D pathJunction = junction.AddComponent<PathJunction2D>();
            pathJunction.ConfigureRoutes(
                new[]
                {
                    new PathJunction2D.RouteDefinition(PathJunction2D.RouteDirection.Up, new[] { upperRouteBlocker }),
                    new PathJunction2D.RouteDefinition(PathJunction2D.RouteDirection.Down, new[] { lowerRouteBlocker }),
                    new PathJunction2D.RouteDefinition(PathJunction2D.RouteDirection.Left, new[] { lowerRouteBlocker }),
                    new PathJunction2D.RouteDefinition(PathJunction2D.RouteDirection.Right, new[] { upperRouteBlocker }),
                    new PathJunction2D.RouteDefinition(PathJunction2D.RouteDirection.CustomPositive, new[] { upperRouteBlocker }),
                    new PathJunction2D.RouteDefinition(PathJunction2D.RouteDirection.CustomNegative, new[] { lowerRouteBlocker })
                },
                new Vector2(1f, 1f),
                new Vector2(-1f, -1f));

            GameObject player = CreateTestPlayer();
            CreateCamera(player.transform);

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Selection.activeGameObject = junction;
            EditorGUIUtility.PingObject(junction);
            Debug.Log("[Cave] Traversal collision test scene built at " + TestScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildCollisionTestScene();
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateTestPlayer()
        {
            GameObject player = new GameObject("Traversal Test Player");
            player.transform.position = new Vector3(-10f, -0.9f, 0f);

            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.9f, 1.8f);
            renderer.color = new Color(0.25f, 0.75f, 1f);
            renderer.sortingOrder = 2;

            BoxCollider2D bodyCollider = player.AddComponent<BoxCollider2D>();
            bodyCollider.size = new Vector2(0.9f, 1.8f);

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            PlayerController controller = player.AddComponent<PlayerController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("groundLayer").intValue = 1 << GroundLayer;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            player.AddComponent<PlayerRespawn>();
            player.AddComponent<TemporaryCollisionBypass2D>();
            return player;
        }

        private static void CreateCamera(Transform target)
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
        }

        private static Collider2D CreatePlatform(
            Transform parent,
            string platformName,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject platform = new GameObject(platformName);
            platform.transform.SetParent(parent, false);
            platform.transform.localPosition = position;
            platform.layer = GroundLayer;

            SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;

            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        private static void CreateOneWayPlatform(Transform parent, string platformName, Vector2 position, Vector2 size)
        {
            Collider2D collider = CreatePlatform(parent, platformName, position, size, new Color(0.35f, 0.8f, 0.45f));
            collider.gameObject.AddComponent<OneWayPlatform2D>();
        }

        private static void ConfigureGroundLayer()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(GroundLayer);
            if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == "Ground")
            {
                layer.stringValue = "Ground";
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
