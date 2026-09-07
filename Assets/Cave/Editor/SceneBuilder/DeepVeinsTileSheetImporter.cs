using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>Non-destructively exposes selected approved Deep Veins environment pieces.</summary>
    public sealed class DeepVeinsTileSheetImporter : AssetPostprocessor
    {
        public const string SheetPath = "Assets/Cave/Art/Rooms/DeepVeins/DeepVeinsEnvironmentSheet_Source.png";
        private const int SheetHeight = 1024;

        private void OnPreprocessTexture()
        {
            if (assetPath != SheetPath)
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
            importer.spritesheet = CreateSpriteMetadata();
        }

        private static SpriteMetaData[] CreateSpriteMetadata()
        {
            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            // Bounds stay below the specimen-sheet headings and exclude its player/enemy/UI examples.
            Add(sprites, "Veins_AncientTileBand", 498, 84, 210, 125);
            Add(sprites, "Veins_BrokenPlatform", 715, 82, 195, 129);
            Add(sprites, "Veins_RuinStairs", 900, 83, 135, 132);
            Add(sprites, "Veins_ArchAndPillars", 1040, 83, 122, 140);
            Add(sprites, "Veins_AncientRuinFacade", 498, 238, 650, 124);
            Add(sprites, "Veins_CrystalLedge", 498, 398, 354, 134);
            Add(sprites, "Veins_LavaAndEarth", 862, 398, 292, 134);
            Add(sprites, "Veins_RuinDepth", 500, 570, 655, 174);
            Add(sprites, "Veins_CaveFarBackground", 500, 790, 160, 108);
            Add(sprites, "Veins_RuinFarBackground", 666, 790, 160, 108);
            Add(sprites, "Veins_CrystalLavaBackground", 832, 790, 160, 108);
            Add(sprites, "Veins_ForegroundVeins", 997, 790, 160, 108);
            Add(sprites, "Veins_AncientMonument", 1175, 84, 165, 116);
            // Begin below the specimen-sheet "Banners / Relics" heading; this slice is used as crystal dressing.
            Add(sprites, "Veins_CrystalProps", 1175, 218, 160, 103);
            Add(sprites, "Veins_LavaPool", 1175, 365, 165, 73);
            Add(sprites, "Veins_Lavafall", 1360, 366, 166, 88);
            Add(sprites, "Veins_HangingLanterns", 1342, 205, 97, 108);
            Add(sprites, "Veins_Rubble", 1440, 205, 88, 108);
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
