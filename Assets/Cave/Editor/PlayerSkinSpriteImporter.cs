#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Keeps the supplied, individually-cropped skin frames compatible with the existing
    /// player visual envelope. These settings affect presentation only.
    /// </summary>
    public sealed class PlayerSkinSpriteImporter : AssetPostprocessor
    {
        private const string SkinRoot = "Assets/Cave/Resources/Player/Skins/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SkinRoot) || !assetPath.EndsWith(".png")) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 800f;
            importer.spritePivot = new Vector2(.5f, 0f);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            // Unity 2021 exposes single-sprite alignment through the importer
            // settings object rather than TextureImporter.spriteAlignment.
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0f);
            importer.SetTextureSettings(settings);
        }
    }
}
#endif
