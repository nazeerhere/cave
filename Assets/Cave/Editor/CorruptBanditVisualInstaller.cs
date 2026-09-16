#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Imports and binds only the approved Corrupt Bandit frames. It creates no
    /// gameplay objects and leaves the normal Bandit's combat configuration intact.
    /// </summary>
    public static class CorruptBanditVisualInstaller
    {
        private const string SourceRoot = "Assets/Cave/Art/Enemies/Bandit/Corrupt/Source";
        private const string NormalPrefabPath = "Assets/Cave/Prefabs/Mobs/Melee/LightBandit Variant.prefab";
        private const string CorruptPrefabPath = "Assets/Cave/Prefabs/Mobs/Melee/Corrupt Bandit Variant.prefab";
        private const string VisualName = "Approved Corrupt Bandit Visual";
        private const float PixelsPerUnit = 512f;

        private static readonly SequenceDefinition[] Sequences =
        {
            new SequenceDefinition("Idle", 4),
            new SequenceDefinition("Walk", 6),
            new SequenceDefinition("Run", 6),
            new SequenceDefinition("Slash", 5),
            new SequenceDefinition("Slash Followup", 5),
            new SequenceDefinition("Lunge Attack", 5),
            new SequenceDefinition("Shadow Step", 5),
            new SequenceDefinition("Dash Attack", 5),
            new SequenceDefinition("Hurt", 4),
            new SequenceDefinition("Death", 5)
        };

        [MenuItem("Tools/Cave/Enemies/Build Corrupt Bandit Visuals", priority = 219)]
        public static void Build()
        {
            if (!Directory.Exists(SourceRoot))
            {
                Debug.LogError("[Cave] Corrupt Bandit visual build stopped: approved source folder is missing: " + SourceRoot);
                return;
            }

            AssetDatabase.Refresh();
            ConfigureSourceImports();

            Sprite[] idle = LoadFrames("Idle");
            Sprite[] walk = LoadFrames("Walk");
            Sprite[] run = LoadFrames("Run");
            Sprite[] slash = LoadFrames("Slash");
            Sprite[] followup = LoadFrames("Slash Followup");
            Sprite[] lunge = LoadFrames("Lunge Attack");
            Sprite[] shadowStep = LoadFrames("Shadow Step");
            Sprite[] dashAttack = LoadFrames("Dash Attack");
            Sprite[] hurt = LoadFrames("Hurt");
            Sprite[] death = LoadFrames("Death");
            if (!HasExpectedFrames(idle, walk, run, slash, followup, lunge, shadowStep, dashAttack, hurt, death))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(NormalPrefabPath) == null)
            {
                Debug.LogError("[Cave] Corrupt Bandit visual build stopped: normal Bandit prefab is missing: " + NormalPrefabPath);
                return;
            }

            EnsureCorruptPrefab();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CorruptPrefabPath) == null)
            {
                Debug.LogError("[Cave] Corrupt Bandit visual build stopped: could not create the authored corrupted prefab.");
                return;
            }

            BindCorruptVisual(idle, walk, run, slash, followup, lunge, shadowStep, dashAttack, hurt, death);
            BindNormalCorruptionReplacement();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Bound the approved 50-frame Corrupt Bandit presentation to the authored corruption replacement.");
        }

        private static void ConfigureSourceImports()
        {
            string[] files = Directory.GetFiles(SourceRoot, "*.png", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string path = files[index].Replace('\\', '/');
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("[Cave] Corrupt Bandit import stopped: no TextureImporter for " + path);
                    continue;
                }

                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.spritePivot != new Vector2(0.5f, 0f)
                    || importer.spritePixelsPerUnit != PixelsPerUnit
                    || importer.filterMode != FilterMode.Point
                    || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.npotScale != TextureImporterNPOTScale.None
                    || !importer.alphaIsTransparency;
                if (!changed)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(0.5f, 0f);
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static Sprite[] LoadFrames(string sequence)
        {
            string folder = SourceRoot + "/" + sequence;
            SequenceDefinition expected = FindDefinition(sequence);
            if (!Directory.Exists(folder))
            {
                Debug.LogError("[Cave] Corrupt Bandit visual build stopped: sequence folder is missing: " + folder);
                return Array.Empty<Sprite>();
            }

            string[] paths = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);
            var frames = new List<Sprite>(paths.Length);
            for (int index = 0; index < paths.Length; index++)
            {
                string assetPath = paths[index].Replace('\\', '/');
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    Debug.LogError("[Cave] Corrupt Bandit visual build stopped: frame did not import as a Sprite: " + assetPath);
                    return Array.Empty<Sprite>();
                }
                frames.Add(sprite);
            }

            if (expected == null || frames.Count != expected.frameCount)
            {
                Debug.LogError("[Cave] Corrupt Bandit visual build stopped: " + sequence + " has "
                    + frames.Count + " frames; expected " + (expected != null ? expected.frameCount : 0) + ".");
                return Array.Empty<Sprite>();
            }
            return frames.ToArray();
        }

        private static bool HasExpectedFrames(params Sprite[][] groups)
        {
            for (int index = 0; index < groups.Length; index++)
            {
                if (groups[index] == null || groups[index].Length == 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static SequenceDefinition FindDefinition(string sequence)
        {
            for (int index = 0; index < Sequences.Length; index++)
            {
                if (Sequences[index].name == sequence) return Sequences[index];
            }
            return null;
        }

        private static void EnsureCorruptPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CorruptPrefabPath) != null)
            {
                return;
            }

            GameObject source = PrefabUtility.LoadPrefabContents(NormalPrefabPath);
            try
            {
                source.name = "Corrupt Bandit";
                PrefabUtility.SaveAsPrefabAsset(source, CorruptPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        private static void BindCorruptVisual(
            Sprite[] idle,
            Sprite[] walk,
            Sprite[] run,
            Sprite[] slash,
            Sprite[] followup,
            Sprite[] lunge,
            Sprite[] shadowStep,
            Sprite[] dashAttack,
            Sprite[] hurt,
            Sprite[] death)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CorruptPrefabPath);
            try
            {
                AuthoredCorruptedPrefabBinding[] recursiveBindings = root.GetComponents<AuthoredCorruptedPrefabBinding>();
                for (int index = 0; index < recursiveBindings.Length; index++)
                {
                    UnityEngine.Object.DestroyImmediate(recursiveBindings[index]);
                }

                SpriteRenderer legacy = root.GetComponent<SpriteRenderer>();
                Transform visual = root.transform.Find(VisualName);
                if (visual == null)
                {
                    GameObject visualObject = new GameObject(VisualName);
                    visual = visualObject.transform;
                    visual.SetParent(root.transform, false);
                }

                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                if (legacy != null)
                {
                    renderer.sortingLayerID = legacy.sortingLayerID;
                    renderer.sortingOrder = legacy.sortingOrder;
                    legacy.enabled = false;
                    legacy.sprite = null;
                }
                renderer.enabled = true;
                renderer.sprite = idle[0];

                Animator legacyAnimator = root.GetComponent<Animator>();
                if (legacyAnimator != null) legacyAnimator.enabled = false;
                LightBanditAnimator legacyBridge = root.GetComponent<LightBanditAnimator>();
                if (legacyBridge != null) legacyBridge.enabled = false;

                ApprovedEnemySheetAnimator animator = root.GetComponent<ApprovedEnemySheetAnimator>();
                if (animator == null) animator = root.AddComponent<ApprovedEnemySheetAnimator>();
                animator.Configure(
                    ApprovedEnemySheetAnimator.VisualRole.CorruptBandit,
                    renderer,
                    legacy,
                    Array.Empty<SpriteRenderer>(),
                    idle,
                    walk,
                    run,
                    slash,
                    followup,
                    lunge,
                    dashAttack,
                    shadowStep,
                    hurt,
                    death,
                    true);
                PrefabUtility.SaveAsPrefabAsset(root, CorruptPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BindNormalCorruptionReplacement()
        {
            GameObject replacement = AssetDatabase.LoadAssetAtPath<GameObject>(CorruptPrefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(NormalPrefabPath);
            try
            {
                AuthoredCorruptedPrefabBinding binding = root.GetComponent<AuthoredCorruptedPrefabBinding>();
                if (binding == null) binding = root.AddComponent<AuthoredCorruptedPrefabBinding>();
                SerializedObject serialized = new SerializedObject(binding);
                serialized.FindProperty("authoredCorruptedPrefab").objectReferenceValue = replacement;
                serialized.FindProperty("isBossMapping").boolValue = false;
                serialized.FindProperty("grantGeneralShardOnFinalDeath").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, NormalPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private sealed class SequenceDefinition
        {
            public readonly string name;
            public readonly int frameCount;

            public SequenceDefinition(string sequenceName, int count)
            {
                name = sequenceName;
                frameCount = count;
            }
        }
    }
}
#endif
