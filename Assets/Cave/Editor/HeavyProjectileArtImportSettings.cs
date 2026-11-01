#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Preserves the approved Heavy sheets for runtime rect slicing.</summary>
    internal sealed class HeavyProjectileArtImportSettings : AssetPostprocessor
    {
        private const string Root = "Assets/Cave/Resources/Projectiles/Heavy/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            Configure((TextureImporter)assetImporter);
        }

        [InitializeOnLoadMethod]
        private static void ConfigureExistingSheets()
        {
            EditorApplication.delayCall += () =>
            {
                string[] paths =
                {
                    Root + "Travel/heavy_projectiles.png",
                    Root + "Fallout/fire_fallout.png",
                    Root + "Fallout/ice_fallout.png",
                    Root + "Fallout/wind_fallout.png",
                    Root + "Fallout/strength_fallout.png",
                    Root + "Fallout/imaginary_fallout.png"
                };

                for (int index = 0; index < paths.Length; index++)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(paths[index]) as TextureImporter;
                    if (importer == null || IsConfigured(importer)) continue;
                    Configure(importer);
                    importer.SaveAndReimport();
                }
            };
        }

        private static bool IsConfigured(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Default
                && !importer.mipmapEnabled
                && importer.filterMode == FilterMode.Point
                && importer.npotScale == TextureImporterNPOTScale.None
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.alphaIsTransparency;
        }

        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }
    }
}
#endif
