#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Scoped high-detail import policy for the supplied Skeleton frames.</summary>
    internal sealed class SkeletonFrameImportSettings : AssetPostprocessor
    {
        private const string AssetRoot = "Assets/Cave/Resources/Enemies/Skeleton/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AssetRoot, System.StringComparison.Ordinal)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // The source's padded canvases need a high PPU at the existing
            // scaled Skeleton root. Preserve their native resolution: the old
            // 512px cap halved 1024px frames while retaining the 4096 PPU.
            importer.spritePixelsPerUnit = 4096f;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = IsRegularWalkFrame(assetPath)
                ? (int)SpriteAlignment.BottomCenter
                : (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
        }

        private static bool IsRegularWalkFrame(string assetPath)
        {
            return assetPath.IndexOf("/Regular/Walk/", System.StringComparison.Ordinal) >= 0;
        }
    }
}
#endif
