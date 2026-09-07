using System;
using System.Collections.Generic;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>
    /// Small, reusable construction API for authored Cave rooms. This is editor-only;
    /// it creates ordinary sprites, colliders, and markers with no generated update work.
    /// </summary>
    public static class CaveSceneBuilderV1
    {
        public const int GroundLayer = 8;

        public struct RoomRoots
        {
            public GameObject Root;
            public Transform Background;
            public Transform Terrain;
            public Transform Foreground;
            public Transform Props;
            public Transform PlayerSpawn;
            public Transform EnemySpawns;
            public Transform Interactables;
            public Transform Transitions;
            public Transform Hazards;
            public Transform Camera;
            public Transform LightingVfx;
        }

        public static Scene CreateNewScene(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                throw new InvalidOperationException(
                    scenePath + " already exists. This builder refuses to overwrite an authored scene.");
            }

            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        public static RoomRoots CreateRoomRoot(Scene scene, string roomName)
        {
            GameObject root = CreateObject(roomName, null, scene);
            Transform environment = CreateObject("Environment", root.transform, scene).transform;
            Transform gameplay = CreateObject("Gameplay", root.transform, scene).transform;
            return new RoomRoots
            {
                Root = root,
                Background = CreateObject("Background", environment, scene).transform,
                Terrain = CreateObject("Terrain", environment, scene).transform,
                Foreground = CreateObject("Foreground", environment, scene).transform,
                Props = CreateObject("Props", environment, scene).transform,
                PlayerSpawn = CreateObject("PlayerSpawn", gameplay, scene).transform,
                EnemySpawns = CreateObject("EnemySpawns", gameplay, scene).transform,
                Interactables = CreateObject("Interactables", gameplay, scene).transform,
                Transitions = CreateObject("Transitions", gameplay, scene).transform,
                Hazards = CreateObject("Hazards", gameplay, scene).transform,
                Camera = CreateObject("Camera", root.transform, scene).transform,
                LightingVfx = CreateObject("LightingVFX", root.transform, scene).transform
            };
        }

        public static Sprite LoadSprite(string assetPath, string spriteName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int index = 0; index < assets.Length; index++)
            {
                Sprite sprite = assets[index] as Sprite;
                if (sprite != null && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            Debug.LogWarning("Scene Builder could not resolve sprite '" + spriteName + "' at " + assetPath);
            return null;
        }

        public static Sprite LoadSingleSprite(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        public static GameObject PlaceTerrainSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector3 scale,
            int sortingOrder = 0)
        {
            return PlaceSprite(name, parent, sprite, position, scale, sortingOrder, Color.white);
        }

        public static GameObject PlaceWalkablePlatform(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 colliderSize,
            Vector3 scale,
            int sortingOrder = 0)
        {
            GameObject platform = PlaceTerrainSprite(name, parent, sprite, position, scale, sortingOrder);
            AddGroundCollider(platform, colliderSize);
            return platform;
        }

        public static GameObject PlaceCollisionWall(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 colliderSize,
            Vector3 scale,
            int sortingOrder = 0)
        {
            GameObject wall = PlaceTerrainSprite(name, parent, sprite, position, scale, sortingOrder);
            AddGroundCollider(wall, colliderSize);
            return wall;
        }

        /// <summary>Creates authored blocking geometry without duplicating a visible environment sprite.</summary>
        public static GameObject PlaceInvisibleCollisionWall(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 colliderSize)
        {
            GameObject wall = CreateObject(name, parent, parent.gameObject.scene);
            wall.transform.position = position;
            AddGroundCollider(wall, colliderSize);
            return wall;
        }

        public static GameObject PlaceBackgroundSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector3 scale,
            int sortingOrder = -20)
        {
            return PlaceSprite(name, parent, sprite, position, scale, sortingOrder, Color.white);
        }

        public static GameObject PlaceForegroundOccluder(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector3 scale,
            int sortingOrder = 20)
        {
            return PlaceSprite(name, parent, sprite, position, scale, sortingOrder, Color.white);
        }

        public static GameObject PlaceProp(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector3 scale,
            int sortingOrder = 5)
        {
            return PlaceSprite(name, parent, sprite, position, scale, sortingOrder, Color.white);
        }

        public static GameObject PlaceInteractableMarker(
            string name,
            Transform parent,
            Vector2 position,
            string identifier)
        {
            GameObject marker = PlaceMarker(name, parent, position, RoomSceneMarker.MarkerKind.Interactable, identifier);
            return marker;
        }

        public static GameObject PlaceEnemySpawnMarker(
            string name,
            Transform parent,
            Vector2 position,
            string identifier)
        {
            return PlaceMarker(name, parent, position, RoomSceneMarker.MarkerKind.EnemySpawn, identifier);
        }

        public static GameObject PlaceRoomTransitionMarker(
            string name,
            Transform parent,
            Vector2 position,
            string identifier,
            string destinationScene)
        {
            return PlaceMarker(
                name,
                parent,
                position,
                RoomSceneMarker.MarkerKind.Transition,
                identifier,
                destinationScene);
        }

        public static GameObject PlacePlayerSpawn(
            string name,
            Transform parent,
            Vector2 position,
            string identifier)
        {
            GameObject marker = PlaceMarker(name, parent, position, RoomSceneMarker.MarkerKind.PlayerSpawn, identifier);
            marker.AddComponent<LevelSpawnPoint>().Configure(identifier);
            return marker;
        }

        public static GameObject PlaceKillZone(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            GameObject killZone = CreateObject(name, parent, parent.gameObject.scene);
            killZone.transform.position = position;
            BoxCollider2D collider = killZone.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;
            killZone.AddComponent<DeathBoundary>();
            return killZone;
        }

        public static GameObject SetCameraBounds(
            string name,
            Transform parent,
            Vector2 center,
            Vector2 size,
            string identifier = "CameraBounds")
        {
            GameObject bounds = PlaceMarker(
                name,
                parent,
                center,
                RoomSceneMarker.MarkerKind.CameraBounds,
                identifier,
                null,
                size);
            return bounds;
        }

        public static void SaveScene(Scene scene, string scenePath)
        {
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
        }

        public static bool ValidateRoom(Scene scene, string requiredInteractableIdentifier)
        {
            bool valid = true;
            RoomSceneMarker[] markers = FindSceneComponents<RoomSceneMarker>(scene);
            int playerSpawnCount = 0;
            int transitionCount = 0;
            int cameraBoundsCount = 0;
            bool foundRequiredInteractable = false;
            for (int index = 0; index < markers.Length; index++)
            {
                RoomSceneMarker marker = markers[index];
                if (marker.Kind == RoomSceneMarker.MarkerKind.PlayerSpawn)
                {
                    playerSpawnCount++;
                }
                else if (marker.Kind == RoomSceneMarker.MarkerKind.Transition)
                {
                    transitionCount++;
                    if (string.IsNullOrWhiteSpace(marker.DestinationScene)
                        || !SceneAssetExists(marker.DestinationScene))
                    {
                        valid = false;
                        Debug.LogWarning("Room transition marker '" + marker.name
                            + "' has no valid destination scene asset.", marker);
                    }
                }
                else if (marker.Kind == RoomSceneMarker.MarkerKind.CameraBounds)
                {
                    cameraBoundsCount++;
                }
                else if (marker.Kind == RoomSceneMarker.MarkerKind.Interactable
                    && marker.Identifier == requiredInteractableIdentifier)
                {
                    foundRequiredInteractable = true;
                }
            }

            if (playerSpawnCount != 1)
            {
                valid = false;
                Debug.LogWarning("Room validation expected exactly one PlayerSpawn marker; found " + playerSpawnCount + ".");
            }

            if (transitionCount == 0)
            {
                valid = false;
                Debug.LogWarning("Room validation found no room-transition marker.");
            }

            if (cameraBoundsCount == 0)
            {
                valid = false;
                Debug.LogWarning("Room validation found no camera-bounds marker.");
            }

            if (!foundRequiredInteractable)
            {
                valid = false;
                Debug.LogWarning("Room validation is missing required interactable marker '"
                    + requiredInteractableIdentifier + "'.");
            }

            if (FindSceneComponents<Collider2D>(scene, collider => collider.gameObject.layer == GroundLayer).Length == 0)
            {
                valid = false;
                Debug.LogWarning("Room validation found no walkable Ground-layer collider.");
            }

            if (FindSceneComponents<DeathBoundary>(scene).Length == 0)
            {
                valid = false;
                Debug.LogWarning("Room validation found no kill zone.");
            }

            SpriteRenderer[] renderers = FindSceneComponents<SpriteRenderer>(scene);
            for (int index = 0; index < renderers.Length; index++)
            {
                // Player combat prefabs intentionally contain disabled visual helpers with no
                // sprite. Validate only authored-room renderers, not cloned gameplay prefabs.
                if (renderers[index].GetComponentInParent<Cave.Player.PlayerHealth>() == null
                    && renderers[index].sprite == null)
                {
                    valid = false;
                    Debug.LogWarning("Room validation found a SpriteRenderer without a sprite.", renderers[index]);
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) > 0)
                {
                    valid = false;
                    Debug.LogWarning("Room validation found a missing script under " + root.name + ".", root);
                }
            }

            Debug.Log(valid
                ? "Cave Scene Builder V1 validation passed for " + scene.name + "."
                : "Cave Scene Builder V1 validation completed with warnings for " + scene.name + ".");
            return valid;
        }

        private static GameObject PlaceMarker(
            string name,
            Transform parent,
            Vector2 position,
            RoomSceneMarker.MarkerKind kind,
            string identifier,
            string destination = null,
            Vector2? size = null)
        {
            GameObject marker = CreateObject(name, parent, parent.gameObject.scene);
            marker.transform.position = position;
            marker.AddComponent<RoomSceneMarker>().Configure(kind, identifier, destination, size);
            return marker;
        }

        private static GameObject PlaceSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector3 scale,
            int sortingOrder,
            Color color)
        {
            GameObject gameObject = CreateObject(name, parent, parent.gameObject.scene);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return gameObject;
        }

        private static void AddGroundCollider(GameObject gameObject, Vector2 size)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            gameObject.layer = groundLayer >= 0 ? groundLayer : GroundLayer;
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static GameObject CreateObject(string name, Transform parent, Scene scene)
        {
            GameObject gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            if (gameObject.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(gameObject, scene);
            }

            return gameObject;
        }

        private static bool SceneAssetExists(string sceneName)
        {
            string[] candidates = AssetDatabase.FindAssets(sceneName + " t:Scene");
            for (int index = 0; index < candidates.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(candidates[index]);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            return FindSceneComponents<T>(scene, null);
        }

        private static T[] FindSceneComponents<T>(Scene scene, Func<T, bool> predicate) where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] components = root.GetComponentsInChildren<T>(true);
                for (int index = 0; index < components.Length; index++)
                {
                    if (predicate == null || predicate(components[index]))
                    {
                        results.Add(components[index]);
                    }
                }
            }

            return results.ToArray();
        }
    }
}
