#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Repairs only the two audited, pre-existing null EnemyArchetypeProfile records.
    /// The records predate the staging transfer and are repaired through Unity's prefab API.
    /// </summary>
    internal static class EnemyArchetypeProfileSerializationRepair
    {
        private const int PreservedArchetypeValue = (int)EnemyArchetype.Melee;

        private static readonly string[] AffectedPrefabPaths =
        {
            "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab",
            "Assets/Cave/Prefabs/Mobs/Melee/LightBandit Variant.prefab"
        };

        [MenuItem("Tools/Cave/Repair/Audited Enemy Archetype Profile Serialization", priority = 240)]
        private static void RepairFromMenu()
        {
            RepairAndVerify();
        }

        // Batch-mode seam used only for the approved, narrowly scoped recovery/verification pass.
        public static void RepairAndVerify()
        {
            var originalGuids = new Dictionary<string, string>();
            for (int index = 0; index < AffectedPrefabPaths.Length; index++)
            {
                string path = AffectedPrefabPaths[index];
                originalGuids.Add(path, AssetDatabase.AssetPathToGUID(path));
                RepairKnownNullProfile(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            for (int index = 0; index < AffectedPrefabPaths.Length; index++)
            {
                string path = AffectedPrefabPaths[index];
                if (AssetDatabase.AssetPathToGUID(path) != originalGuids[path])
                {
                    throw new InvalidOperationException("Prefab GUID changed during serialization repair: " + path);
                }

                VerifyProfile(path);
            }

            VerifySkeletonDependencies();
            Debug.Log("[Cave] Repaired and verified the two audited EnemyArchetypeProfile prefab records.");
        }

        public static void VerifySkeletonForBatch()
        {
            VerifyProfile(AffectedPrefabPaths[0]);
            VerifySkeletonDependencies();
            Debug.Log("[Cave] Skeleton prefab serialization and dependency verification passed.");
        }

        private static void RepairKnownNullProfile(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing audited prefab: " + prefabPath);

            MonoBehaviour[] importedBehaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            int importedMissingCount = CountMissing(importedBehaviours);
            if (importedMissingCount == 0)
            {
                VerifyProfile(prefabPath);
                return;
            }

            if (importedMissingCount != 1)
            {
                throw new InvalidOperationException("Refusing to repair " + prefabPath
                    + ": expected exactly one audited null component, found " + importedMissingCount + ".");
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                MonoBehaviour[] rootBehaviours = contents.GetComponents<MonoBehaviour>();
                if (CountMissing(rootBehaviours) != 1)
                {
                    throw new InvalidOperationException("Refusing to repair " + prefabPath
                        + ": its null record is not the expected single root EnemyArchetypeProfile.");
                }

                if (GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents) != 1)
                {
                    throw new InvalidOperationException("Unity did not remove exactly one audited null component from " + prefabPath + ".");
                }

                // EnemySwarm may create its required profile while the prefab contents load.
                // Preserve that Unity-created component rather than creating a duplicate.
                EnemyArchetypeProfile[] profiles = contents.GetComponents<EnemyArchetypeProfile>();
                if (profiles.Length > 1)
                {
                    throw new InvalidOperationException("Refusing to repair " + prefabPath
                        + ": loading produced more than one EnemyArchetypeProfile.");
                }

                EnemyArchetypeProfile profile = profiles.Length == 1
                    ? profiles[0]
                    : contents.AddComponent<EnemyArchetypeProfile>();
                var serializedProfile = new SerializedObject(profile);
                SerializedProperty archetypes = serializedProfile.FindProperty("archetypes");
                if (archetypes == null)
                {
                    throw new InvalidOperationException("EnemyArchetypeProfile no longer exposes its serialized archetypes field.");
                }

                // The audited YAML record contained archetypes: 1. Preserve that value explicitly.
                archetypes.intValue = PreservedArchetypeValue;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void VerifyProfile(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Prefab did not resolve: " + prefabPath);
            if (CountMissing(prefab.GetComponentsInChildren<MonoBehaviour>(true)) != 0)
            {
                throw new InvalidOperationException("Missing MonoBehaviour remains in " + prefabPath + ".");
            }

            EnemyArchetypeProfile[] profiles = prefab.GetComponentsInChildren<EnemyArchetypeProfile>(true);
            if (profiles.Length != 1 || (int)profiles[0].Archetypes != PreservedArchetypeValue)
            {
                throw new InvalidOperationException("EnemyArchetypeProfile verification failed for " + prefabPath
                    + ": expected one profile with archetypes = " + PreservedArchetypeValue + ".");
            }
        }

        private static void VerifySkeletonDependencies()
        {
            const string skeletonPath = "Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(skeletonPath);
            if (prefab == null) throw new InvalidOperationException("Skeleton prefab did not resolve.");

            GameObject contents = PrefabUtility.LoadPrefabContents(skeletonPath);
            try
            {
                SpriteRenderer[] renderers = contents.GetComponentsInChildren<SpriteRenderer>(true);
                Animator[] animators = contents.GetComponentsInChildren<Animator>(true);
                if (renderers.Length == 0
                    || contents.GetComponentsInChildren<Rigidbody2D>(true).Length == 0
                    || contents.GetComponentsInChildren<Collider2D>(true).Length == 0)
                {
                    throw new InvalidOperationException("Skeleton is missing required SpriteRenderer, Rigidbody2D, or Collider2D dependencies.");
                }

                for (int index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index].sprite == null) throw new InvalidOperationException("Skeleton has an unresolved SpriteRenderer sprite.");
                }

                for (int index = 0; index < animators.Length; index++)
                {
                    RuntimeAnimatorController controller = animators[index].runtimeAnimatorController;
                    if (controller == null || controller.animationClips == null || controller.animationClips.Length == 0)
                    {
                        throw new InvalidOperationException("Skeleton has an unresolved Animator controller or AnimationClip.");
                    }
                }

                foreach (Transform transform in contents.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.GetPrefabInstanceStatus(transform.gameObject) == PrefabInstanceStatus.MissingAsset)
                    {
                        throw new InvalidOperationException("Skeleton has an unresolved nested prefab at " + transform.name + ".");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static int CountMissing(MonoBehaviour[] behaviours)
        {
            int count = 0;
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] == null) count++;
            }

            return count;
        }
    }
}
#endif
