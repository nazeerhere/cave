#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Scoped importer for the three approved two-handed cosmetic weapon sources.</summary>
    internal sealed class LegendaryWeaponImportSettings : AssetPostprocessor
    {
        private const string Root = "Assets/Cave/Resources/Cosmetics/Weapons/";

        private void OnPreprocessTexture()
        {
            Vector2 pivot;
            if (!TryGetGripPivot(assetPath, out pivot)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024f;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
        }

        private static bool TryGetGripPivot(string path, out Vector2 pivot)
        {
            switch (path)
            {
                case Root + "Astral_Nocturne.png":
                    pivot = new Vector2(.5f, .32f);
                    return true;
                case Root + "Heavens_Defiance.png":
                    pivot = new Vector2(.5f, .35f);
                    return true;
                case Root + "Abyssal_Ossuary.png":
                    pivot = new Vector2(.5f, .47f);
                    return true;
                default:
                    pivot = Vector2.zero;
                    return false;
            }
        }
    }
}
#endif
