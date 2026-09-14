#if UNITY_EDITOR
using System.IO;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Imports the approved separated Eye frames unchanged and serializes them
    /// into the two existing Eye prefabs. No crop, alpha cleanup, or gameplay
    /// setup occurs here.
    /// </summary>
    public sealed class ApprovedEyeVisualInstaller : AssetPostprocessor
    {
        private const string SourceRoot = "Assets/Cave/Art/Enemies/Eye/Source/Approved/IndividualFrames/";
        private const string RegularRoot = SourceRoot + "Regular_Eye/";
        private const string CorruptRoot = SourceRoot + "Corrupt_Eye/";
        private const string RegularPrefabPath = "Assets/Cave/Prefabs/Mobs/Swarm/Eye.prefab";
        private const string CorruptPrefabPath = "Assets/Cave/Prefabs/Mobs/Swarm/Corrupted Eye.prefab";
        private const float PixelsPerUnit = 1024f;

        [MenuItem("Tools/Cave/Enemies/Build Approved Eye Visuals")]
        private static void BuildApprovedEyeVisuals()
        {
            if (!ValidateFrameRoots()) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BindPrefab(RegularPrefabPath, RegularRoot, false);
            BindPrefab(CorruptPrefabPath, CorruptRoot, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Approved Eye visuals bound to Regular and Corrupted Eye prefabs.");
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SourceRoot) || !assetPath.EndsWith(".png")) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.alphaIsTransparency = true;
        }

        private static bool ValidateFrameRoots()
        {
            return ValidateActionSet(RegularRoot, 5) && ValidateActionSet(CorruptRoot, 6);
        }

        private static bool ValidateActionSet(string root, int expectedCount)
        {
            string[] required = { "Idle", "Hover", "Drift_Fly", "Cast", "Attack", "Special_Charge", "Hurt", "Death" };
            for (int index = 0; index < required.Length; index++)
            {
                string folder = root + required[index];
                string absolute = Path.Combine(Directory.GetCurrentDirectory(), folder);
                if (!Directory.Exists(absolute) || Directory.GetFiles(absolute, "*.png").Length != expectedCount)
                {
                    Debug.LogError("Approved Eye frame validation failed: " + folder
                        + " must contain exactly " + expectedCount + " PNG frames.");
                    return false;
                }
            }
            return true;
        }

        private static void BindPrefab(string prefabPath, string frameRoot, bool corrupt)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    Debug.LogError("Approved Eye binding failed: root SpriteRenderer missing on " + prefabPath);
                    return;
                }

                EyeVisualAnimator animator = root.GetComponent<EyeVisualAnimator>();
                if (animator == null) animator = root.AddComponent<EyeVisualAnimator>();
                animator.Configure(
                    renderer,
                    corrupt,
                    LoadFrames(frameRoot, "Idle"),
                    LoadFrames(frameRoot, "Hover"),
                    LoadFrames(frameRoot, "Drift_Fly"),
                    LoadFrames(frameRoot, "Cast"),
                    LoadFrames(frameRoot, "Attack"),
                    LoadFrames(frameRoot, "Special_Charge"),
                    LoadFrames(frameRoot, "Hurt"),
                    LoadFrames(frameRoot, "Death"));
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite[] LoadFrames(string frameRoot, string action)
        {
            string folder = frameRoot + action + "/";
            string[] files = Directory.GetFiles(folder, "*.png");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            Sprite[] frames = new Sprite[files.Length];
            for (int index = 0; index < files.Length; index++)
            {
                string assetPath = folder + Path.GetFileName(files[index]);
                frames[index] = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (frames[index] == null)
                {
                    throw new System.InvalidOperationException("Eye frame did not import as a Sprite: " + assetPath);
                }
            }
            return frames;
        }
    }
}
#endif
