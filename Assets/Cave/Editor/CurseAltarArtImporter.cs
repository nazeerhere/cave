#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Locks the approved altar source to the project's pixel-art import contract.</summary>
    internal sealed class CurseAltarArtImporter : AssetPostprocessor
    {
        private const string SourcePath = "Assets/Cave/Resources/Player/CurseAltar/CurseAltarActive.png";

        [InitializeOnLoadMethod]
        private static void VerifyImportAfterDomainReload()
        {
            EditorApplication.delayCall += EnsureApprovedImportSettings;
        }

        private void OnPreprocessTexture()
        {
            if (assetPath != SourcePath)
            {
                return;
            }

            Configure((TextureImporter)assetImporter);
        }

        [MenuItem("Tools/Cave/Art/Reimport Approved Curse Altar")]
        private static void ReimportApprovedAltar()
        {
            EnsureApprovedImportSettings();
        }

        private static void EnsureApprovedImportSettings()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null || IsConfigured(importer))
            {
                return;
            }

            Configure(importer);
            importer.SaveAndReimport();
        }

        private static bool IsConfigured(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, 512f)
                && importer.filterMode == FilterMode.Point
                && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.npotScale == TextureImporterNPOTScale.None
                && importer.maxTextureSize >= 2048;
        }

        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.spritePivot = new Vector2(0.5f, 0f);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
        }
    }
}
#endif
