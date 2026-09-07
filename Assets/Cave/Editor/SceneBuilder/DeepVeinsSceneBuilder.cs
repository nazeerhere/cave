using System;
using Cave.CameraSystem;
using Cave.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>
    /// Creates one functional Deep Veins traversal scaffold from the approved source sheet.
    /// The recipe places ordinary sprites, sparse Ground collision, and future encounter markers only.
    /// </summary>
    public static class DeepVeinsSceneBuilder
    {
        public const string ScenePath = "Assets/Cave/Scenes/04_DeepVeins.unity";
        private const string SourceScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";
        // A final-boss scene is not authored yet. Keep this marker valid and conspicuous for retargeting.
        private const string TemporaryFinalBossTarget = "Cave_Level01";

        [MenuItem("Tools/Cave/Scene Builder/Build 04 Deep Veins (New Scene Only)")]
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
            AssetDatabase.ImportAsset(DeepVeinsTileSheetImporter.SheetPath, ImportAssetOptions.ForceUpdate);
            Scene veins = CaveSceneBuilderV1.CreateNewScene(ScenePath);
            SceneManager.SetActiveScene(veins);
            CaveSceneBuilderV1.RoomRoots roots = CaveSceneBuilderV1.CreateRoomRoot(veins, "04_DeepVeins");

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            try
            {
                PlayerHealth sourcePlayer = FindComponentInScene<PlayerHealth>(source);
                Camera sourceCamera = FindComponentInScene<Camera>(source);
                if (sourcePlayer == null)
                {
                    throw new InvalidOperationException("Sprint5_Hazards has no PlayerHealth to clone for 04_DeepVeins.");
                }

                BuildEnvironment(roots);
                BuildGameplayMarkers(roots);
                GameObject player = ClonePlayer(sourcePlayer, veins, roots.PlayerSpawn);
                CreateCamera(sourceCamera, player.transform, veins, roots.Camera);
                CreateLighting(roots.LightingVfx);

                Physics2D.SyncTransforms();
                if (!CaveSceneBuilderV1.ValidateRoom(veins, "HeartThresholdLandmark"))
                {
                    throw new InvalidOperationException("04_DeepVeins validation failed; the scene was not saved.");
                }

                CaveSceneBuilderV1.SaveScene(veins, ScenePath);
                Debug.Log("Created functional Deep Veins scene at " + ScenePath + ". No boss or mobs were placed.");
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(veins);
            }
        }

        private static void BuildEnvironment(CaveSceneBuilderV1.RoomRoots roots)
        {
            Sprite tile = Require(Sprite("Veins_AncientTileBand"), "ancient tile band");
            Sprite platform = Require(Sprite("Veins_BrokenPlatform"), "broken platform");
            Sprite stairs = Require(Sprite("Veins_RuinStairs"), "ruin stairs");
            Sprite arch = Require(Sprite("Veins_ArchAndPillars"), "arches and pillars");
            Sprite facade = Require(Sprite("Veins_AncientRuinFacade"), "ancient ruin facade");
            Sprite crystalLedge = Require(Sprite("Veins_CrystalLedge"), "crystal ledge");
            Sprite lavaEarth = Require(Sprite("Veins_LavaAndEarth"), "lava and earth");
            Sprite ruinDepth = Require(Sprite("Veins_RuinDepth"), "ruin depth");
            Sprite caveFar = Require(Sprite("Veins_CaveFarBackground"), "far cave background");
            Sprite ruinsFar = Require(Sprite("Veins_RuinFarBackground"), "far ruin background");
            Sprite crystalsFar = Require(Sprite("Veins_CrystalLavaBackground"), "crystal and lava background");
            Sprite foregroundVeins = Require(Sprite("Veins_ForegroundVeins"), "foreground veins");
            Sprite monument = Require(Sprite("Veins_AncientMonument"), "ancient monument");
            Sprite crystalProps = Require(Sprite("Veins_CrystalProps"), "crystal props");
            Sprite lavaPool = Require(Sprite("Veins_LavaPool"), "lava pool");
            Sprite lavafall = Require(Sprite("Veins_Lavafall"), "lavafall");
            Sprite lanterns = Require(Sprite("Veins_HangingLanterns"), "hanging lanterns");
            Sprite rubble = Require(Sprite("Veins_Rubble"), "rubble");

            // Background only: this unifies the room without turning empty space into traversal terrain.
            CaveSceneBuilderV1.PlaceBackgroundSprite("Deep Veins Left Cave Depth", roots.Background, caveFar, new Vector2(-27f, 8f), new Vector3(8f, 8f, 1f), -42);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Deep Veins Central Ancient Depth", roots.Background, ruinDepth, new Vector2(0f, 5f), new Vector3(7.7f, 7.7f, 1f), -41);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Deep Veins Left Ruin Depth", roots.Background, ruinsFar, new Vector2(-21f, 4f), new Vector3(7.5f, 7.5f, 1f), -40);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Deep Veins Right Crystal Depth", roots.Background, crystalsFar, new Vector2(22f, 4f), new Vector3(7.5f, 7.5f, 1f), -40);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Deep Veins Lower Cave Depth", roots.Background, caveFar, new Vector2(0f, -13.5f), new Vector3(10.5f, 10.5f, 1f), -42);

            // Central start/hub: broad enough for immediate left/right combat choices, with an ancient landmark behind it.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Central Hub West", roots.Terrain, tile, new Vector2(-8f, 0f), new Vector2(5.7f, 0.28f), new Vector3(3.5f, 3.5f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Central Hub Start", roots.Terrain, tile, new Vector2(0f, 0.1f), new Vector2(5.8f, 0.28f), new Vector3(3.6f, 3.6f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Central Hub East", roots.Terrain, tile, new Vector2(8f, 0f), new Vector2(5.7f, 0.28f), new Vector3(3.5f, 3.5f, 1f), 2);
            // These two low connectors make the initial left/right choice physically dependable,
            // while the higher broken span remains the second cross-map route.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Hub Fractured Connector", roots.Terrain, tile, new Vector2(-15.5f, 0.15f), new Vector2(4.2f, 0.28f), new Vector3(2.0f, 2.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Hub Fractured Connector", roots.Terrain, tile, new Vector2(15.5f, 0.15f), new Vector2(4.2f, 0.28f), new Vector3(-2.0f, 2.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Central Hub Ancient Landmark", roots.Props, facade, new Vector2(0f, 3.0f), new Vector3(3.3f, 3.3f, 1f), 5);
            CaveSceneBuilderV1.PlaceProp("Central Hub Crystal Shrine", roots.Props, crystalProps, new Vector2(0f, 1.2f), new Vector3(1.45f, 1.45f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Central Hub Lava Seams", roots.Props, lavaPool, new Vector2(10.2f, -0.55f), new Vector3(1.3f, 1.3f, 1f), 7);

            // Left route: a fractured upper chamber returns to the hub through both ground and elevated links.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Ruin Combat Floor", roots.Terrain, platform, new Vector2(-23f, 1f), new Vector2(6.2f, 0.28f), new Vector3(4.1f, 4.1f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Ruin Upper Shelf", roots.Terrain, crystalLedge, new Vector2(-25.0f, 8.4f), new Vector2(4.5f, 0.26f), new Vector3(3.0f, 3.0f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left To Upper Crossway", roots.Terrain, stairs, new Vector2(-16.5f, 4.5f), new Vector2(2.6f, 0.26f), new Vector3(2.6f, 2.6f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Left Ruin Monument", roots.Props, monument, new Vector2(-27f, 2.1f), new Vector3(2.0f, 2.0f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Left Crystal Growth", roots.Props, crystalProps, new Vector2(-20.2f, 1.3f), new Vector3(1.25f, 1.25f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Left Lavafall", roots.Props, lavafall, new Vector2(-30f, 3.5f), new Vector3(1.25f, 1.25f, 1f), 6);

            // Right route: a larger lava-cut ruin mirrors the opportunity but not the silhouette or traversal details.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Ruin Combat Floor", roots.Terrain, platform, new Vector2(23f, 1.2f), new Vector2(6.2f, 0.28f), new Vector3(-4.1f, 4.1f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Ruin Upper Shelf", roots.Terrain, lavaEarth, new Vector2(25.5f, 8.5f), new Vector2(4.3f, 0.26f), new Vector3(2.8f, 2.8f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right To Upper Crossway", roots.Terrain, stairs, new Vector2(16.5f, 4.7f), new Vector2(2.6f, 0.26f), new Vector3(-2.6f, 2.6f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Right Ruin Arch", roots.Props, arch, new Vector2(25f, 2.4f), new Vector3(2.5f, 2.5f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Right Lava Pool", roots.Props, lavaPool, new Vector2(19.5f, 0.4f), new Vector3(1.6f, 1.6f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Right Lavafall", roots.Props, lavafall, new Vector2(31f, 4f), new Vector3(1.3f, 1.3f, 1f), 6);

            // Second meaningful cross-map connection: an exposed broken span above the central hub.
            CaveSceneBuilderV1.PlaceWalkablePlatform("High Crossway West", roots.Terrain, platform, new Vector2(-11.5f, 8.0f), new Vector2(3.8f, 0.26f), new Vector3(2.6f, 2.6f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("High Crossway Center", roots.Terrain, platform, new Vector2(0f, 9.0f), new Vector2(4.0f, 0.26f), new Vector3(2.75f, 2.75f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("High Crossway East", roots.Terrain, platform, new Vector2(11.5f, 8.0f), new Vector2(3.8f, 0.26f), new Vector3(-2.6f, 2.6f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("High Crossway Pillars", roots.Props, arch, new Vector2(0f, 7.8f), new Vector3(2.0f, 2.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("High Crossway Lanterns", roots.Props, lanterns, new Vector2(5.8f, 8.2f), new Vector3(1.0f, 1.0f, 1f), 8);

            // Forsaken Hold is central and lower: an old ceremonial landmark with a broad combat floor.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Forsaken Hold West Floor", roots.Terrain, tile, new Vector2(-8f, -6.3f), new Vector2(5.7f, 0.28f), new Vector3(3.5f, 3.5f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Forsaken Hold Central Floor", roots.Terrain, tile, new Vector2(0f, -6.2f), new Vector2(5.9f, 0.28f), new Vector3(3.6f, 3.6f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Forsaken Hold East Floor", roots.Terrain, tile, new Vector2(8f, -6.3f), new Vector2(5.7f, 0.28f), new Vector3(3.5f, 3.5f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Hold Descent", roots.Terrain, stairs, new Vector2(-14f, -3.2f), new Vector2(2.5f, 0.27f), new Vector3(2.5f, 2.5f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Hold Descent", roots.Terrain, stairs, new Vector2(14f, -3.2f), new Vector2(2.5f, 0.27f), new Vector3(-2.5f, 2.5f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Forsaken Hold Facade", roots.Props, facade, new Vector2(0f, -3.2f), new Vector3(4.7f, 4.7f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Forsaken Hold Monument", roots.Props, monument, new Vector2(0f, -5.0f), new Vector3(2.0f, 2.0f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Forsaken Hold Crystal Fracture", roots.Props, crystalLedge, new Vector2(-10f, -5.9f), new Vector3(1.2f, 1.2f, 1f), 7);

            // Distinct lower descents: left is collapsed/crystal-heavy, right is consumed by lava, then they converge.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Collapsed Descent One", roots.Terrain, crystalLedge, new Vector2(-15.5f, -10.0f), new Vector2(3.5f, 0.26f), new Vector3(2.3f, 2.3f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Collapsed Descent Two", roots.Terrain, stairs, new Vector2(-8.5f, -13.4f), new Vector2(2.6f, 0.27f), new Vector3(-2.45f, 2.45f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Left Descent Crystal Cluster", roots.Props, crystalProps, new Vector2(-16f, -9.3f), new Vector3(1.4f, 1.4f, 1f), 7);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Melting Descent One", roots.Terrain, lavaEarth, new Vector2(15.5f, -10.0f), new Vector2(3.5f, 0.26f), new Vector3(-2.3f, 2.3f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Melting Descent Two", roots.Terrain, stairs, new Vector2(8.5f, -13.4f), new Vector2(2.6f, 0.27f), new Vector3(2.45f, 2.45f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Right Descent Lavafall", roots.Props, lavafall, new Vector2(16f, -9.0f), new Vector3(1.4f, 1.4f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Right Descent Lava Pool", roots.Props, lavaPool, new Vector2(11f, -12.5f), new Vector3(1.2f, 1.2f, 1f), 8);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Melting Descent Convergence", roots.Terrain, lavaEarth, new Vector2(0f, -15.8f), new Vector2(5.4f, 0.28f), new Vector3(3.5f, 3.5f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Melting Descent Cave Body", roots.Props, lavaEarth, new Vector2(0f, -14.5f), new Vector3(3.2f, 3.2f, 1f), 6);

            // Small terminal chamber only: the marker is deliberately reachable on its wide threshold floor.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Heart Threshold Floor", roots.Terrain, tile, new Vector2(0f, -20.0f), new Vector2(5.8f, 0.3f), new Vector3(3.8f, 3.8f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Heart Threshold Ancient Gate", roots.Props, facade, new Vector2(0f, -17.2f), new Vector3(3.8f, 3.8f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Heart Threshold Crystal", roots.Props, crystalProps, new Vector2(0f, -18.8f), new Vector3(1.8f, 1.8f, 1f), 9);
            CaveSceneBuilderV1.PlaceProp("Heart Threshold Lava", roots.Props, lavaPool, new Vector2(-6f, -20.4f), new Vector3(1.3f, 1.3f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Heart Threshold Rubble", roots.Props, rubble, new Vector2(5.5f, -20.0f), new Vector3(1.2f, 1.2f, 1f), 8);

            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Deep Veins Left Boundary", roots.Terrain, new Vector2(-36f, -4f), new Vector2(0.6f, 38f));
            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Deep Veins Right Boundary", roots.Terrain, new Vector2(36f, -4f), new Vector2(0.6f, 38f));
            CaveSceneBuilderV1.PlaceForegroundOccluder("Deep Veins Foreground Crystal Veins", roots.Foreground, foregroundVeins, new Vector2(0f, -22.0f), new Vector3(8.0f, 8.0f, 1f), 20);
        }

        private static void BuildGameplayMarkers(CaveSceneBuilderV1.RoomRoots roots)
        {
            CaveSceneBuilderV1.PlacePlayerSpawn("Deep Veins Player Spawn", roots.PlayerSpawn, new Vector2(0f, 0.8f), "FromHollowDistricts");
            CaveSceneBuilderV1.PlaceInteractableMarker("Heart Threshold Landmark Marker", roots.Interactables, new Vector2(0f, -18.7f), "HeartThresholdLandmark");
            CaveSceneBuilderV1.PlaceRoomTransitionMarker("Deep Veins Final Boss Exit", roots.Transitions, new Vector2(0f, -19.0f), "ToFinalBoss", TemporaryFinalBossTarget);
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Deep Veins Central Spawn", roots.EnemySpawns, new Vector2(-3f, 0.7f), "DeepVeinsCentral01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Deep Veins Left Ruin Spawn", roots.EnemySpawns, new Vector2(-23f, 2.1f), "DeepVeinsLeftRuin01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Deep Veins Right Ruin Spawn", roots.EnemySpawns, new Vector2(23f, 2.2f), "DeepVeinsRightRuin01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Forsaken Hold Spawn", roots.EnemySpawns, new Vector2(0f, -5.4f), "DeepVeinsForsakenHold01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Lower Descent Spawn", roots.EnemySpawns, new Vector2(0f, -15.0f), "DeepVeinsMeltingDescent01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Heart Threshold Spawn", roots.EnemySpawns, new Vector2(0f, -19.1f), "DeepVeinsHeartThreshold01");
            CaveSceneBuilderV1.PlaceKillZone("Deep Veins Fall Kill Zone", roots.Hazards, new Vector2(0f, -26.0f), new Vector2(80f, 2f));
            CaveSceneBuilderV1.SetCameraBounds("Deep Veins Camera Bounds", roots.Camera, new Vector2(0f, -5f), new Vector2(78f, 54f), "DeepVeinsCameraBounds");
        }

        private static GameObject ClonePlayer(PlayerHealth sourcePlayer, Scene destination, Transform parent)
        {
            GameObject player = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            player.name = "Player";
            SceneManager.MoveGameObjectToScene(player, destination);
            player.transform.SetParent(parent, true);
            player.transform.position = new Vector3(0f, 0.8f, 0f);
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
            cameraObject.transform.position = new Vector3(0f, -1f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera != null && sourceCamera == null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 5.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.012f, 0.05f, 1f);
            }

            CameraFollow follow = cameraObject.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = cameraObject.AddComponent<CameraFollow>();
            }

            follow.SetTarget(player);
        }

        private static void CreateLighting(Transform parent)
        {
            GameObject lighting = new GameObject("Deep Veins Molten Crystal Ambient Light");
            lighting.transform.SetParent(parent, false);
            Light light = lighting.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.66f, 0.22f, 0.45f, 1f);
            light.intensity = 0.32f;
            lighting.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
        }

        private static Sprite Sprite(string spriteName)
        {
            return CaveSceneBuilderV1.LoadSprite(DeepVeinsTileSheetImporter.SheetPath, spriteName);
        }

        private static Sprite Require(Sprite sprite, string role)
        {
            if (sprite == null)
            {
                throw new InvalidOperationException("Deep Veins needs the approved " + role + " sprite.");
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
