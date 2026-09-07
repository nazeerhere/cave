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
    /// Builds one functional, deliberately sparse-collision Stronghold pass from the approved sheets.
    /// This editor-only recipe never populates enemy prefabs or changes shared gameplay systems.
    /// </summary>
    public static class StrongholdSceneBuilder
    {
        public const string StrongholdScenePath = "Assets/Cave/Scenes/02_Stronghold.unity";
        private const string SourceScenePath = "Assets/Cave/Scenes/Sprint5_Hazards.unity";

        [MenuItem("Tools/Cave/Scene Builder/Build 02 Stronghold (New Scene Only)")]
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
            Scene stronghold = CaveSceneBuilderV1.CreateNewScene(StrongholdScenePath);
            SceneManager.SetActiveScene(stronghold);
            CaveSceneBuilderV1.RoomRoots roots = CaveSceneBuilderV1.CreateRoomRoot(stronghold, "02_Stronghold");

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            try
            {
                PlayerHealth sourcePlayer = FindComponentInScene<PlayerHealth>(source);
                Camera sourceCamera = FindComponentInScene<Camera>(source);
                if (sourcePlayer == null)
                {
                    throw new InvalidOperationException("Sprint5_Hazards has no PlayerHealth to clone for 02_Stronghold.");
                }

                BuildEnvironment(roots);
                BuildGameplayMarkers(roots);
                GameObject player = ClonePlayer(sourcePlayer, stronghold, roots.PlayerSpawn);
                CreateCamera(sourceCamera, player.transform, stronghold, roots.Camera);
                CreateLighting(roots.LightingVfx);

                Physics2D.SyncTransforms();
                if (!CaveSceneBuilderV1.ValidateRoom(stronghold, "StrongholdPitArena"))
                {
                    throw new InvalidOperationException("02_Stronghold validation failed; the scene was not saved.");
                }

                CaveSceneBuilderV1.SaveScene(stronghold, StrongholdScenePath);
                Debug.Log("Created functional Stronghold scene at " + StrongholdScenePath + ". No mobs were placed.");
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(stronghold);
            }
        }

        private static void BuildEnvironment(CaveSceneBuilderV1.RoomRoots roots)
        {
            Sprite caveWall = Require(Structural("Stronghold_CaveWall"), "cave wall");
            Sprite fortifiedWall = Require(Structural("Stronghold_FortifiedWall"), "fortified wall");
            Sprite barracks = Require(Structural("Stronghold_BarracksBlock"), "barracks block");
            Sprite stoneFloor = Require(Structural("Stronghold_StoneFloor"), "stone floor");
            Sprite stoneLedge = Require(Structural("Stronghold_StoneLedge"), "stone ledge");
            Sprite stairRise = Require(Structural("Stronghold_StairRise"), "stair rise");
            Sprite marketDeck = Require(Structural("Stronghold_MarketDeck"), "market deck");
            Sprite royalHall = Require(Structural("Stronghold_RoyalHall"), "royal hall");
            Sprite fightingPit = Require(Structural("Stronghold_FightingPit"), "fighting pit");
            Sprite exitTower = Require(Structural("Stronghold_ExitTower"), "exit tower");
            Sprite hallwayWall = Require(Structural("Stronghold_HallwayWall"), "hallway wall");
            Sprite foregroundRocks = Require(Structural("Stronghold_ForegroundRocks"), "foreground rocks");
            Sprite barracksSupplies = Require(Props("Stronghold_BarracksSupplies"), "barracks supplies");
            Sprite marketStalls = Require(Props("Stronghold_MarketStalls"), "market stalls");
            Sprite marketAwning = Require(Props("Stronghold_MarketAwning"), "market awning");
            Sprite slumShelters = Require(Props("Stronghold_SlumShelters"), "slum shelters");
            Sprite banners = Require(Props("Stronghold_BannerSet"), "banners");
            Sprite lanterns = Require(Props("Stronghold_LanternSet"), "lanterns");
            Sprite crates = Require(Props("Stronghold_CrateBarrelSet"), "crates and barrels");
            Sprite crystals = Require(Props("Stronghold_CrystalField"), "crystal field");
            Sprite ropePosts = Require(Props("Stronghold_RopePosts"), "rope posts");

            // Background density is visual only: it unifies the city without turning it into collision.
            CaveSceneBuilderV1.PlaceBackgroundSprite("Stronghold Left Cave Depth", roots.Background, caveWall, new Vector2(-27f, 1.5f), new Vector3(4.6f, 4.6f, 1f), -40);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Stronghold Wall Depth", roots.Background, fortifiedWall, new Vector2(-22f, 0.8f), new Vector3(3.0f, 3.0f, 1f), -39);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Stronghold Central Depth", roots.Background, barracks, new Vector2(0f, -0.5f), new Vector3(5.4f, 5.4f, 1f), -38);
            CaveSceneBuilderV1.PlaceBackgroundSprite("Stronghold Right Cave Depth", roots.Background, caveWall, new Vector2(27f, -0.4f), new Vector3(4.7f, 4.7f, 1f), -40);

            // Left entry: the defensive wall stays visually dominant over the barracks and slum dressing.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Left Entry Floor", roots.Terrain, stoneFloor, new Vector2(-30f, -5.2f), new Vector2(10f, 0.85f), new Vector3(4.0f, 4.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Barracks Floor", roots.Terrain, stoneFloor, new Vector2(-22f, -5.1f), new Vector2(10f, 0.85f), new Vector3(4.0f, 4.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceProp("Fortified Entry Wall", roots.Props, fortifiedWall, new Vector2(-23.6f, -3.7f), new Vector3(3.35f, 3.35f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Barracks Wing", roots.Props, barracks, new Vector2(-29.4f, -4.4f), new Vector3(2.75f, 2.75f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Barracks Supply Line", roots.Props, barracksSupplies, new Vector2(-18.5f, -4.4f), new Vector3(1.45f, 1.45f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Fortified Wall Banners", roots.Props, banners, new Vector2(-20.6f, -2.1f), new Vector3(1.05f, 1.05f, 1f), 9);

            // The broad slum floor is deliberately open; shelters are background/edge dressing only.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Slum Combat Floor", roots.Terrain, stoneFloor, new Vector2(-10.5f, -5.0f), new Vector2(17f, 0.9f), new Vector3(6.75f, 4.0f, 1f), 2);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Slum Upper Ledge", roots.Terrain, stoneLedge, new Vector2(-12.7f, -2.7f), new Vector2(6f, 0.72f), new Vector3(2.8f, 2.8f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Slum District Background", roots.Props, slumShelters, new Vector2(-11.3f, -4.15f), new Vector3(2.25f, 2.25f, 1f), 1);
            CaveSceneBuilderV1.PlaceProp("Slum Lanterns", roots.Props, lanterns, new Vector2(-15.9f, -4.15f), new Vector3(1.0f, 1.0f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Slum Supplies", roots.Props, crates, new Vector2(-5.2f, -4.3f), new Vector3(0.82f, 0.82f, 1f), 8);

            // Market raises the player slightly, then runs broadly right rather than ending in a single stall cluster.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Market Main Deck", roots.Terrain, marketDeck, new Vector2(4.5f, -3.35f), new Vector2(20f, 0.95f), new Vector3(4.15f, 4.15f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Market Attack Ledge", roots.Terrain, stoneLedge, new Vector2(6.4f, -0.95f), new Vector2(6.4f, 0.7f), new Vector3(2.95f, 2.95f, 1f), 4);
            CaveSceneBuilderV1.PlaceProp("Market Stalls", roots.Props, marketStalls, new Vector2(3.0f, -3.7f), new Vector3(2.3f, 2.3f, 1f), 5);
            CaveSceneBuilderV1.PlaceProp("Market Awning", roots.Props, marketAwning, new Vector2(11.1f, -3.45f), new Vector3(1.75f, 1.75f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Market Rope Lighting", roots.Props, ropePosts, new Vector2(9.3f, -2.3f), new Vector3(1.25f, 1.25f, 1f), 7);

            // The royal hall dominates the next district, while the forecourt remains a generous combat surface.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Stronghold Forecourt", roots.Terrain, stoneFloor, new Vector2(20.5f, -3.3f), new Vector2(14f, 0.95f), new Vector3(5.6f, 4.0f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Central Stronghold Citadel", roots.Props, fortifiedWall, new Vector2(19.5f, -3.8f), new Vector3(2.65f, 2.65f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Central Stronghold", roots.Props, royalHall, new Vector2(20f, -3.2f), new Vector3(2.75f, 2.75f, 1f), 8);
            CaveSceneBuilderV1.PlaceProp("Stronghold Banners", roots.Props, banners, new Vector2(24.8f, -0.2f), new Vector3(1.15f, 1.15f, 1f), 9);
            CaveSceneBuilderV1.PlaceProp("Stronghold Lanterns", roots.Props, lanterns, new Vector2(14.0f, -3.2f), new Vector3(1.05f, 1.05f, 1f), 9);

            // Exactly one arena, set below the Stronghold. It has a floor, a short ingress, and a separate exit rise.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Fighting Pit Entrance", roots.Terrain, stairRise, new Vector2(18.0f, -5.2f), new Vector2(6.0f, 0.85f), new Vector3(2.35f, 2.35f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Stronghold Fighting Pit Floor", roots.Terrain, fightingPit, new Vector2(25.0f, -7.8f), new Vector2(15.0f, 1.0f), new Vector3(3.05f, 3.05f, 1f), 3);
            CaveSceneBuilderV1.PlaceWalkablePlatform("Fighting Pit Exit Rise", roots.Terrain, stairRise, new Vector2(31.5f, -5.5f), new Vector2(5.0f, 0.82f), new Vector3(-1.95f, 1.95f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Pit Crystal Accent", roots.Props, crystals, new Vector2(31.5f, -7.3f), new Vector3(0.78f, 0.78f, 1f), 7);

            // The final right-side compression is a small hall, not another combat arena.
            CaveSceneBuilderV1.PlaceWalkablePlatform("Right Transition Hall Floor", roots.Terrain, stoneFloor, new Vector2(35.5f, -3.4f), new Vector2(9f, 0.9f), new Vector3(3.6f, 3.6f, 1f), 3);
            CaveSceneBuilderV1.PlaceProp("Right Transition Tower", roots.Props, exitTower, new Vector2(36.7f, -3.2f), new Vector3(1.55f, 1.55f, 1f), 7);
            CaveSceneBuilderV1.PlaceProp("Right Hallway Wall", roots.Props, hallwayWall, new Vector2(32.5f, -2.3f), new Vector3(1.85f, 1.85f, 1f), 6);
            CaveSceneBuilderV1.PlaceProp("Right Hallway Supplies", roots.Props, crates, new Vector2(38.4f, -3.0f), new Vector3(0.72f, 0.72f, 1f), 8);

            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Stronghold Left Boundary", roots.Terrain, new Vector2(-35.2f, -0.5f), new Vector2(0.75f, 15f));
            CaveSceneBuilderV1.PlaceInvisibleCollisionWall("Stronghold Right Boundary", roots.Terrain, new Vector2(40.4f, -0.5f), new Vector2(0.75f, 15f));
            CaveSceneBuilderV1.PlaceForegroundOccluder("Stronghold Foreground Rocks", roots.Foreground, foregroundRocks, new Vector2(3.0f, -8.8f), new Vector3(7.2f, 7.2f, 1f), 20);
        }

        private static void BuildGameplayMarkers(CaveSceneBuilderV1.RoomRoots roots)
        {
            CaveSceneBuilderV1.PlacePlayerSpawn("Stronghold Player Spawn", roots.PlayerSpawn, new Vector2(-31.5f, -4.35f), "FromUpperCave");
            CaveSceneBuilderV1.PlaceInteractableMarker("Stronghold Fighting Pit Marker", roots.Interactables, new Vector2(25f, -6.85f), "StrongholdPitArena");
            CaveSceneBuilderV1.PlaceRoomTransitionMarker("Stronghold Right Exit Marker", roots.Transitions, new Vector2(39.3f, -2.5f), "ToHollowDistricts", "Cave_Level01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Barracks Encounter", roots.EnemySpawns, new Vector2(-22.5f, -4.3f), "StrongholdBarracks01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Slum Encounter", roots.EnemySpawns, new Vector2(-10.0f, -4.2f), "StrongholdSlums01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Market Encounter", roots.EnemySpawns, new Vector2(4.5f, -2.55f), "StrongholdMarket01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Stronghold Encounter", roots.EnemySpawns, new Vector2(20f, -2.45f), "StrongholdCore01");
            CaveSceneBuilderV1.PlaceEnemySpawnMarker("Future Fighting Pit Encounter", roots.EnemySpawns, new Vector2(25f, -6.8f), "StrongholdPit01");
            CaveSceneBuilderV1.PlaceKillZone("Stronghold Fall Kill Zone", roots.Hazards, new Vector2(3f, -15f), new Vector2(82f, 2f));
            CaveSceneBuilderV1.SetCameraBounds("Stronghold Camera Bounds", roots.Camera, new Vector2(3f, -1.5f), new Vector2(82f, 22f), "StrongholdCameraBounds");
        }

        private static GameObject ClonePlayer(PlayerHealth sourcePlayer, Scene destination, Transform parent)
        {
            GameObject player = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            player.name = "Player";
            SceneManager.MoveGameObjectToScene(player, destination);
            player.transform.SetParent(parent, true);
            player.transform.position = new Vector3(-31.5f, -4.35f, 0f);
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
            cameraObject.transform.position = new Vector3(-31.5f, -2.0f, -10f);
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
            GameObject lighting = new GameObject("Stronghold Ambient Light");
            lighting.transform.SetParent(parent, false);
            Light light = lighting.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.67f, 0.41f, 0.22f, 1f);
            light.intensity = 0.28f;
            lighting.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static Sprite Structural(string spriteName)
        {
            return CaveSceneBuilderV1.LoadSprite(StrongholdTileSheetImporter.StructuralSheetPath, spriteName);
        }

        private static Sprite Props(string spriteName)
        {
            return CaveSceneBuilderV1.LoadSprite(StrongholdTileSheetImporter.PropsSheetPath, spriteName);
        }

        private static Sprite Require(Sprite sprite, string role)
        {
            if (sprite == null)
            {
                throw new InvalidOperationException("Stronghold needs the approved " + role + " sprite.");
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
