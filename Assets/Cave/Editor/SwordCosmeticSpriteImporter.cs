#if UNITY_EDITOR
using UnityEditor;

namespace Cave.Editor
{
    /// <summary>Imports the three approved standalone sword cosmetics without touching combat assets.</summary>
    public sealed class SwordCosmeticSpriteImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Cave/Resources/Cosmetics/Swords/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".png"))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024f;
            importer.spritePivot = new UnityEngine.Vector2(0.5f, 0.5f);
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }
    }
}
#endif
