using System;
using System.Collections.Generic;
using Cave.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor.VisualQA
{
    [Serializable]
    public sealed class CaveVisualQaSceneManifest
    {
        public int schemaVersion = 1;
        public string sceneName;
        public string scenePath;
        public string exportedAtUtc;
        public CaveVisualQaBounds overviewBounds;
        public string gameplayCamera;
        public List<CaveVisualQaObject> objects = new List<CaveVisualQaObject>();
    }

    [Serializable]
    public sealed class CaveVisualQaObject
    {
        public string name;
        public string hierarchyPath;
        public bool activeSelf;
        public bool activeInHierarchy;
        public CaveVisualQaVector3 worldPosition;
        public CaveVisualQaVector3 localPosition;
        public CaveVisualQaVector3 worldEulerRotation;
        public CaveVisualQaVector3 localEulerRotation;
        public CaveVisualQaVector3 localScale;
        public string tag;
        public int layer;
        public string layerName;
        public string category;
        public string prefabAssetPath;
        public string spriteName;
        public string spriteAssetPath;
        public string sortingLayer;
        public int sortingOrder;
        public bool hasRendererBounds;
        public CaveVisualQaBounds rendererBounds;
        public List<CaveVisualQaCollider> colliders = new List<CaveVisualQaCollider>();
        public bool hasRigidbody2D;
        public string rigidbody2DBodyType;
        public bool hasRoomMarker;
        public CaveVisualQaMarker marker;
        public List<string> importantComponents = new List<string>();
    }

    [Serializable]
    public sealed class CaveVisualQaCollider
    {
        public string type;
        public bool isTrigger;
        public CaveVisualQaBounds bounds;
    }

    [Serializable]
    public sealed class CaveVisualQaMarker
    {
        public string kind;
        public string identifier;
        public string destinationScene;
        public CaveVisualQaVector2 boundsSize;
    }

    [Serializable]
    public struct CaveVisualQaVector2
    {
        public float x;
        public float y;

        public CaveVisualQaVector2(Vector2 value)
        {
            x = value.x;
            y = value.y;
        }
    }

    [Serializable]
    public struct CaveVisualQaVector3
    {
        public float x;
        public float y;
        public float z;

        public CaveVisualQaVector3(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }
    }

    [Serializable]
    public struct CaveVisualQaBounds
    {
        public CaveVisualQaVector3 center;
        public CaveVisualQaVector3 size;
        public CaveVisualQaVector3 min;
        public CaveVisualQaVector3 max;

        public CaveVisualQaBounds(Bounds value)
        {
            center = new CaveVisualQaVector3(value.center);
            size = new CaveVisualQaVector3(value.size);
            min = new CaveVisualQaVector3(value.min);
            max = new CaveVisualQaVector3(value.max);
        }
    }

    /// <summary>Builds the compact, editor-only structural evidence for a loaded authored room.</summary>
    public static class CaveVisualQaManifestExporter
    {
        public static CaveVisualQaSceneManifest Create(Scene scene, Bounds overviewBounds, Camera gameplayCamera)
        {
            CaveVisualQaSceneManifest manifest = new CaveVisualQaSceneManifest
            {
                sceneName = scene.name,
                scenePath = scene.path,
                exportedAtUtc = DateTime.UtcNow.ToString("o"),
                overviewBounds = new CaveVisualQaBounds(overviewBounds),
                gameplayCamera = gameplayCamera != null ? GetHierarchyPath(gameplayCamera.transform) : string.Empty
            };

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AddHierarchy(root.transform, manifest.objects);
            }

            return manifest;
        }

        private static void AddHierarchy(Transform transform, List<CaveVisualQaObject> output)
        {
            if ((transform.hideFlags & HideFlags.DontSave) == 0)
            {
                output.Add(CreateObjectRecord(transform.gameObject));
            }

            for (int index = 0; index < transform.childCount; index++)
            {
                AddHierarchy(transform.GetChild(index), output);
            }
        }

        private static CaveVisualQaObject CreateObjectRecord(GameObject gameObject)
        {
            Transform transform = gameObject.transform;
            CaveVisualQaObject record = new CaveVisualQaObject
            {
                name = gameObject.name,
                hierarchyPath = GetHierarchyPath(transform),
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                worldPosition = new CaveVisualQaVector3(transform.position),
                localPosition = new CaveVisualQaVector3(transform.localPosition),
                worldEulerRotation = new CaveVisualQaVector3(transform.eulerAngles),
                localEulerRotation = new CaveVisualQaVector3(transform.localEulerAngles),
                localScale = new CaveVisualQaVector3(transform.localScale),
                tag = gameObject.tag,
                layer = gameObject.layer,
                layerName = LayerMask.LayerToName(gameObject.layer),
                category = InferCategory(gameObject),
                prefabAssetPath = GetPrefabAssetPath(gameObject)
            };

            AddRenderer(record, gameObject.GetComponent<SpriteRenderer>());
            AddColliders(record, gameObject);
            AddRigidbody(record, gameObject.GetComponent<Rigidbody2D>());
            AddMarker(record, gameObject.GetComponent<RoomSceneMarker>());
            AddImportantComponents(record, gameObject);
            return record;
        }

        private static void AddRenderer(CaveVisualQaObject record, SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            record.sortingLayer = renderer.sortingLayerName;
            record.sortingOrder = renderer.sortingOrder;
            record.hasRendererBounds = true;
            record.rendererBounds = new CaveVisualQaBounds(renderer.bounds);
            if (renderer.sprite != null)
            {
                record.spriteName = renderer.sprite.name;
                record.spriteAssetPath = AssetDatabase.GetAssetPath(renderer.sprite);
            }
        }

        private static void AddColliders(CaveVisualQaObject record, GameObject gameObject)
        {
            Collider2D[] colliders = gameObject.GetComponents<Collider2D>();
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider2D collider = colliders[index];
                record.colliders.Add(new CaveVisualQaCollider
                {
                    type = collider.GetType().Name,
                    isTrigger = collider.isTrigger,
                    bounds = new CaveVisualQaBounds(collider.bounds)
                });
            }
        }

        private static void AddRigidbody(CaveVisualQaObject record, Rigidbody2D body)
        {
            if (body == null)
            {
                return;
            }

            record.hasRigidbody2D = true;
            record.rigidbody2DBodyType = body.bodyType.ToString();
        }

        private static void AddMarker(CaveVisualQaObject record, RoomSceneMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            record.marker = new CaveVisualQaMarker
            {
                kind = marker.Kind.ToString(),
                identifier = marker.Identifier,
                destinationScene = marker.DestinationScene,
                boundsSize = new CaveVisualQaVector2(marker.BoundsSize)
            };
            record.hasRoomMarker = true;
        }

        private static void AddImportantComponents(CaveVisualQaObject record, GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    record.importantComponents.Add("MissingScript");
                    continue;
                }

                Type type = component.GetType();
                string typeName = type.FullName ?? type.Name;
                if (type.Namespace != null && type.Namespace.StartsWith("Cave", StringComparison.Ordinal)
                    || component is Camera
                    || component is SpriteRenderer
                    || component is Collider2D
                    || component is Rigidbody2D)
                {
                    record.importantComponents.Add(typeName);
                }
            }
        }

        private static string InferCategory(GameObject gameObject)
        {
            RoomSceneMarker marker = gameObject.GetComponent<RoomSceneMarker>();
            if (marker != null)
            {
                return marker.Kind.ToString();
            }

            if (gameObject.GetComponent<DeathBoundary>() != null)
            {
                return "KillZone";
            }

            if (gameObject.GetComponent<Camera>() != null)
            {
                return "GameplayCamera";
            }

            if (gameObject.layer == 8 && gameObject.GetComponent<Collider2D>() != null)
            {
                return "WalkablePlatform";
            }

            string path = GetHierarchyPath(gameObject.transform);
            if (path.IndexOf("/Background/", StringComparison.Ordinal) >= 0 || path.EndsWith("/Background", StringComparison.Ordinal))
            {
                return "Background";
            }

            if (path.IndexOf("/Foreground/", StringComparison.Ordinal) >= 0 || path.EndsWith("/Foreground", StringComparison.Ordinal))
            {
                return "Foreground";
            }

            if (path.IndexOf("/Terrain/", StringComparison.Ordinal) >= 0 || path.EndsWith("/Terrain", StringComparison.Ordinal))
            {
                return "Terrain";
            }

            if (path.IndexOf("/Props/", StringComparison.Ordinal) >= 0 || path.EndsWith("/Props", StringComparison.Ordinal))
            {
                return "Prop";
            }

            return "Structure";
        }

        private static string GetPrefabAssetPath(GameObject gameObject)
        {
            GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
            return prefabRoot == null ? string.Empty : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
