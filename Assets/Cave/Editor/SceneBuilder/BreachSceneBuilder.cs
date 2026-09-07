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
    /// <summary>Builds the authored first-outskirts room; it never populates enemy prefabs.</summary>
    public static class UpperCaveSceneBuilder
    {
        public const string UpperCaveScenePath = "Assets/Cave/Scenes/01_UpperCave.unity";
        private const string SourceScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";
        private const string UpperCaveTilesPath = "Assets/Cave/Art/Rooms/Breach/BreachTileSheet_Source.png";

        [MenuItem("Tools/Cave/Scene Builder/Build 01 Upper Cave (New Scene Only)")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        [MenuItem("Tools/Cave/Scene Builder/Validate Active Authored Room")]
        public static void ValidateActiveRoom()
        {
            CaveSceneBuilderV1.ValidateRoom(SceneManager.GetActiveScene(), "HiddenAlcoveChest");
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            Scene upperCave = CaveSceneBuilderV1.CreateNewScene(UpperCaveScenePath);
            SceneManager.SetActiveScene(upperCave);
            CaveSceneBuilderV1.RoomRoots roots = CaveSceneBuilderV1.CreateRoomRoot(upperCave, "01_UpperCave");

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            try
            {
                PlayerHealth sourcePlayer = FindComponentInScene<PlayerHealth>(source);
                Camera sourceCamera = FindComponentInScene<Camera>(source);
                if (sourcePlayer == null)
                {
                    throw new InvalidOperationException("Sprint5_Hazards has no PlayerHealth to clone for 01_UpperCave.");
                }

                BuildEnvironment(roots);
                BuildGameplayMarkers(roots);
                GameObject player = ClonePlayer(sourcePlayer, upperCave, roots.PlayerSpawn);
                CreateCamera(sourceCamera, player.transform, upperCave, roots.Camera);
                CreateLighting(roots.LightingVfx);

                Physics2D.SyncTransforms();
                if (!CaveSceneBuilderV1.ValidateRoom(upperCave, "HiddenAlcoveChest"))
                {
                    throw new InvalidOperationException("01_UpperCave validation failed; the scene was not saved.");
                }

                CaveSceneBuilderV1.SaveScene(upperCave, UpperCaveScenePath);
                Debug.Log("Created authored Upper Cave scene at " + UpperCaveScenePath + ". No mobs were placed.");
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(upperCave);
            }
        }

        private static void BuildEnvironment(CaveSceneBuilderV1.RoomRoots roots)
        {
            // Every environment visual in this room comes from the approved Upper Cave source sheet.
            Sprite caveDepthWarm = Require(UpperCaveSprite("Breach_CaveDepthWarm"), "warm cave depth");
            Sprite caveDepthBlue = Require(UpperCaveSprite("Breach_CaveDepthBlue"), "blue cave depth");
            Sprite caveDepthFar = Require(UpperCaveSprite("Breach_CaveDepthFar"), "far cave depth");
            Sprite entryTunnel = Require(UpperCaveSprite("Breach_EntryTunnel"), "entry tunnel");
            Sprite entryTunnelChest = Require(UpperCaveSprite("Breach_EntryTunnelChest"), "upper chest alcove");
            Sprite rockLarge = Require(UpperCaveSprite("Breach_RockPlatformLarge"), "large rock platform");
            Sprite rockCenter = Require(UpperCaveSprite("Breach_RockPlatformCenter"), "center rock platform");
            Sprite rockSmallA = Require(UpperCaveSprite("Breach_RockPlatformSmallA"), "small rock platform");
            Sprite rockSmallB = Require(UpperCaveSprite("Breach_RockPlatformSmallB"), "second small rock platform");
            Sprite rockSmallC = Require(UpperCaveSprite("Breach_RockPlatformSmallC"), "upper small rock platform");
            Sprite rockRamp = Require(UpperCaveSprite("Breach_RockRamp"), "rock ramp");
            Sprite bridge = Require(UpperCaveSprite("Breach_Bridge"), "bridge");
            Sprite campTent = Require(UpperCaveSprite("Breach_CampTent"), "camp tent");
            Sprite cityGate = Require(UpperCaveSprite("Breach_CityGate"), "city gate");
            Sprite scaffoldTall = Require(UpperCaveSprite("Breach_ScaffoldTall"), "tall mining scaffold");
            Sprite scaffoldLower = Require(UpperCaveSprite("Breach_ScaffoldLower"), "lower mining scaffold");
            Sprite oreCart = Require(UpperCaveSprite("Breach_OreCart"), "ore cart");
            Sprite miningCrane = Require(UpperCaveSprite("Breach_MiningCrane"), "mining crane");
            Sprite mineCartPile = Require(UpperCaveSprite("Breach_MineCartPile"), "mine cart pile");
            Sprite lanternPost = Require(UpperCaveSprite("Breach_LanternPost"), "lantern post");
            Sprite brazier = Require(UpperCaveSprite("Breach_Brazier"), "brazier");
            Sprite ropeFence = Require(UpperCaveSprite("Breach_RopeFence"), "rope fence");
            Sprite banner = Require(UpperCaveSprite("Breach_Banner"), "banner");
            Sprite crate = Require(UpperCaveSprite("Breach_Crate"), "crate");
            Sprite barrel = Require(UpperCaveSprite("Breach_Barrel"), "barrel");
            Sprite workbench = Require(UpperCaveSprite("Breach_MiningWorkbench"), "mining workbench");
            Sprite redCrystal = Require(UpperCaveSprite("Breach_RedCrystalLarge"), "red crystal growth");
            Sprite blueCrystal = Require(UpperCaveSprite("Breach_BlueCrystalLarge"), "blue crystal growth");
            Sprite stalagmite = Require(UpperCaveSprite("Breach_StalagmiteTall"), "stalagmite");
            Sprite foregroundStalagmites = Require(UpperCaveSprite("Breach_ForegroundStalagmites"), "foreground stalagmites");

            // The reference opens from a warm left tunnel into cool, distant depth and a fortified right-hand gate.
            CaveSceneBuilderV1.PlaceBackgroundSprite("Warm Upper Cave Depth", roots.Background, caveDepthWarm, new Vector2(-7f, 1.7f), new Vector3(7.8f, 7.8f, 1f), -40);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Blue Cavern Depth", roots.Background, caveDepthBlue, new Vector2(8f, 1.8f), new Vector3(9.4f, 9.4f, 1f), -39);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Far City Depth", roots.Background, caveDepthFar, new Vector2(21f, 2.8f), new Vector3(7.4f, 7.4f, 1f), -38);

            // Entry tunnel and upper alcove are intentionally separate routes, not a painted backdrop.
            CaveSceneBuilderV1.PlaceProp("Upper Cave Entry Tunnel", roots.Props, entryTunnel, new Vector2(-19.2f, -2.8f), new Vector3(2.3f, 2.3f, 1f), 1);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Entry Tunnel Floor", roots.Terrain, rockCenter, new Vector2(-18f, -5.2f), new Vector2(8.2f, 0.9f), new Vector3(4.8f, 4.8f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Alcove Step One", roots.Terrain, rockSmallA, new Vector2(-14.2f, -3.3f), new Vector2(4.2f, 0.65f), new Vector3(3.3f, 3.3f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Alcove Step Two", roots.Terrain, rockSmallB, new Vector2(-16.7f, -1.2f), new Vector2(4.4f, 0.65f), new Vector3(3.1f, 3.1f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Alcove Step Three", roots.Terrain, rockSmallC, new Vector2(-19f, 1.3f), new Vector2(4.2f, 0.65f), new Vector3(2.9f, 2.9f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Hidden Upper Alcove", roots.Terrain, entryTunnelChest, new Vector2(-18f, 4f), new Vector2(8.6f, 0.8f), new Vector3(2.15f, 2.15f, 1f), 2);
            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Upper Cave Left Boundary", roots.Terrain, new Vector2(-22.8f, 0f), new Vector2(0.75f, 13f));
            // Camp forms the safe midpoint before the bridge; its deck is walkable rather than merely decorative.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Camp Ledge", roots.Terrain, rockLarge, new Vector2(-5f, -2.1f), new Vector2(11.5f, 1.05f), new Vector3(4.3f, 4.3f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Mining Camp Tent", roots.Props, campTent, new Vector2(-5.3f, -1.45f), new Vector3(2.3f, 2.3f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Camp Lantern Post", roots.Props, lanternPost, new Vector2(-8.5f, -1.35f), new Vector3(1.35f, 1.35f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Camp Crate", roots.Props, crate, new Vector2(-2.5f, -1.2f), new Vector3(1.15f, 1.15f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Camp Barrel", roots.Props, barrel, new Vector2(-3.7f, -1.2f), new Vector3(1.05f, 1.05f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Camp Banner", roots.Props, banner, new Vector2(-7.6f, 0.2f), new Vector3(1.05f, 1.05f, 1f), 8);

            CaveSceneBuilderV1.PlaceWalkablePlatform("Suspension Bridge", roots.Terrain, bridge, new Vector2(6f, -0.9f), new Vector2(10.5f, 0.75f), new Vector3(2.95f, 2.95f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Bridge Foreground Rope", roots.Props, ropeFence, new Vector2(6f, -0.12f), new Vector3(3.35f, 3.35f, 1f), 9);
            CaveSceneBuilderV1.PlaceProp("Bridge Rear Rope", roots.Props, ropeFence, new Vector2(6f, -1.95f), new Vector3(3.35f, 3.35f, 1f), 1);

            // Gate lies beyond the bridge on a raised city-facing shelf.
            CaveSceneBuilderV1.PlaceWalkablePlatform("City Gate Shelf", roots.Terrain, rockCenter, new Vector2(16.8f, -0.1f), new Vector2(10.5f, 0.95f), new Vector3(5.8f, 5.8f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Gate Approach Step", roots.Terrain, rockSmallA, new Vector2(11.5f, -0.55f), new Vector2(4.6f, 0.7f), new Vector3(3.3f, 3.3f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Fortified City Gate", roots.Props, cityGate, new Vector2(20.5f, 0.45f), new Vector3(2.5f, 2.5f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Gate Banner", roots.Props, banner, new Vector2(17f, 1.0f), new Vector3(1.2f, 1.2f, 1f), 9);
            CaveSceneBuilderV1.PlaceProp("Gate Brazier Left", roots.Props, brazier, new Vector2(18.4f, 0.65f), new Vector3(1.1f, 1.1f, 1f), 9);
            CaveSceneBuilderV1.PlaceProp("Gate Brazier Right", roots.Props, brazier, new Vector2(22.6f, 0.65f), new Vector3(1.1f, 1.1f, 1f), 9);
            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("City Gate Boundary", roots.Terrain, new Vector2(24.2f, 1.8f), new Vector2(0.8f, 8f));

            // Mining descends below the gate shelf on its own connected, prop-dense work area.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Mine Ramp", roots.Terrain, rockRamp, new Vector2(13.8f, -2.35f), new Vector2(7.4f, 0.85f), new Vector3(3.0f, 3.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Mine Descent Step One", roots.Terrain, rockSmallB, new Vector2(17.2f, -2.8f), new Vector2(4.6f, 0.7f), new Vector3(2.4f, 2.4f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Mine Descent Step Two", roots.Terrain, rockSmallC, new Vector2(19.3f, -3.75f), new Vector2(4.4f, 0.7f), new Vector3(2.35f, 2.35f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Lower Mining Platform", roots.Terrain, rockLarge, new Vector2(17.4f, -5.5f), new Vector2(12f, 1.1f), new Vector3(4.5f, 4.5f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Mine Edge Platform", roots.Terrain, rockSmallA, new Vector2(23.1f, -3.4f), new Vector2(5.1f, 0.75f), new Vector3(3.7f, 3.7f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Tall Mining Scaffold", roots.Props, scaffoldTall, new Vector2(17.5f, -2.8f), new Vector3(2.25f, 2.25f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Lower Mining Scaffold", roots.Props, scaffoldLower, new Vector2(21.5f, -4.25f), new Vector3(1.9f, 1.9f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Mining Crane", roots.Props, miningCrane, new Vector2(14.3f, -3.25f), new Vector3(1.65f, 1.65f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Mining Ore Cart", roots.Props, oreCart, new Vector2(15.4f, -4.55f), new Vector3(1.35f, 1.35f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Abandoned Mine Cart", roots.Props, mineCartPile, new Vector2(22.2f, -4.95f), new Vector3(1.25f, 1.25f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Mining Workbench", roots.Props, workbench, new Vector2(19.8f, -4.5f), new Vector3(1.15f, 1.15f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Blue Crystal Vein", roots.Props, blueCrystal, new Vector2(22.6f, -2.25f), new Vector3(1.35f, 1.35f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Red Crystal Vein", roots.Props, redCrystal, new Vector2(23.5f, -5.0f), new Vector3(1.1f, 1.1f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Mine Lantern Post", roots.Props, lanternPost, new Vector2(13.2f, -3.25f), new Vector3(1.25f, 1.25f, 1f), 8);

            CaveSceneBuilderV1.PlaceForegroundOccluder("Entry Foreground Stalagmites", roots.Foreground, foregroundStalagmites, new Vector2(-18f, -6.2f), new Vector3(2.8f, 2.8f, 1f), 20);
            CaveSceneBuilderV1.PlaceForegroundOccluder("Camp Foreground Stalagmites", roots.Foreground, stalagmite, new Vector2(-10.5f, -4.8f), new Vector3(2.3f, 2.3f, 1f), 20);
            CaveSceneBuilderV1.PlaceForegroundOccluder("Mine Foreground Stalagmites", roots.Foreground, foregroundStalagmites, new Vector2(17.5f, -6.3f), new Vector3(2.6f, 2.6f, 1f), 20);
        }

        private static void BuildGameplayMarkers(CaveSceneBuilderV1.RoomRoots roots)
        {
            CaveSceneBuilderV1.PlacePlayerSpawn("Upper Cave Player Spawn", roots.PlayerSpawn, new Vector2(-18f, -4.25f), "UpperCaveEntrance");
            CaveSceneBuilderV1.PlaceInteractableMarker("Hidden Alcove Chest Marker", roots.Interactables, new Vector2(-18f, 4.6f), "HiddenAlcoveChest");
            CaveSceneBuilderV1.PlaceRoomTransitionMarker("City Gate Transition Marker", roots.Transitions, new Vector2(23.35f, 0.4f), "KingdomGate", "Cave_Level01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Brute Mining Spawn 01", roots.EnemySpawns, new Vector2(16f, -4.5f), "UpperCaveMiningBrute01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Brute Mining Spawn 02", roots.EnemySpawns, new Vector2(19f, -4.5f), "UpperCaveMiningBrute02");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Wizard Lead Spawn", roots.EnemySpawns, new Vector2(22f, -3.4f), "UpperCaveMiningWizard");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Detective Alcove Spawn 01", roots.EnemySpawns, new Vector2(-17f, 4.6f), "UpperCaveAlcoveDetective01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Detective Alcove Spawn 02", roots.EnemySpawns, new Vector2(-19.1f, 4.6f), "UpperCaveAlcoveDetective02");
            CaveSceneBuilderV1.PlaceKillZone("Upper Cave Fall Kill Zone", roots.Hazards, new Vector2(2f, -11f), new Vector2(56f, 2f));
            CaveSceneBuilderV1.SetCameraBounds("Upper Cave Camera Bounds", roots.Camera, new Vector2(2f, 0f), new Vector2(52f, 19f), "UpperCaveCameraBounds");
        }

        private static GameObject ClonePlayer(PlayerHealth sourcePlayer, Scene upperCave, Transform parent)
        {
            GameObject player = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            player.name = "Player";
            SceneManager.MoveGameObjectToScene(player, upperCave);
            player.transform.SetParent(parent, true);
            player.transform.position = new Vector3(-18f, -4.25f, 0f);
            player.SetActive(true);
            return player;
        }

        private static void CreateCamera(Camera sourceCamera, Transform player, Scene upperCave, Transform parent)
        {
            GameObject cameraObject = sourceCamera != null
                ? UnityEngine.Object.Instantiate(sourceCamera.gameObject)
                : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.name = "Main Camera";
            SceneManager.MoveGameObjectToScene(cameraObject, upperCave);
            cameraObject.transform.SetParent(parent, true);
            cameraObject.transform.position = new Vector3(-18f, -2f, -10f);
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
            GameObject lighting = new GameObject("Upper Cave Ambient Light");
            lighting.transform.SetParent(parent, false);
            Light light = lighting.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.36f, 0.48f, 0.74f, 1f);
            light.intensity = 0.25f;
            lighting.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static Sprite UpperCaveSprite(string spriteName)
        {
            return CaveSceneBuilderV1.LoadSprite(UpperCaveTilesPath, spriteName);
        }

        private static Sprite Require(Sprite sprite, string role)
        {
            if (sprite == null)
            {
                throw new InvalidOperationException("Upper Cave needs the approved " + role + " sprite.");
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
