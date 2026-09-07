using System;
using Cave.CameraSystem;
using Cave.EnvironmentFx;
using Cave.Player;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>One focused environmental assembly pass for the final Heart Chamber arena.</summary>
    public static class HeartChamberSceneBuilder
    {
        public const string ScenePath = "Assets/Cave/Scenes/05_HeartChamber.unity";
        private const string SourceScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";

        [MenuItem("Tools/Cave/Scene Builder/Build 05 Heart Chamber (New Scene Only)")]
        public static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Build();
            }
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            AssetDatabase.ImportAsset(HeartChamberTileSheetImporter.StructuralSheetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(HeartChamberTileSheetImporter.CrystalWaterSheetPath, ImportAssetOptions.ForceUpdate);
            Scene chamber = CaveSceneBuilderV1.CreateNewScene(ScenePath);
            SceneManager.SetActiveScene(chamber);
            CaveSceneBuilderV1.RoomRoots roots = CaveSceneBuilderV1.CreateRoomRoot(chamber, "05_HeartChamber");

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            try
            {
                PlayerHealth sourcePlayer = FindComponentInScene<PlayerHealth>(source);
                Camera sourceCamera = FindComponentInScene<Camera>(source);
                if (sourcePlayer == null)
                {
                    throw new InvalidOperationException("Sprint5_Hazards has no PlayerHealth to clone for 05_HeartChamber.");
                }

                BuildEnvironment(roots, chamber);
                BuildGameplayMarkers(roots, chamber);
                GameObject player = ClonePlayer(sourcePlayer, chamber, roots.PlayerSpawn);
                CreateCamera(sourceCamera, player.transform, chamber, roots.Camera);
                CreateLighting(roots.LightingVfx, chamber);

                Physics2D.SyncTransforms();
                if (!CaveSceneBuilderV1.ValidateRoom(chamber, "HeartChamberLandmark"))
                {
                    throw new InvalidOperationException("05_HeartChamber validation failed; the scene was not saved.");
                }

                CaveSceneBuilderV1.SaveScene(chamber, ScenePath);
                Debug.Log("Created Heart Chamber environmental scene at " + ScenePath + ". No boss runtime systems were added.");
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(chamber);
            }
        }

        private static void BuildEnvironment(CaveSceneBuilderV1.RoomRoots roots, Scene scene)
        {
            Sprite entryCave = Require(Structural("Heart_EntryCave"), "entry cave");
            Sprite ceilingLeft = Require(Structural("Heart_CeilingLeft"), "left ceiling");
            Sprite ceilingCenter = Require(Structural("Heart_CeilingCenter"), "center ceiling");
            Sprite ceilingRight = Require(Structural("Heart_CeilingRight"), "right ceiling");
            Sprite threshold = Require(Structural("Heart_AncientThreshold"), "ancient threshold");
            Sprite leftLedge = Require(Structural("Heart_LeftLedge"), "left ledge");
            Sprite rightLedge = Require(Structural("Heart_RightLedge"), "right ledge");
            Sprite entryDoor = Require(Structural("Heart_EntryDoor"), "entry doorway");
            Sprite pool = Require(Structural("Heart_BackgroundPool"), "background pool");
            Sprite arena = Require(Structural("Heart_CentralArena"), "central arena");
            Sprite fragments = Require(Structural("Heart_FloorFragments"), "floor fragments");
            Sprite crystalField = Require(Crystal("Heart_CrystalField"), "crystal field");
            Sprite stalactites = Require(Crystal("Heart_StalactiteCrown"), "stalactite crown");
            Sprite cyanVeins = Require(Crystal("Heart_CyanVeins"), "cyan veins");
            Sprite purpleVeins = Require(Crystal("Heart_PurpleVeins"), "purple veins");
            Sprite rightVeins = Require(Crystal("Heart_RightVeins"), "right veins");
            Sprite rockSpines = Require(Crystal("Heart_RockSpines"), "rock spines");
            Sprite waterDetail = Require(Crystal("Heart_WaterPoolDetail"), "water detail");
            Sprite bubbles = Require(Crystal("Heart_BubbleRiser"), "bubble riser");
            Sprite waterColumn = Require(Crystal("Heart_ReverseWaterColumn"), "reverse water column");
            Sprite mistColumn = Require(Crystal("Heart_MistColumn"), "mist column");
            Sprite caveBase = Require(Crystal("Heart_LivingCaveBase"), "living cave base");
            Sprite crystalRubble = Require(Crystal("Heart_CrystalRubble"), "crystal rubble");

            // Rear cave, vein work, pool, and ancient threshold are intentionally background-only.
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Rear Cave", roots.Background, caveBase, new Vector2(0f, 3.0f), new Vector3(7.3f, 7.3f, 1f), -45);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Left Entry Cave", roots.Background, entryCave, new Vector2(-23.5f, 0f), new Vector3(4.5f, 4.5f, 1f), -40);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Pool", roots.Background, pool, new Vector2(0f, 2.6f), new Vector3(5.1f, 5.1f, 1f), -34);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Pool Detail", roots.Background, waterDetail, new Vector2(0f, 2.65f), new Vector3(5.5f, 5.5f, 1f), -33);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Ancient Threshold", roots.Background, threshold, new Vector2(0f, 11.4f), new Vector3(3.0f, 3.0f, 1f), -26);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Left Ceiling", roots.Background, ceilingLeft, new Vector2(-17.5f, 14.4f), new Vector3(4.3f, 4.3f, 1f), -42);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Center Ceiling", roots.Background, ceilingCenter, new Vector2(0f, 15.2f), new Vector3(4.0f, 4.0f, 1f), -42);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Right Ceiling", roots.Background, ceilingRight, new Vector2(18f, 14.0f), new Vector3(4.1f, 4.1f, 1f), -42);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Heart Chamber Stalactite Crown", roots.Background, stalactites, new Vector2(0f, 15.5f), new Vector3(3.8f, 3.8f, 1f), -41);

            GameObject veinsRoot = CreateContainer("LivingVeins", roots.LightingVfx, scene);
            SpriteRenderer leftVeinsRenderer = CaveSceneBuilderV1.PlaceBackgroundSprite("Living Veins Left", veinsRoot.transform, cyanVeins, new Vector2(-19f, 6.2f), new Vector3(3.7f, 3.7f, 1f), -30).GetComponent<SpriteRenderer>();
            SpriteRenderer centerVeinsRenderer = CaveSceneBuilderV1.PlaceBackgroundSprite("Living Veins Center", veinsRoot.transform, purpleVeins, new Vector2(-5.5f, 8.6f), new Vector3(3.2f, 3.2f, 1f), -30).GetComponent<SpriteRenderer>();
            SpriteRenderer rightVeinsRenderer = CaveSceneBuilderV1.PlaceBackgroundSprite("Living Veins Right", veinsRoot.transform, rightVeins, new Vector2(18.5f, 6.5f), new Vector3(3.35f, 3.35f, 1f), -30).GetComponent<SpriteRenderer>();
            veinsRoot.AddComponent<LivingVeinPulse>().Configure(
                new[] { leftVeinsRenderer, centerVeinsRenderer, rightVeinsRenderer }, 0.22f, 0.10f);

            // Wide, mostly uninterrupted foreground arena. Its supporting collision is the only major combat ground.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Heart Chamber Main Arena", roots.Terrain, arena, new Vector2(0f, -5.0f), new Vector2(7.5f, 0.22f), new Vector3(4.5f, 4.5f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Heart Chamber Entry Approach", roots.Terrain, fragments, new Vector2(-21.0f, -5.1f), new Vector2(3.3f, 0.22f), new Vector3(2.2f, 2.2f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Heart Chamber Left Ledge", roots.Terrain, leftLedge, new Vector2(-17.5f, 1.5f), new Vector2(2.8f, 0.22f), new Vector3(2.5f, 2.5f, 1f), 4);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Heart Chamber Right Ledge", roots.Terrain, rightLedge, new Vector2(17.5f, 1.5f), new Vector2(2.8f, 0.22f), new Vector3(2.5f, 2.5f, 1f), 4);
            CaveSceneBuilderV1.PlaceProp("Heart Chamber Entry Door", roots.Props, entryDoor, new Vector2(-23f, -1.5f), new Vector3(2.7f, 2.7f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Heart Chamber Left Crystal Edge", roots.Props, crystalField, new Vector2(-17.7f, -3.3f), new Vector3(1.15f, 1.15f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Heart Chamber Right Crystal Edge", roots.Props, crystalField, new Vector2(17.7f, -3.3f), new Vector3(-1.15f, 1.15f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Heart Chamber Poolside Spines", roots.Props, rockSpines, new Vector2(0f, -0.4f), new Vector3(2.8f, 2.8f, 1f), 1);
            CaveSceneBuilderV1.PlaceProp("Heart Chamber Lower Left Crystal Dressing", roots.Props, crystalRubble, new Vector2(-22.5f, -7.0f), new Vector3(0.82f, 0.82f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Heart Chamber Lower Right Crystal Dressing", roots.Props, crystalRubble, new Vector2(22.5f, -7.0f), new Vector3(-0.82f, 0.82f, 1f), 6);

            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Heart Chamber Left Arena Boundary", roots.Terrain, new Vector2(-26.5f, -3.5f), new Vector2(0.6f, 11f));
            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Heart Chamber Right Arena Boundary", roots.Terrain, new Vector2(26.5f, -3.5f), new Vector2(0.6f, 11f));
            CaveSceneBuilderV1.PlaceForegroundOccluder("Heart Chamber Foreground Left Frame", roots.Foreground, crystalRubble, new Vector2(-25.0f, -8.4f), new Vector3(0.8f, 0.8f, 1f), 18);
            CaveSceneBuilderV1.PlaceForegroundOccluder("Heart Chamber Foreground Right Frame", roots.Foreground, crystalRubble, new Vector2(25.0f, -8.4f), new Vector3(-0.8f, 0.8f, 1f), 18);

            CreateReverseStream(roots.LightingVfx, scene, waterColumn, mistColumn, bubbles);
        }

        private static void CreateReverseStream(Transform lightingParent, Scene scene, Sprite waterColumn, Sprite mistColumn, Sprite bubbles)
        {
            GameObject streamRoot = CreateContainer("ReverseStream", lightingParent, scene);
            SpriteRenderer lowerStream = CaveSceneBuilderV1.PlaceBackgroundSprite("Reverse Stream Lower Flow", streamRoot.transform, waterColumn, new Vector2(-0.45f, 3.0f), new Vector3(2.3f, 2.3f, 1f), -22).GetComponent<SpriteRenderer>();
            SpriteRenderer coreStream = CaveSceneBuilderV1.PlaceBackgroundSprite("Reverse Stream Bright Core", streamRoot.transform, waterColumn, new Vector2(0.55f, 4.2f), new Vector3(-1.45f, 1.65f, 1f), -21).GetComponent<SpriteRenderer>();
            SpriteRenderer upperStream = CaveSceneBuilderV1.PlaceBackgroundSprite("Reverse Stream Upper Flow", streamRoot.transform, mistColumn, new Vector2(0.1f, 7.2f), new Vector3(1.75f, 1.75f, 1f), -23).GetComponent<SpriteRenderer>();
            lowerStream.color = new Color(0.62f, 1f, 1f, 0.98f);
            coreStream.color = new Color(0.68f, 0.96f, 1f, 0.92f);
            upperStream.color = new Color(0.70f, 0.94f, 1f, 0.82f);
            SpriteRenderer leftBubbles = CaveSceneBuilderV1.PlaceBackgroundSprite("Reverse Stream Bubbles Left", streamRoot.transform, bubbles, new Vector2(-1.3f, 2.6f), new Vector3(0.72f, 0.72f, 1f), -20).GetComponent<SpriteRenderer>();
            SpriteRenderer rightBubbles = CaveSceneBuilderV1.PlaceBackgroundSprite("Reverse Stream Bubbles Right", streamRoot.transform, bubbles, new Vector2(1.3f, 3.7f), new Vector3(-0.64f, 0.64f, 1f), -20).GetComponent<SpriteRenderer>();
            streamRoot.AddComponent<ReverseStreamAnimator>().Configure(
                new[] { lowerStream, coreStream, upperStream }, new[] { leftBubbles, rightBubbles }, 0.28f, 0.36f, 0.22f, 2.4f);
        }

        private static void BuildGameplayMarkers(CaveSceneBuilderV1.RoomRoots roots, Scene scene)
        {
            CaveSceneBuilderV1.PlacePlayerSpawn("Heart Chamber Player Spawn", roots.PlayerSpawn, new Vector2(-21.5f, -4.0f), "FromDeepVeins");
            CreateBossSpawn(roots.Root.transform.Find("Gameplay"), scene, new Vector2(0f, -3.8f));
            CaveSceneBuilderV1.PlaceInteractableMarker("Heart Chamber Landmark Marker", roots.Interactables, new Vector2(0f, 2.5f), "HeartChamberLandmark");
            CaveSceneBuilderV1.PlaceRoomTransitionMarker("Heart Chamber Deep Veins Entrance", roots.Transitions, new Vector2(-24.5f, -4.1f), "ToDeepVeins", "04_DeepVeins");
            CaveSceneBuilderV1.PlaceKillZone("Heart Chamber Fall Kill Zone", roots.Hazards, new Vector2(0f, -13.0f), new Vector2(60f, 2f));
            CaveSceneBuilderV1.SetCameraBounds("Heart Chamber Camera Bounds", roots.Camera, new Vector2(0f, 3.0f), new Vector2(56f, 31f), "HeartChamberCameraBounds");
        }

        private static void CreateBossSpawn(Transform gameplayRoot, Scene scene, Vector2 position)
        {
            GameObject bossSpawn = CreateContainer("BossSpawn", gameplayRoot, scene);
            bossSpawn.transform.position = position;
            bossSpawn.AddComponent<RoomSceneMarker>().Configure(RoomSceneMarker.MarkerKind.EnemySpawn, "BanditAvatarOfCave_BossSpawn");
        }

        private static GameObject ClonePlayer(PlayerHealth sourcePlayer, Scene destination, Transform parent)
        {
            GameObject player = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            player.name = "Player";
            SceneManager.MoveGameObjectToScene(player, destination);
            player.transform.SetParent(parent, true);
            player.transform.position = new Vector3(-21.5f, -4.0f, 0f);
            player.SetActive(true);
            return player;
        }

        private static void CreateCamera(Camera sourceCamera, Transform player, Scene destination, Transform parent)
        {
            GameObject cameraObject = sourceCamera != null
                ? UnityEngine.Object.Instantiate(sourceCamera.gameObject)
                : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.name = "Main Camera";
            SceneManager.MoveGameObjectToScene(cameraObject, destination);
            cameraObject.transform.SetParent(parent, true);
            cameraObject.transform.position = new Vector3(0f, 1.5f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera != null && sourceCamera == null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 7.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.012f, 0.035f, 0.07f, 1f);
            }

            CameraFollow follow = cameraObject.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = cameraObject.AddComponent<CameraFollow>();
            }

            follow.SetTarget(player);
        }

        private static void CreateLighting(Transform parent, Scene scene)
        {
            GameObject ambientEffects = CreateContainer("AmbientEffects", parent, scene);
            GameObject lightObject = new GameObject("Heart Chamber Ambient Light");
            lightObject.transform.SetParent(ambientEffects.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.28f, 0.58f, 0.85f, 1f);
            light.intensity = 0.28f;
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        }

        private static GameObject CreateContainer(string name, Transform parent, Scene scene)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            if (container.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(container, scene);
            }

            return container;
        }

        private static Sprite Structural(string spriteName)
        {
            return CaveSceneBuilderV1.LoadSprite(HeartChamberTileSheetImporter.StructuralSheetPath, spriteName);
        }

        private static Sprite Crystal(string spriteName)
        {
            return CaveSceneBuilderV1.LoadSprite(HeartChamberTileSheetImporter.CrystalWaterSheetPath, spriteName);
        }

        private static Sprite Require(Sprite sprite, string role)
        {
            if (sprite == null)
            {
                throw new InvalidOperationException("Heart Chamber needs the approved " + role + " sprite.");
            }

            return sprite;
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
    }
}
