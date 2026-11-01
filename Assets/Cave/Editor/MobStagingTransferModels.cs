#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor.MobStaging
{
    internal static class MobStagingTransferPaths
    {
        internal const int SchemaVersion = 1;
        internal const string ProductionMobRoot = "Assets/Cave/Prefabs/Mobs/";
        internal const string ExchangeRoot = "/Users/nazeer171/Desktop/cave/MobStagingExchange";
        internal const string ToStagingFolder = ExchangeRoot + "/ToStaging";
        internal const string FromStagingFolder = ExchangeRoot + "/FromStaging";
        internal const string ArchiveFolder = ExchangeRoot + "/Archive";
        internal const string ManifestFolder = ExchangeRoot + "/Manifests";

        internal static readonly string[] ProtectedPaths =
        {
            "Assets/Cave/Scripts/",
            "Assets/Cave/Editor/",
            "Assets/Cave/Scenes/",
            "Assets/Cave/Prefabs/Player",
            "Assets/Cave/Resources/",
            "Assets/Cave/Animations/"
        };

        internal static readonly string[] EditablePolicy =
        {
            "Transform local position, rotation, and scale on existing non-root prefab children",
            "SpriteRenderer presentation fields excluding replacement sprite assets",
            "Animator presentation settings; controller changes require review",
            "BoxCollider2D, CircleCollider2D, CapsuleCollider2D, and PolygonCollider2D geometry and trigger state",
            "Approved Rigidbody2D serialized settings",
            "Existing dedicated Cave hurtbox/hitbox geometry through their Collider2D; gameplay script fields are never copied"
        };

        internal static bool IsProductionMobPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(ProductionMobRoot, StringComparison.Ordinal);
        }

        internal static bool IsProtectedPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            for (int index = 0; index < ProtectedPaths.Length; index++)
            {
                if (path.StartsWith(ProtectedPaths[index], StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    [Serializable]
    internal sealed class MobTransferAsset
    {
        public string path;
        public string guid;
        public string type;
        public bool mirroredReadOnly;
    }

    [Serializable]
    internal sealed class MobTransferManifest
    {
        public int schemaVersion;
        public string packageId;
        public string timestampUtc;
        public string direction;
        public string sourceProject;
        public string destinationProject;
        public string sourceGitBranch;
        public string sourceGitHead;
        public string packageFileName;
        public List<string> prefabRoots = new List<string>();
        public List<MobTransferAsset> includedAssets = new List<MobTransferAsset>();
        public List<string> protectedAuthoritativePaths = new List<string>();
        public List<string> stagingEditablePolicy = new List<string>();
    }

    [Serializable]
    internal sealed class MobAssetReference
    {
        public string path;
        public string guid;
        public long localId;
        public string type;

        public static MobAssetReference Capture(UnityEngine.Object asset)
        {
            var result = new MobAssetReference();
            if (asset == null) return result;
            string guid;
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId))
            {
                result.guid = guid;
                result.localId = localId;
                result.path = AssetDatabase.GUIDToAssetPath(guid);
            }
            result.type = asset.GetType().AssemblyQualifiedName;
            return result;
        }

        public bool SameAs(MobAssetReference other)
        {
            return other != null && guid == other.guid && localId == other.localId && path == other.path;
        }
    }

    [Serializable]
    internal sealed class MobSpriteState
    {
        public int componentIndex;
        public MobAssetReference sprite;
        public MobAssetReference material;
        public Color color;
        public bool flipX;
        public bool flipY;
        public string sortingLayerName;
        public int sortingOrder;
    }

    [Serializable]
    internal sealed class MobAnimatorState
    {
        public int componentIndex;
        public MobAssetReference controller;
        public float speed;
        public bool applyRootMotion;
        public int updateMode;
        public int cullingMode;
    }

    [Serializable]
    internal sealed class MobColliderState
    {
        public string type;
        public int componentIndex;
        public bool enabled;
        public bool isTrigger;
        public Vector2 offset;
        public Vector2 size;
        public float radius;
        public float edgeRadius;
        public int direction;
        public List<Vector2> points = new List<Vector2>();
    }

    [Serializable]
    internal sealed class MobRigidbodyState
    {
        public bool exists;
        public bool simulated;
        public int bodyType;
        public float mass;
        public float drag;
        public float angularDrag;
        public float gravityScale;
        public int collisionDetectionMode;
        public int interpolation;
        public int constraints;
    }

    [Serializable]
    internal sealed class MobNodeState
    {
        public string path;
        public string name;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public List<MobSpriteState> sprites = new List<MobSpriteState>();
        public List<MobAnimatorState> animators = new List<MobAnimatorState>();
        public List<MobColliderState> colliders = new List<MobColliderState>();
        public MobRigidbodyState rigidbody = new MobRigidbodyState();
    }

    [Serializable]
    internal sealed class MobPrefabBaseline
    {
        public string targetPrefabPath;
        public string targetPrefabGuid;
        public List<MobNodeState> nodes = new List<MobNodeState>();
    }

    [Serializable]
    internal sealed class MobStagingBaseline
    {
        public int schemaVersion;
        public string packageId;
        public string capturedUtc;
        public string sourceMainBranch;
        public string sourceMainHead;
        public List<MobPrefabBaseline> prefabs = new List<MobPrefabBaseline>();
    }

    [Serializable]
    internal sealed class MobPrefabPatch
    {
        public string targetPrefabPath;
        public string targetPrefabGuid;
        public List<MobNodeState> changedNodes = new List<MobNodeState>();
        public List<string> changeSummary = new List<string>();
    }

    [Serializable]
    internal sealed class MobReturnPatch
    {
        public int schemaVersion;
        public string packageId;
        public string timestampUtc;
        public string direction;
        public string sourceProject;
        public string destinationProject;
        public string sourceMainBranch;
        public string sourceMainHead;
        public string baselinePackageId;
        public List<string> protectedAuthoritativePaths = new List<string>();
        public List<string> reviewRequiredAssets = new List<string>();
        public List<MobPrefabPatch> prefabPatches = new List<MobPrefabPatch>();
    }
}
#endif
