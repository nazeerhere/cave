#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Slices the approved crystal art as four stages by four damage bands.</summary>
    public sealed class DetectiveTowerCrystalSheetImporter : AssetPostprocessor
    {
        public const string SheetPath = "Assets/Cave/Resources/Towers/DetectiveTowerCrystalSheet.png";
        private static bool reimportQueued;

        [InitializeOnLoadMethod]
        private static void ValidateAfterReload()
        {
            EditorApplication.delayCall += ApplyFourByFourSlices;
        }

        private void OnPreprocessTexture()
        {
            if (assetPath != SheetPath) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 192f;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (reimportQueued) return;
            for (int index = 0; index < importedAssets.Length; index++)
            {
                if (importedAssets[index] == SheetPath)
                {
                    reimportQueued = true;
                    EditorApplication.delayCall += ApplyFourByFourSlices;
                    return;
                }
            }
        }

        private static void ApplyFourByFourSlices()
        {
            reimportQueued = false;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (texture == null || importer == null || texture.width < 4 || texture.height < 4) return;

            SpriteMetaData[] desired = BuildSlices(texture.width, texture.height);
            if (Matches(importer.spritesheet, desired)) return;
            importer.spritesheet = desired;
            importer.SaveAndReimport();
        }

        private static SpriteMetaData[] BuildSlices(int width, int height)
        {
            var slices = new SpriteMetaData[16];
            int index = 0;
            for (int damage = 0; damage < 4; damage++)
            {
                for (int stage = 0; stage < 4; stage++)
                {
                    int left = Mathf.RoundToInt(stage * width / 4f);
                    int right = Mathf.RoundToInt((stage + 1) * width / 4f);
                    int top = Mathf.RoundToInt(damage * height / 4f);
                    int bottom = Mathf.RoundToInt((damage + 1) * height / 4f);
                    slices[index++] = new SpriteMetaData
                    {
                        name = "Tower_Stage" + (stage + 1).ToString("00")
                            + "_Damage" + (damage + 1).ToString("00"),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, 0.02f),
                        rect = new Rect(left, height - bottom, right - left, bottom - top)
                    };
                }
            }
            return slices;
        }

        private static bool Matches(SpriteMetaData[] current, SpriteMetaData[] desired)
        {
            if (current == null || current.Length != desired.Length) return false;
            for (int index = 0; index < desired.Length; index++)
            {
                if (current[index].name != desired[index].name
                    || current[index].rect != desired[index].rect
                    || current[index].pivot != desired[index].pivot) return false;
            }
            return true;
        }
    }
}
#endif
