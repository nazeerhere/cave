using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>
    /// Deterministic extraction manifest for the approved compact HUD atlas.
    /// Crops preserve the source alpha and dark recessed interiors exactly.
    /// </summary>
    public static class CompactHudArtExtractor
    {
        private const string SourcePath = "Assets/Cave/Resources/UI/HudCompact/Source/FantasyRpgPixelHudSpriteSheet.png";
        private const string FramePath = "Assets/Cave/Resources/UI/HudCompact/Frames/";
        private const string IconPath = "Assets/Cave/Resources/UI/HudCompact/Icons/";

        private struct Crop
        {
            internal readonly string Name;
            internal readonly RectInt TopLeftRect;
            internal readonly bool IsFrame;
            internal Crop(string name, int x, int top, int width, int height, bool frame)
            {
                Name = name;
                TopLeftRect = new RectInt(x, top, width, height);
                IsFrame = frame;
            }
        }

        // Coordinates are reviewed against the supplied 1448x1086 atlas.
        // Each panel crop retains its own inset and ornamental border.
        private static readonly Crop[] Manifest =
        {
            new Crop("CurrencyPanel", 38, 18, 666, 257, true),
            new Crop("GeneralShardsPanel", 744, 18, 666, 257, true),
            new Crop("SkillPathPanel", 38, 286, 666, 257, true),
            new Crop("FirePatternPanel", 744, 286, 666, 257, true),
            new Crop("DomainChargePanel", 38, 548, 1372, 256, true),
            new Crop("GoldCurrencyIcon", 56, 824, 145, 151, false),
            new Crop("GeneralShardsIcon", 218, 822, 139, 154, false),
            new Crop("SkillPathIcon", 370, 820, 142, 157, false),
            new Crop("FirePatternIcon", 510, 820, 142, 157, false),
            new Crop("DomainChargeIcon", 656, 812, 154, 170, false),
            new Crop("OrnamentalSeparator", 878, 838, 220, 75, false)
        };

        [MenuItem("Tools/Cave/Art/Extract Compact HUD Atlas")]
        public static void Extract()
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (source == null || !source.isReadable)
            {
                throw new InvalidOperationException("Compact HUD source is missing or not readable: " + SourcePath);
            }

            Color32[] allPixels = source.GetPixels32();
            foreach (Crop crop in Manifest)
            {
                if (crop.TopLeftRect.xMin < 0 || crop.TopLeftRect.yMin < 0
                    || crop.TopLeftRect.xMax > source.width || crop.TopLeftRect.yMax > source.height)
                {
                    throw new InvalidOperationException("Compact HUD crop is out of source bounds: " + crop.Name);
                }

                string destination = (crop.IsFrame ? FramePath : IconPath) + crop.Name + ".png";
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                Texture2D extracted = new Texture2D(crop.TopLeftRect.width, crop.TopLeftRect.height, TextureFormat.RGBA32, false);
                extracted.SetPixels32(CopyTopLeftCrop(allPixels, source.width, source.height, crop.TopLeftRect));
                extracted.Apply(false, false);
                File.WriteAllBytes(destination, extracted.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(extracted);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
                ConfigureOutput(destination, crop.IsFrame);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Cave] Compact HUD atlas extraction PASS: " + Manifest.Length + " crops.");
        }

        private static Color32[] CopyTopLeftCrop(Color32[] source, int sourceWidth, int sourceHeight, RectInt topLeft)
        {
            Color32[] result = new Color32[topLeft.width * topLeft.height];
            int sourceBottom = sourceHeight - topLeft.y - topLeft.height;
            for (int row = 0; row < topLeft.height; row++)
            {
                Array.Copy(source, (sourceBottom + row) * sourceWidth + topLeft.x,
                    result, row * topLeft.width, topLeft.width);
            }
            return result;
        }

        private static void ConfigureOutput(string path, bool isFrame)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteBorder = isFrame ? new Vector4(34f, 28f, 34f, 28f) : Vector4.zero;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
