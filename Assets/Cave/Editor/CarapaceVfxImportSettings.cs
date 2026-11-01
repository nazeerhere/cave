using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>
    /// Keeps the source storyboard sheets readable, alpha-preserving, and
    /// uncompressed for the Carapace presenter's runtime frame normalization.
    /// The presenter owns the Sprite instances because it removes only
    /// edge-connected synthetic blue/magenta spill from each frame.
    /// </summary>
    internal sealed class CarapaceVfxImportSettings : AssetPostprocessor
    {
        private const string SourceFolder = "Assets/Cave/Resources/VFX/Carapace/Source/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SourceFolder, System.StringComparison.OrdinalIgnoreCase)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
        }
    }
}
