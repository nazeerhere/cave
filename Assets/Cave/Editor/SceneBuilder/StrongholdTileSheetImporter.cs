using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>
    /// Non-destructively exposes the approved Stronghold sheets as the first-pass room pieces.
    /// Coordinates intentionally stay tied to the supplied 1254 x 1254 source sheets.
    /// </summary>
    public sealed class StrongholdTileSheetImporter : AssetPostprocessor
    {
        public const string StructuralSheetPath = "Assets/Cave/Art/Rooms/Stronghold/StrongholdStructuralSheet_Source.png";
        public const string PropsSheetPath = "Assets/Cave/Art/Rooms/Stronghold/StrongholdPropsSheet_Source.png";

        private const int SheetHeight = 1254;

        private void OnPreprocessTexture()
        {
            if (assetPath != StructuralSheetPath && assetPath != PropsSheetPath)
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
                : CreatePropsMetadata();
        }

        private static SpriteMetaData[] CreateStructuralMetadata()
        {
            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            Add(sprites, "Stronghold_CaveWall", 12, 8, 292, 402);
            Add(sprites, "Stronghold_FortifiedWall", 305, 7, 454, 430);
            Add(sprites, "Stronghold_BarracksBlock", 8, 428, 298, 255);
            Add(sprites, "Stronghold_StoneFloor", 12, 644, 284, 86);
            Add(sprites, "Stronghold_StoneLedge", 304, 648, 207, 85);
            Add(sprites, "Stronghold_StairRise", 577, 294, 268, 238);
            Add(sprites, "Stronghold_SuspendedWalkway", 767, 246, 466, 177);
            Add(sprites, "Stronghold_MarketDeck", 296, 706, 486, 190);
            Add(sprites, "Stronghold_RoyalHall", 566, 900, 297, 340);
            Add(sprites, "Stronghold_FightingPit", 858, 892, 300, 348);
            Add(sprites, "Stronghold_ExitTower", 1114, 748, 138, 495);
            Add(sprites, "Stronghold_HallwayWall", 983, 537, 271, 211);
            Add(sprites, "Stronghold_CrystalCluster", 728, 568, 166, 128);
            Add(sprites, "Stronghold_ForegroundRocks", 0, 1184, 650, 66);
            return sprites.ToArray();
        }

        private static SpriteMetaData[] CreatePropsMetadata()
        {
            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            Add(sprites, "Stronghold_BarracksSupplies", 8, 6, 550, 202);
            Add(sprites, "Stronghold_MarketStalls", 10, 208, 588, 416);
            Add(sprites, "Stronghold_MarketAwning", 12, 354, 270, 193);
            Add(sprites, "Stronghold_SlumShelters", 0, 642, 542, 238);
            Add(sprites, "Stronghold_BannerSet", 500, 0, 172, 201);
            Add(sprites, "Stronghold_LanternSet", 629, 115, 190, 121);
            Add(sprites, "Stronghold_CrateBarrelSet", 1001, 0, 245, 203);
            Add(sprites, "Stronghold_Crane", 12, 964, 180, 273);
            Add(sprites, "Stronghold_MineCart", 13, 1111, 174, 133);
            Add(sprites, "Stronghold_CrystalField", 470, 836, 412, 212);
            Add(sprites, "Stronghold_IndustrialRig", 1030, 835, 223, 411);
            Add(sprites, "Stronghold_RopePosts", 1026, 1026, 226, 218);
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
