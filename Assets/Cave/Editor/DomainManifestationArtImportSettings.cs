#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Keeps the structural Domain art predictable without affecting any other
    /// project texture import. The runtime loader also has a Texture2D fallback
    /// so an already-open editor can finish importing safely.
    /// </summary>
    internal sealed class DomainManifestationArtImportSettings : AssetPostprocessor
    {
        private const string AssetRoot = "Assets/Cave/Resources/Domain/Manifestation/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AssetRoot, System.StringComparison.Ordinal)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
        }
    }
}
#endif
