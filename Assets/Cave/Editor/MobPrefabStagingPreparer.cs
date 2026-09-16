using System;
using System.Collections.Generic;
using Cave.Combat;
using Cave.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    /// <summary>
    /// Creates one-way staging copies of production mob prefabs. Production
    /// prefab assets, catalogs, scenes, and gameplay configuration are never
    /// written by this utility. Shared scripts, sprites, materials, animator
    /// controllers, and deliberately nested presentation prefabs stay shared.
    /// </summary>
    internal static class MobPrefabStagingPreparer
    {
        private const string ProductionRoot = "Assets/Cave/Prefabs/Mobs";
        private const string StagingRoot = "Assets/Cave/Staging/Mobs";

        private readonly struct StagingEntry
        {
            public readonly string SourcePath;
            public readonly string StagingPath;
            public readonly bool UnpackToIndependentPrefab;

            public StagingEntry(string sourcePath, string stagingPath, bool unpackToIndependentPrefab = false)
            {
                SourcePath = sourcePath;
                StagingPath = stagingPath;
                UnpackToIndependentPrefab = unpackToIndependentPrefab;
            }
        }

        private static readonly StagingEntry[] Entries =
        {
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Melee/Brute .prefab", "Assets/Cave/Staging/Mobs/Melee/Brute.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Melee/Corrupt Brute .prefab", "Assets/Cave/Staging/Mobs/Melee/Corrupt Brute.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Melee/LightBandit Variant.prefab", "Assets/Cave/Staging/Mobs/Melee/LightBandit Variant.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Range/Detective.prefab", "Assets/Cave/Staging/Mobs/Range/Detective.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Range/Corrupt Detective.prefab", "Assets/Cave/Staging/Mobs/Range/Corrupt Detective.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Range/Goth .prefab", "Assets/Cave/Staging/Mobs/Range/Goth.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Range/Corrupt Goth .prefab", "Assets/Cave/Staging/Mobs/Range/Corrupt Goth.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Support/Wizard.prefab", "Assets/Cave/Staging/Mobs/Support/Wizard.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Support/DarkWizard.prefab", "Assets/Cave/Staging/Mobs/Support/DarkWizard.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Swarm/Eye.prefab", "Assets/Cave/Staging/Mobs/Swarm/Eye.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Swarm/Corrupted Eye.prefab", "Assets/Cave/Staging/Mobs/Swarm/Corrupted Eye.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Swarm/Skeleton.prefab", "Assets/Cave/Staging/Mobs/Swarm/Skeleton.prefab"),
            // This is the one production prefab variant. Keeping its base in
            // production would make staging edits deceptively unsafe, so its
            // resolved content becomes an independent staging prefab.
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Swarm/GeneralSkeleton Variant.prefab", "Assets/Cave/Staging/Mobs/Swarm/GeneralSkeleton Variant.prefab", true),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Tank/Troll.prefab", "Assets/Cave/Staging/Mobs/Tank/Troll.prefab"),
            new StagingEntry("Assets/Cave/Prefabs/Mobs/Tank/Corrupt Troll.prefab", "Assets/Cave/Staging/Mobs/Tank/Corrupt Troll.prefab")
        };

        [MenuItem("Tools/Cave/Staging/Prepare Mob Prefab Staging Copies")]
        private static void PrepareStagingCopies()
        {
            if (!AssetDatabase.IsValidFolder(ProductionRoot))
            {
                throw new InvalidOperationException("Missing production mob prefab root: " + ProductionRoot);
            }

            EnsureFolder(StagingRoot);
            int created = 0;
            for (int index = 0; index < Entries.Length; index++)
            {
                StagingEntry entry = Entries[index];
                if (AssetDatabase.LoadAssetAtPath<GameObject>(entry.SourcePath) == null)
                {
                    throw new InvalidOperationException("Missing production prefab: " + entry.SourcePath);
                }

                EnsureFolder(ParentFolder(entry.StagingPath));
                if (AssetDatabase.LoadMainAssetAtPath(entry.StagingPath) == null)
                {
                    if (entry.UnpackToIndependentPrefab)
                    {
                        CopyVariantAsIndependentPrefab(entry);
                    }
                    else if (!AssetDatabase.CopyAsset(entry.SourcePath, entry.StagingPath))
                    {
                        throw new InvalidOperationException("Could not copy " + entry.SourcePath + " to " + entry.StagingPath);
                    }

                    created++;
                }
                else
                {
                    Debug.Log("[Cave] Staging prefab already exists; left unchanged: " + entry.StagingPath);
                }

            }

            RebindStagingCorruptionPairs();

            int validated = 0;
            for (int index = 0; index < Entries.Length; index++)
            {
                ValidateStagingPrefab(Entries[index]);
                validated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Cave] Mob staging complete. Created " + created + " and validated " + validated
                + " prefabs under " + StagingRoot + ". Production prefabs were not modified.");
        }

        private static void CopyVariantAsIndependentPrefab(StagingEntry entry)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.SourcePath);
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject contents = PrefabUtility.InstantiatePrefab(source, previewScene) as GameObject;
                if (contents == null || !PrefabUtility.IsPartOfPrefabInstance(contents))
                {
                    throw new InvalidOperationException("Expected a prefab variant instance: " + entry.SourcePath);
                }

                PrefabUtility.UnpackPrefabInstance(
                    contents,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                PrefabUtility.SaveAsPrefabAsset(contents, entry.StagingPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void RebindStagingCorruptionPairs()
        {
            RebindCorruptionPair(
                "Assets/Cave/Staging/Mobs/Melee/Brute.prefab",
                "Assets/Cave/Staging/Mobs/Melee/Corrupt Brute.prefab");
            RebindCorruptionPair(
                "Assets/Cave/Staging/Mobs/Range/Detective.prefab",
                "Assets/Cave/Staging/Mobs/Range/Corrupt Detective.prefab");
            RebindCorruptionPair(
                "Assets/Cave/Staging/Mobs/Range/Goth.prefab",
                "Assets/Cave/Staging/Mobs/Range/Corrupt Goth.prefab");
            RebindCorruptionPair(
                "Assets/Cave/Staging/Mobs/Swarm/Eye.prefab",
                "Assets/Cave/Staging/Mobs/Swarm/Corrupted Eye.prefab");
        }

        private static void RebindCorruptionPair(string normalStagingPath, string corruptStagingPath)
        {
            GameObject corruptStagingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(corruptStagingPath);
            GameObject contents = PrefabUtility.LoadPrefabContents(normalStagingPath);
            try
            {
                AuthoredCorruptedPrefabBinding binding = contents.GetComponent<AuthoredCorruptedPrefabBinding>();
                if (binding == null)
                {
                    return;
                }

                SerializedObject serializedBinding = new SerializedObject(binding);
                SerializedProperty replacement = serializedBinding.FindProperty("authoredCorruptedPrefab");
                if (replacement == null || corruptStagingPrefab == null)
                {
                    throw new InvalidOperationException("Cannot rebind staged corruption pair: " + normalStagingPath);
                }

                replacement.objectReferenceValue = corruptStagingPrefab;
                serializedBinding.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, normalStagingPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ValidateStagingPrefab(StagingEntry entry)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(entry.StagingPath);
            try
            {
                if (contents == null)
                {
                    throw new InvalidOperationException("Could not open staging prefab: " + entry.StagingPath);
                }

                MonoBehaviour[] behaviours = contents.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    if (behaviours[index] == null)
                    {
                        throw new InvalidOperationException("Missing script in staging prefab: " + entry.StagingPath);
                    }
                }

                if (contents.GetComponent<Damageable>() == null)
                {
                    throw new InvalidOperationException("Missing Damageable on staging mob root: " + entry.StagingPath);
                }

                if (contents.GetComponent<Rigidbody2D>() == null)
                {
                    throw new InvalidOperationException("Missing Rigidbody2D on staging mob root: " + entry.StagingPath);
                }

                if (contents.GetComponent<EnemyController>() == null)
                {
                    throw new InvalidOperationException("Missing EnemyController on staging mob root: " + entry.StagingPath);
                }

                AuthoredCorruptedPrefabBinding corruptionBinding = contents.GetComponent<AuthoredCorruptedPrefabBinding>();
                if (corruptionBinding != null && corruptionBinding.AuthoredCorruptedPrefab != null)
                {
                    string replacementPath = AssetDatabase.GetAssetPath(corruptionBinding.AuthoredCorruptedPrefab);
                    if (!replacementPath.StartsWith(StagingRoot + "/", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Staging corruption binding still targets production: " + entry.StagingPath);
                    }
                }

                Transform[] transforms = contents.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    if (PrefabUtility.GetPrefabInstanceStatus(transforms[index].gameObject)
                        == PrefabInstanceStatus.MissingAsset)
                    {
                        throw new InvalidOperationException(
                            "Missing nested prefab reference in staging mob: " + entry.StagingPath
                            + " at " + transforms[index].name);
                    }
                }

                Animator[] animators = contents.GetComponentsInChildren<Animator>(true);
                for (int index = 0; index < animators.Length; index++)
                {
                    if (animators[index].runtimeAnimatorController == null)
                    {
                        Debug.LogWarning("[Cave] Staging mob has an Animator without a controller: " + entry.StagingPath, animators[index]);
                    }
                }

                if (entry.UnpackToIndependentPrefab
                    && PrefabUtility.IsPartOfVariantPrefab(contents))
                {
                    throw new InvalidOperationException("Staging variant retained a production base: " + entry.StagingPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string parent = ParentFolder(assetFolder);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, assetFolder.Substring(parent.Length + 1));
        }

        private static string ParentFolder(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            if (separator <= 0)
            {
                throw new InvalidOperationException("Asset path has no parent folder: " + assetPath);
            }

            return assetPath.Substring(0, separator);
        }
    }
}
