using System;
using Cave.CameraSystem;
using Cave.Player;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>
    /// One functional Hollow Districts scaffold. It creates ordinary sprites, sparse Ground colliders,
    /// and future encounter markers only; it adds no faction runtime behavior.
    /// </summary>
    public static class HollowDistrictsSceneBuilder
    {
        public const string ScenePath = "Assets/Cave/Scenes/03_HollowDistricts.unity";
        private const string SourceScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";
        private const string TemporaryDeepVeinsTarget = "Cave_Level01";

        [MenuItem("Tools/Cave/Scene Builder/Build 03 Hollow Districts (New Scene Only)")]
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
            AssetDatabase.ImportAsset(HollowDistrictsTileSheetImporter.SheetPath, ImportAssetOptions.ForceUpdate);
            Scene hollow = CaveSceneBuilderV1.CreateNewScene(ScenePath);
            SceneManager.SetActiveScene(hollow);
            CaveSceneBuilderV1.RoomRoots roots = CaveSceneBuilderV1.CreateRoomRoot(hollow, "03_HollowDistricts");

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            try
            {
                PlayerHealth sourcePlayer = FindComponentInScene<PlayerHealth>(source);
                Camera sourceCamera = FindComponentInScene<Camera>(source);
                if (sourcePlayer == null)
                {
                    throw new InvalidOperationException("Sprint5_Hazards has no PlayerHealth to clone for 03_HollowDistricts.");
                }

                BuildEnvironment(roots);
                BuildGameplayMarkers(roots);
                GameObject player = ClonePlayer(sourcePlayer, hollow, roots.PlayerSpawn);
                CreateCamera(sourceCamera, player.transform, hollow, roots.Camera);
                CreateLighting(roots.LightingVfx);

                Physics2D.SyncTransforms();
                if (!CaveSceneBuilderV1.ValidateRoom(hollow, "HollowCatacombLandmark"))
                {
                    throw new InvalidOperationException("03_HollowDistricts validation failed; the scene was not saved.");
                }

                CaveSceneBuilderV1.SaveScene(hollow, ScenePath);
                Debug.Log("Created functional Hollow Districts scene at " + ScenePath + ". No mobs were placed.");
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(hollow);
            }
        }

        private static void BuildEnvironment(CaveSceneBuilderV1.RoomRoots roots)
        {
            Sprite floor = Require(Sprite("Hollow_IrregularFloor"), "irregular floor");
            Sprite shelf = Require(Sprite("Hollow_RockShelf"), "rock shelf");
            Sprite bridge = Require(Sprite("Hollow_BridgeSegment"), "bridge segment");
            Sprite stairs = Require(Sprite("Hollow_StairRamp"), "stair ramp");
            Sprite supports = Require(Sprite("Hollow_StoneSupports"), "stone supports");
            Sprite caveWall = Require(Sprite("Hollow_CaveWall"), "cave wall");
            Sprite catacombWall = Require(Sprite("Hollow_CatacombWall"), "catacomb wall");
            Sprite doorArch = Require(Sprite("Hollow_DoorArch"), "door arch");
            Sprite tunnel = Require(Sprite("Hollow_TunnelArch"), "tunnel arch");
            Sprite lava = Require(Sprite("Hollow_LavaLight"), "lava light");
            Sprite lanterns = Require(Sprite("Hollow_HangingLanterns"), "hanging lanterns");
            Sprite banners = Require(Sprite("Hollow_FactionBanners"), "faction banners");
            Sprite debris = Require(Sprite("Hollow_MiningDebris"), "mining debris");
            Sprite rubble = Require(Sprite("Hollow_BrokenRubble"), "broken rubble");
            Sprite crystals = Require(Sprite("Hollow_CrystalFlora"), "crystal flora");
            Sprite hallway = Require(Sprite("Hollow_TransitionHall"), "transition hallway");
            Sprite wizardRoom = Require(Sprite("Hollow_WizardRoom"), "Wizard room");
            Sprite detectiveRoom = Require(Sprite("Hollow_DetectiveRoom"), "Detective room");
            Sprite catacombHall = Require(Sprite("Hollow_CatacombHall"), "catacomb hall");

            // Dense, low-light cave framing stays behind the readable sparse collision route.
            CaveSceneBuilderV1.PlaceBackgroundSprite("Hollow Left Cave Depth", roots.Background, caveWall, new Vector2(-27f, 8f), new Vector3(5.0f, 5.0f, 1f), -40);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Hollow Central Catacomb Depth", roots.Background, catacombWall, new Vector2(0f, 8f), new Vector3(6.1f, 6.1f, 1f), -39);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Hollow Right Cave Depth", roots.Background, caveWall, new Vector2(27f, 7.5f), new Vector3(5.2f, 5.2f, 1f), -40);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Hollow Lower Catacomb Depth", roots.Background, catacombHall, new Vector2(1f, -10.5f), new Vector3(4.4f, 4.4f, 1f), -39);

            // Main floor: entry is broad and visually irregular, with only a few sparse combat ledges.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Hollow Entry Main Floor", roots.Terrain, floor, new Vector2(-27f, -1.5f), new Vector2(6.0f, 0.26f), new Vector3(3.2f, 3.2f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Hollow Main Floor West", roots.Terrain, floor, new Vector2(-12.5f, -1.3f), new Vector2(7.0f, 0.26f), new Vector3(3.5f, 3.5f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Hollow Main Floor East", roots.Terrain, floor, new Vector2(5.5f, -1.55f), new Vector2(7.0f, 0.26f), new Vector3(3.5f, 3.5f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Hollow Main Floor Exit", roots.Terrain, floor, new Vector2(22f, -1.35f), new Vector2(6.2f, 0.26f), new Vector3(3.1f, 3.1f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Main Floor West Shelf", roots.Terrain, shelf, new Vector2(-16.5f, 1.8f), new Vector2(2.8f, 0.25f), new Vector3(2.4f, 2.4f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Main Floor East Shelf", roots.Terrain, shelf, new Vector2(12.5f, 1.4f), new Vector2(2.9f, 0.25f), new Vector3(2.1f, 2.1f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Main Floor Lava Fissure West", roots.Props, lava, new Vector2(-6.0f, -2.0f), new Vector3(1.55f, 1.55f, 1f), 1);
            CaveSceneBuilderV1.PlaceProp("Main Floor Lava Fissure East", roots.Props, lava, new Vector2(13.5f, -2.05f), new Vector3(1.25f, 1.25f, 1f), 1);
            CaveSceneBuilderV1.PlaceProp("Main Floor Rubble", roots.Props, rubble, new Vector2(-1.5f, -0.8f), new Vector3(0.9f, 0.9f, 1f), 6);

            // Level 1 is deliberately broken into three crossings, never a single straight bridge.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge Level 1 West", roots.Terrain, bridge, new Vector2(-16f, 5.0f), new Vector2(3.7f, 0.24f), new Vector3(2.7f, 2.7f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge Level 1 Center", roots.Terrain, bridge, new Vector2(-3f, 6.0f), new Vector2(3.9f, 0.24f), new Vector3(2.8f, 2.8f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge Level 1 East", roots.Terrain, bridge, new Vector2(11f, 5.25f), new Vector2(3.5f, 0.24f), new Vector3(2.5f, 2.5f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge One Left Access", roots.Terrain, stairs, new Vector2(-21.5f, 0.7f), new Vector2(2.5f, 0.28f), new Vector3(2.2f, 2.2f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge One Right Access", roots.Terrain, stairs, new Vector2(17.5f, 0.5f), new Vector2(2.4f, 0.28f), new Vector3(-2.0f, 2.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Bridge One Supports", roots.Props, supports, new Vector2(-2.5f, 2.5f), new Vector3(2.0f, 2.0f, 1f), 1);
            CaveSceneBuilderV1.PlaceProp("Bridge One Lanterns", roots.Props, lanterns, new Vector2(4.0f, 5.2f), new Vector3(1.05f, 1.05f, 1f), 7);

            // Level 2 is higher, more exposed, and leads directly into the Wizard territory.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge Level 2 West", roots.Terrain, bridge, new Vector2(-7.5f, 10.6f), new Vector2(3.4f, 0.24f), new Vector3(2.45f, 2.45f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge Level 2 Center", roots.Terrain, bridge, new Vector2(5.0f, 11.7f), new Vector2(3.7f, 0.24f), new Vector3(2.65f, 2.65f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge Level 2 Wizard Approach", roots.Terrain, bridge, new Vector2(15.5f, 11.0f), new Vector2(2.8f, 0.24f), new Vector3(2.0f, 2.0f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Bridge Two Access", roots.Terrain, stairs, new Vector2(-11.8f, 7.0f), new Vector2(2.4f, 0.28f), new Vector3(2.05f, 2.05f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Bridge Two Ruined Support", roots.Props, supports, new Vector2(6.0f, 8.4f), new Vector3(1.75f, 1.75f, 1f), 1);
            CaveSceneBuilderV1.PlaceProp("Bridge Two Lava Seam", roots.Props, lava, new Vector2(9.8f, 9.2f), new Vector3(0.82f, 0.82f, 1f), 1);

            CaveSceneBuilderV1.PlaceWalkablePlatform("Wizard Room Floor", roots.Terrain, wizardRoom, new Vector2(22.5f, 12.5f), new Vector2(5.8f, 0.25f), new Vector3(2.55f, 2.55f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Wizard Room Arcane Chamber", roots.Props, wizardRoom, new Vector2(22.5f, 12.5f), new Vector3(2.55f, 2.55f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Wizard Room Purple Banners", roots.Props, banners, new Vector2(19.4f, 13.6f), new Vector3(1.0f, 1.0f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Wizard Crystal Flora", roots.Props, crystals, new Vector2(27.0f, 12.3f), new Vector3(1.0f, 1.0f, 1f), 8);

            // The lower descent reaches old Catacombs and Detective territory at the same bottom elevation.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Catacomb Descent", roots.Terrain, stairs, new Vector2(-19f, -5.2f), new Vector2(2.6f, 0.3f), new Vector3(2.25f, 2.25f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Catacomb Tunnel Descent", roots.Terrain, stairs, new Vector2(18.0f, -5.3f), new Vector2(2.5f, 0.3f), new Vector3(-2.15f, 2.15f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Catacomb Main Chamber Floor", roots.Terrain, catacombHall, new Vector2(-5.5f, -11.0f), new Vector2(4.5f, 0.22f), new Vector3(3.8f, 3.8f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Detective Room Floor", roots.Terrain, detectiveRoom, new Vector2(18.0f, -11.0f), new Vector2(4.5f, 0.22f), new Vector3(3.7f, 3.7f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Lower Catacomb Connector", roots.Terrain, tunnel, new Vector2(6.2f, -11.2f), new Vector2(4.5f, 0.22f), new Vector3(2.45f, 2.45f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Catacomb Ancient Hall", roots.Props, catacombHall, new Vector2(-5.5f, -11.0f), new Vector3(3.8f, 3.8f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Detective Controlled Room", roots.Props, detectiveRoom, new Vector2(18.0f, -11.0f), new Vector3(3.7f, 3.7f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Catacomb Lava Landmark", roots.Props, lava, new Vector2(4.5f, -11.4f), new Vector3(1.1f, 1.1f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Detective Cyan Crystals", roots.Props, crystals, new Vector2(13.5f, -10.5f), new Vector3(0.85f, 0.85f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Lower Tunnel Arch West", roots.Props, tunnel, new Vector2(-18.2f, -8.8f), new Vector3(1.55f, 1.55f, 1f), 5);
            CaveSceneBuilderV1.PlaceProp("Lower Tunnel Arch East", roots.Props, tunnel, new Vector2(9.5f, -9.0f), new Vector3(1.45f, 1.45f, 1f), 5);
            CaveSceneBuilderV1.PlaceProp("Catacomb Debris", roots.Props, debris, new Vector2(-13.5f, -10.8f), new Vector3(0.8f, 0.8f, 1f), 8);

            CaveSceneBuilderV1.PlaceWalkablePlatform("Deep Veins Hallway Floor", roots.Terrain, hallway, new Vector2(32.5f, -1.1f), new Vector2(3.5f, 0.24f), new Vector3(1.9f, 1.9f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Deep Veins Hallway Arch", roots.Props, doorArch, new Vector2(34.5f, -0.7f), new Vector3(1.85f, 1.85f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Deep Veins Lava Guidance", roots.Props, lava, new Vector2(29.6f, -1.1f), new Vector3(0.75f, 0.75f, 1f), 7);

            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Hollow Districts Left Boundary", roots.Terrain, new Vector2(-35.7f, 0f), new Vector2(0.6f, 30f));
            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Hollow Districts Right Boundary", roots.Terrain, new Vector2(38.5f, 0f), new Vector2(0.6f, 30f));
            CaveSceneBuilderV1.PlaceForegroundOccluder("Hollow Foreground Rubble", roots.Foreground, rubble, new Vector2(-1.0f, -13.3f), new Vector3(4.8f, 4.8f, 1f), 20);
        }

        private static void BuildGameplayMarkers(CaveSceneBuilderV1.RoomRoots roots)
        {
            CaveSceneBuilderV1.PlacePlayerSpawn("Hollow Districts Player Spawn", roots.PlayerSpawn, new Vector2(-29.5f, -0.55f), "FromStronghold");
            CaveSceneBuilderV1.PlaceInteractableMarker("Hollow Catacomb Landmark Marker", roots.Interactables, new Vector2(-5.5f, -9.9f), "HollowCatacombLandmark");
            CaveSceneBuilderV1.PlaceRoomTransitionMarker("Hollow Districts Deep Veins Exit", roots.Transitions, new Vector2(36.2f, -0.25f), "ToDeepVeins", TemporaryDeepVeinsTarget);
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Hollow Main Floor Spawn", roots.EnemySpawns, new Vector2(-7.0f, -0.35f), "HollowMainFloor01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Bridge One Spawn", roots.EnemySpawns, new Vector2(-2.5f, 6.6f), "HollowBridgeOne01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Bridge Two Spawn", roots.EnemySpawns, new Vector2(5.0f, 12.2f), "HollowBridgeTwo01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Wizard Room Spawn", roots.EnemySpawns, new Vector2(22.5f, 13.1f), "HollowWizardRoom01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Catacomb Spawn", roots.EnemySpawns, new Vector2(-5.5f, -9.9f), "HollowCatacombs01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Detective Room Spawn", roots.EnemySpawns, new Vector2(18.0f, -9.9f), "HollowDetectiveRoom01");
            CaveSceneBuilderV1.PlaceKillZone("Hollow Districts Fall Kill Zone", roots.Hazards, new Vector2(1.5f, -17.5f), new Vector2(82f, 2f));
            CaveSceneBuilderV1.SetCameraBounds("Hollow Districts Camera Bounds", roots.Camera, new Vector2(1.5f, 1.5f), new Vector2(82f, 39f), "HollowDistrictsCameraBounds");
        }

        private static GameObject ClonePlayer(PlayerHealth sourcePlayer, Scene destination, Transform parent)
        {
            GameObject player = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            player.name = "Player";
            SceneManager.MoveGameObjectToScene(player, destination);
            player.transform.SetParent(parent, true);
            player.transform.position = new Vector3(-29.5f, -0.55f, 0f);
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
            cameraObject.transform.position = new Vector3(-29.5f, 1.5f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera != null && sourceCamera == null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 5.2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.015f, 0.02f, 0.055f, 1f);
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
            GameObject lighting = new GameObject("Hollow Districts Molten Ambient Light");
            lighting.transform.SetParent(parent, false);
            Light light = lighting.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.64f, 0.25f, 0.11f, 1f);
            light.intensity = 0.32f;
            lighting.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static Sprite Sprite(string spriteName)
        {
            return CaveSceneBuilderV1.LoadSprite(HollowDistrictsTileSheetImporter.SheetPath, spriteName);
        }

        private static Sprite Require(Sprite sprite, string role)
        {
            if (sprite == null)
            {
                throw new InvalidOperationException("Hollow Districts needs the approved " + role + " sprite.");
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
