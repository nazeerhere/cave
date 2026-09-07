using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>Non-destructively exposes the approved Heart Chamber environmental artwork.</summary>
    public sealed class HeartChamberTileSheetImporter : AssetPostprocessor
    {
        public const string StructuralSheetPath = "Assets/Cave/Art/Rooms/HeartChamber/HeartChamberStructuralSheet_Source.png";
        public const string CrystalWaterSheetPath = "Assets/Cave/Art/Rooms/HeartChamber/HeartChamberCrystalWaterSheet_Source.png";
        private const int SheetHeight = 1086;

        private void OnPreprocessTexture()
        {
            if (assetPath != StructuralSheetPath && assetPath != CrystalWaterSheetPath)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritesheet = assetPath == StructuralSheetPath
                ? CreateStructuralMetadata()
                : CreateCrystalWaterMetadata();
        }

        private static SpriteMetaData[] CreateStructuralMetadata()
        {
            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            Add(sprites, "Heart_EntryCave", 0, 280, 260, 530);
            Add(sprites, "Heart_CeilingLeft", 0, 0, 315, 260);
            Add(sprites, "Heart_CeilingCenter", 320, 0, 330, 205);
            Add(sprites, "Heart_CeilingRight", 1040, 0, 408, 285);
            Add(sprites, "Heart_AncientThreshold", 585, 40, 290, 275);
            Add(sprites, "Heart_LeftLedge", 235, 315, 365, 190);
            Add(sprites, "Heart_RightLedge", 845, 315, 380, 195);
            Add(sprites, "Heart_EntryDoor", 90, 500, 300, 215);
            Add(sprites, "Heart_BackgroundPool", 320, 655, 800, 132);
            Add(sprites, "Heart_CentralArena", 320, 775, 800, 205);
            Add(sprites, "Heart_FloorFragments", 0, 805, 1448, 278);
            return sprites.ToArray();
        }

        private static SpriteMetaData[] CreateCrystalWaterMetadata()
        {
            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            Add(sprites, "Heart_CrystalField", 0, 0, 650, 238);
            Add(sprites, "Heart_StalactiteCrown", 635, 0, 813, 238);
            Add(sprites, "Heart_CyanVeins", 445, 240, 220, 250);
            Add(sprites, "Heart_PurpleVeins", 660, 245, 335, 245);
            Add(sprites, "Heart_RightVeins", 995, 240, 380, 255);
            Add(sprites, "Heart_RockSpines", 690, 450, 390, 205);
            Add(sprites, "Heart_WaterPoolDetail", 0, 605, 730, 198);
            Add(sprites, "Heart_BubbleRiser", 660, 735, 330, 138);
            Add(sprites, "Heart_ReverseWaterColumn", 1090, 545, 205, 270);
            Add(sprites, "Heart_MistColumn", 1270, 545, 178, 280);
            Add(sprites, "Heart_LivingCaveBase", 0, 800, 600, 286);
            Add(sprites, "Heart_CrystalRubble", 860, 820, 475, 260);
            return sprites.ToArray();
        }

        private static void Add(List<SpriteMetaData> sprites, string name, int x, int top, int width, int height)
        {
            sprites.Add(new SpriteMetaData
            {
                name = name,
                rect = new Rect(x, SheetHeight - top - height, width, height),
                alignment = (int)SpriteAlignment.BottomCenter,
                pivot = new Vector2(0.5f, 0f)
            });
        }
    }
}
