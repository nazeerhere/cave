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
    /// Binds the approved Witch source frames to the existing Wizard and
    /// DarkWizard actors. This is presentation-only: the root physics and all
    /// existing ability components remain authoritative.
    /// </summary>
    public static class ApprovedWizardVisualInstaller
    {
        private const string RegularRoot = "Assets/Cave/Art/Enemies/Wizard/Regular/Source";
        private const string CorruptRoot = "Assets/Cave/Art/Enemies/Wizard/Corrupt/Source";
        private const string WizardPrefabPath = "Assets/Cave/Prefabs/Mobs/Support/Wizard.prefab";
        private const string CorruptWizardPrefabPath = "Assets/Cave/Prefabs/Mobs/Support/DarkWizard.prefab";
        private const float PixelsPerUnit = 128f;

        private static readonly Sequence[] RegularSequences =
        {
            new Sequence("Idle", 6),
            new Sequence("Walk", 6),
            new Sequence("Attack_Shoot", 6),
            new Sequence("Cast_Channel", 6),
            new Sequence("Summon_Ritual", 6),
            new Sequence("Special_Spell", 6),
            new Sequence("Hurt", 4),
            new Sequence("Death", 6)
        };

        private static readonly Sequence[] CorruptSequences =
        {
            new Sequence("Page1_Row1", 4),
            new Sequence("Page1_Row2", 7),
            new Sequence("Page1_Row3", 5),
            new Sequence("Page1_Row4", 5),
            new Sequence("Page2_Row1", 6),
            new Sequence("Page2_Row2", 6),
            new Sequence("Page2_Row3", 5),
            new Sequence("Page2_Row4", 6)
        };

        [MenuItem("Tools/Cave/Enemies/Build Approved Wizard Visuals", priority = 220)]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ConfigureImports(RegularRoot);
            ConfigureImports(CorruptRoot);

            Sprite[] regularIdle = LoadFrames(RegularRoot, RegularSequences[0]);
            Sprite[] regularWalk = LoadFrames(RegularRoot, RegularSequences[1]);
            Sprite[] regularShoot = LoadFrames(RegularRoot, RegularSequences[2]);
            Sprite[] regularChannel = LoadFrames(RegularRoot, RegularSequences[3]);
            Sprite[] regularSummon = LoadFrames(RegularRoot, RegularSequences[4]);
            Sprite[] regularSpecial = LoadFrames(RegularRoot, RegularSequences[5]);
            Sprite[] regularHurt = LoadFrames(RegularRoot, RegularSequences[6]);
            Sprite[] regularDeath = LoadFrames(RegularRoot, RegularSequences[7]);

            Sprite[] corruptIdle = LoadFrames(CorruptRoot, CorruptSequences[0]);
            Sprite[] corruptWalk = LoadFrames(CorruptRoot, CorruptSequences[1]);
            Sprite[] corruptShoot = LoadFrames(CorruptRoot, CorruptSequences[2]);
            Sprite[] corruptChannel = LoadFrames(CorruptRoot, CorruptSequences[3]);
            Sprite[] corruptSummon = LoadFrames(CorruptRoot, CorruptSequences[4]);
            Sprite[] corruptSpecial = LoadFrames(CorruptRoot, CorruptSequences[5]);
            Sprite[] corruptHurt = LoadFrames(CorruptRoot, CorruptSequences[6]);
            Sprite[] corruptDeath = LoadFrames(CorruptRoot, CorruptSequences[7]);

            if (!AllPresent(
                    regularIdle, regularWalk, regularShoot, regularChannel, regularSummon, regularSpecial, regularHurt, regularDeath,
                    corruptIdle, corruptWalk, corruptShoot, corruptChannel, corruptSummon, corruptSpecial, corruptHurt, corruptDeath))
            {
                return;
            }

            BindPrefab(
                WizardPrefabPath,
                "Approved Wizard Visual",
                ApprovedEnemySheetAnimator.VisualRole.Wizard,
                regularIdle, regularWalk, regularShoot, regularChannel, regularSummon, regularSpecial, regularHurt, regularDeath);
            BindPrefab(
                CorruptWizardPrefabPath,
                "Approved Corrupt Wizard Visual",
                ApprovedEnemySheetAnimator.VisualRole.CorruptWizard,
                corruptIdle, corruptWalk, corruptShoot, corruptChannel, corruptSummon, corruptSpecial, corruptHurt, corruptDeath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Bound approved regular and corrupt Wizard visual frames.");
        }

        private static void ConfigureImports(string root)
        {
            if (!Directory.Exists(root))
            {
                Debug.LogError("[Cave] Wizard visual build stopped: source folder is missing: " + root);
                return;
            }

            string[] files = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string path = files[index].Replace('\\', '/');
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("[Cave] Wizard visual build stopped: no TextureImporter for " + path);
                    continue;
                }

                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.spritePivot != new Vector2(.5f, 0f)
                    || importer.spritePixelsPerUnit != PixelsPerUnit
                    || importer.filterMode != FilterMode.Point
                    || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.npotScale != TextureImporterNPOTScale.None
                    || !importer.alphaIsTransparency;
                if (!changed) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(.5f, 0f);
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static Sprite[] LoadFrames(string root, Sequence sequence)
        {
            string folder = root + "/" + sequence.folder;
            if (!Directory.Exists(folder))
            {
                Debug.LogError("[Cave] Wizard visual build stopped: missing sequence folder: " + folder);
                return Array.Empty<Sprite>();
            }

            string[] paths = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);
            if (paths.Length != sequence.count)
            {
                Debug.LogError("[Cave] Wizard visual build stopped: " + folder + " contains " + paths.Length
                    + " frames; expected " + sequence.count + ".");
                return Array.Empty<Sprite>();
            }

            var frames = new List<Sprite>(paths.Length);
            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index].Replace('\\', '/');
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogError("[Cave] Wizard visual build stopped: frame is not an imported Sprite: " + path);
                    return Array.Empty<Sprite>();
                }
                frames.Add(sprite);
            }
            return frames.ToArray();
        }

        private static bool AllPresent(params Sprite[][] groups)
        {
            for (int index = 0; index < groups.Length; index++)
            {
                if (groups[index] == null || groups[index].Length == 0) return false;
            }
            return true;
        }

        private static void BindPrefab(
            string prefabPath,
            string visualName,
            ApprovedEnemySheetAnimator.VisualRole role,
            Sprite[] idle,
            Sprite[] walk,
            Sprite[] shoot,
            Sprite[] channel,
            Sprite[] summon,
            Sprite[] special,
            Sprite[] hurt,
            Sprite[] death)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError("[Cave] Wizard visual build stopped: prefab is missing: " + prefabPath);
                return;
            }

            try
            {
                SpriteRenderer legacy = root.GetComponent<SpriteRenderer>();
                if (legacy == null)
                {
                    Debug.LogError("[Cave] Wizard visual build stopped: root SpriteRenderer is missing: " + prefabPath);
                    return;
                }

                Transform visual = root.transform.Find(visualName);
                if (visual == null)
                {
                    visual = new GameObject(visualName).transform;
                    visual.SetParent(root.transform, false);
                }

                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = legacy.sortingLayerID;
                renderer.sortingOrder = legacy.sortingOrder;
                renderer.sprite = idle[0];
                renderer.enabled = true;

                float legacyHeight = legacy.sprite != null ? legacy.sprite.bounds.size.y : idle[0].bounds.size.y;
                float idleHeight = Mathf.Max(.0001f, idle[0].bounds.size.y);
                visual.localPosition = new Vector3(0f, legacy.sprite != null ? legacy.sprite.bounds.min.y : 0f, 0f);
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one * (legacyHeight / idleHeight);

                legacy.enabled = false;
                legacy.sprite = null;

                ApprovedEnemySheetAnimator animator = root.GetComponent<ApprovedEnemySheetAnimator>();
                if (animator == null) animator = root.AddComponent<ApprovedEnemySheetAnimator>();
                animator.Configure(
                    role,
                    renderer,
                    legacy,
                    Array.Empty<SpriteRenderer>(),
                    idle,
                    walk,
                    Array.Empty<Sprite>(),
                    shoot,
                    special,
                    summon,
                    channel,
                    special,
                    hurt,
                    death,
                    true);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private sealed class Sequence
        {
            public readonly string folder;
            public readonly int count;

            public Sequence(string folderName, int frameCount)
            {
                folder = folderName;
                count = frameCount;
            }
        }
    }
}
#endif
