using System.Collections.Generic;
using Cave.CameraSystem;
using Cave.Player;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class StartingRoomSceneBuilder
    {
        private const string StartingScenePath = "Assets/Cave/Scenes/00_StartingRoom.unity";
        private const string MainScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";
        private const string MainSceneName = "Sprint5_Hazards";

        [MenuItem("Tools/Cave/Build Starting Room")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            Scene mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            PlayerHealth sourcePlayer = FindComponentInScene<PlayerHealth>(mainScene);
            Camera sourceCamera = FindComponentInScene<Camera>(mainScene);
            if (sourcePlayer == null)
            {
                throw new System.InvalidOperationException(
                    "The main gameplay scene has no PlayerHealth to copy safely.");
            }

            Scene startingScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            SceneManager.SetActiveScene(startingScene);

            GameObject environment = new GameObject("Starting Room Environment");
            SceneManager.MoveGameObjectToScene(environment, startingScene);
            CreateBlock(
                "Backdrop",
                environment.transform,
                Vector2.zero,
                new Vector2(16f, 10f),
                new Color(0.025f, 0.035f, 0.09f, 1f),
                -20,
                false);
            CreateBlock(
                "Floor",
                environment.transform,
                new Vector2(0f, -2.5f),
                new Vector2(12f, 1f),
                new Color(0.16f, 0.13f, 0.18f, 1f),
                -5,
                true);
            CreateBlock(
                "Left Wall",
                environment.transform,
                new Vector2(-6f, 0f),
                new Vector2(1f, 6f),
                new Color(0.12f, 0.11f, 0.17f, 1f),
                -5,
                true);
            CreateBlock(
                "Right Wall",
                environment.transform,
                new Vector2(6f, 0f),
                new Vector2(1f, 6f),
                new Color(0.12f, 0.11f, 0.17f, 1f),
                -5,
                true);
            CreateBlock(
                "Exit Door",
                environment.transform,
                new Vector2(4.75f, -0.4f),
                new Vector2(1.35f, 3.2f),
                new Color(0.08f, 0.38f, 0.55f, 0.75f),
                -3,
                false);

            GameObject player = Object.Instantiate(sourcePlayer.gameObject);
            player.name = "Player";
            player.transform.position = new Vector3(-3f, -1.1f, 0f);
            SceneManager.MoveGameObjectToScene(player, startingScene);

            GameObject spawnObject = new GameObject("Starting Room Spawn");
            spawnObject.transform.position = player.transform.position;
            spawnObject.AddComponent<LevelSpawnPoint>().Configure("StartingRoomSpawn");
            SceneManager.MoveGameObjectToScene(spawnObject, startingScene);

            GameObject transitionObject = new GameObject("Exit To Main Cave");
            transitionObject.transform.position = new Vector3(4.75f, -0.45f, 0f);
            BoxCollider2D transitionTrigger = transitionObject.AddComponent<BoxCollider2D>();
            transitionTrigger.size = new Vector2(1.45f, 3.1f);
            transitionTrigger.isTrigger = true;
            transitionObject.AddComponent<LevelTransition>().Configure(
                MainSceneName,
                "MainEntrance",
                true);
            SceneManager.MoveGameObjectToScene(transitionObject, startingScene);

            CreateRoomLabel(environment.transform);
            CreateCamera(startingScene, sourceCamera, player.transform);

            EditorSceneManager.SaveScene(startingScene, StartingScenePath);
            EditorSceneManager.CloseScene(mainScene, true);
            SceneManager.SetActiveScene(startingScene);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Built safe starting room: " + StartingScenePath);
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void CreateCamera(Scene scene, Camera sourceCamera, Transform player)
        {
            GameObject cameraObject;
            if (sourceCamera != null)
            {
                cameraObject = Object.Instantiate(sourceCamera.gameObject);
                cameraObject.name = "Main Camera";
            }
            else
            {
                cameraObject = new GameObject("Main Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.015f, 0.02f, 0.055f, 1f);
                cameraObject.AddComponent<AudioListener>();
            }

            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            CameraFollow follow = cameraObject.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = cameraObject.AddComponent<CameraFollow>();
            }

            follow.SetTarget(player);
        }

        private static GameObject CreateBlock(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            bool createCollider)
        {
            GameObject block = new GameObject(objectName);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (createCollider)
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                block.layer = groundLayer >= 0 ? groundLayer : 8;
                BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
                collider.size = size;
            }

            return block;
        }

        private static void CreateRoomLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("Starting Room Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = "THE THRESHOLD\nA SAFE PLACE TO PREPARE";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 52;
            label.characterSize = 0.045f;
            label.color = new Color(0.72f, 0.82f, 0.95f, 0.8f);
            label.GetComponent<MeshRenderer>().sortingOrder = 10;
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> orderedScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(StartingScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == StartingScenePath || scene.path == MainScenePath)
                {
                    continue;
                }

                orderedScenes.Add(scene);
            }

            EditorBuildSettings.scenes = orderedScenes.ToArray();
        }
    }
}
