using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    internal sealed class CompactHudArtImportSettings : AssetPostprocessor
    {
        private const string CarapaceFolder = "Assets/Cave/Resources/UI/Hud/Carapace/";
        private const string SourceAtlas = "Assets/Cave/Resources/UI/HudCompact/Source/";
        private const string ExtractedHudFolder = "Assets/Cave/Resources/UI/HudCompact/";

        private void OnPreprocessTexture()
        {
            TextureImporter importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(CarapaceFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (assetPath.StartsWith(SourceAtlas, System.StringComparison.OrdinalIgnoreCase))
            {
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (assetPath.StartsWith(ExtractedHudFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }
    }
}
