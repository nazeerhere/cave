using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Deterministic import contract for the two approved manifestation sprites.</summary>
    public sealed class ManifestationVisualImportSettings : AssetPostprocessor
    {
        public const string AnchorPath =
            "Assets/Cave/Resources/Projectiles/Manifestations/Anchor_Basilisk_Gate.png";
        public const string OrbitalPath =
            "Assets/Cave/Resources/Projectiles/Manifestations/Orbital_Gilded_Illusion.png";
        public const float PixelsPerUnit = 256f;

        private void OnPreprocessTexture()
        {
            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null) return;
            if (assetPath == AnchorPath)
            {
                Apply(importer, new Vector2(.5f, 0f));
            }
            else if (assetPath == OrbitalPath)
            {
                Apply(importer, new Vector2(.5f, .5f));
            }
        }

        [MenuItem("Cave/Art/Manifestation Visuals/Apply Import Settings")]
        public static void ApplyAll()
        {
            AssetDatabase.ImportAsset(AnchorPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(OrbitalPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Applied approved Manifestation Origin sprite import settings.");
        }

        private static void Apply(TextureImporter importer, Vector2 pivot)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = 2048;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
        }
    }
}
