using System;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Keeps only the extracted Nymrath frame files on the same sprite import
    /// path as Cave's pixel-art enemies. Composite source sheets remain source
    /// references and are deliberately not sliced or repurposed at import time.
    /// </summary>
    public sealed class NymrathSpriteImportSettings : AssetPostprocessor
    {
        private const string SourceRoot = "Assets/Cave/Art/Creatures/Nymrath/Source/";
        private static bool assetBuildScheduled;

        private void OnPreprocessTexture()
        {
            if (!IsExtractedFrame(assetPath))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024f;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0f);
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        [MenuItem("Cave/Creatures/Nymrath/Apply Frame Import Settings")]
        private static void ApplyFrameImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceRoot.TrimEnd('/') });
            int imported = 0;
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!IsExtractedFrame(path))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                imported++;
            }

            Debug.Log("Nymrath configured " + imported + " extracted animation frames.");
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] _,
            string[] __,
            string[] ___)
        {
            if (assetBuildScheduled || importedAssets == null)
            {
                return;
            }

            for (int index = 0; index < importedAssets.Length; index++)
            {
                if (!IsExtractedFrame(importedAssets[index]))
                {
                    continue;
                }

                assetBuildScheduled = true;
                EditorApplication.delayCall += BuildAfterFrameImport;
                return;
            }
        }

        private static void BuildAfterFrameImport()
        {
            assetBuildScheduled = false;
            NymrathAssetBuilder.BuildIfReady();
        }

        internal static bool IsExtractedFrame(string path)
        {
            if (string.IsNullOrEmpty(path)
                || !path.StartsWith(SourceRoot, StringComparison.Ordinal)
                || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string filename = System.IO.Path.GetFileNameWithoutExtension(path);
            return filename.StartsWith("GrottoMender_", StringComparison.Ordinal)
                || filename.StartsWith("CorruptedQilin_", StringComparison.Ordinal);
        }
    }
}
