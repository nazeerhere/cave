using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>Non-destructively exposes the approved Breach sheet as named authored-room pieces.</summary>
    public sealed class BreachTileSheetImporter : AssetPostprocessor
    {
        public const string SheetPath = "Assets/Cave/Art/Rooms/Breach/BreachTileSheet_Source.png";
        private const int SheetHeight = 1086;

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
            Add(sprites, "Breach_EntryTunnel", 14, 8, 430, 258);
            Add(sprites, "Breach_EntryTunnelChest", 14, 266, 432, 200);
            Add(sprites, "Breach_RockPlatformSmallA", 470, 142, 174, 136);
            Add(sprites, "Breach_RockPlatformSmallB", 671, 142, 208, 142);
            Add(sprites, "Breach_RockRamp", 895, 63, 272, 198);
            Add(sprites, "Breach_RockPlatformLarge", 1175, 27, 273, 263);
            Add(sprites, "Breach_Bridge", 178, 304, 336, 172);
            Add(sprites, "Breach_ScaffoldTall", 472, 296, 180, 385);
            Add(sprites, "Breach_RockPlatformCenter", 646, 286, 202, 209);
            Add(sprites, "Breach_RockPlatformSmallC", 833, 324, 174, 179);
            Add(sprites, "Breach_StalagmiteTall", 997, 251, 173, 238);
            Add(sprites, "Breach_CityGate", 1171, 250, 277, 244);
            Add(sprites, "Breach_CampTent", 15, 465, 432, 220);
            Add(sprites, "Breach_ScaffoldLower", 402, 456, 205, 225);
            Add(sprites, "Breach_OreCart", 583, 565, 106, 116);
            Add(sprites, "Breach_Crate", 682, 583, 65, 98);
            Add(sprites, "Breach_Barrel", 738, 572, 76, 109);
            Add(sprites, "Breach_LanternPost", 797, 460, 93, 222);
            Add(sprites, "Breach_MiningCrane", 883, 476, 147, 206);
            Add(sprites, "Breach_Banner", 1057, 493, 66, 183);
            Add(sprites, "Breach_MiningWorkbench", 1134, 504, 112, 173);
            Add(sprites, "Breach_MineCartPile", 1223, 543, 225, 140);
            Add(sprites, "Breach_RedCrystalLarge", 18, 666, 145, 137);
            Add(sprites, "Breach_BlueCrystalLarge", 151, 668, 127, 137);
            Add(sprites, "Breach_RedCrystalSmall", 253, 674, 97, 127);
            Add(sprites, "Breach_BlueCrystalSmall", 337, 676, 95, 123);
            Add(sprites, "Breach_EmberOre", 424, 686, 101, 105);
            Add(sprites, "Breach_BlueOre", 510, 689, 98, 101);
            Add(sprites, "Breach_CaveDepthWarm", 757, 783, 282, 162);
            Add(sprites, "Breach_CaveDepthBlue", 1024, 783, 219, 162);
            Add(sprites, "Breach_CaveDepthFar", 1227, 783, 221, 162);
            Add(sprites, "Breach_Brazier", 15, 885, 117, 145);
            Add(sprites, "Breach_RopeFence", 119, 928, 221, 118);
            Add(sprites, "Breach_StoneBlock", 333, 943, 97, 98);
            Add(sprites, "Breach_Supplies", 425, 944, 365, 100);
            Add(sprites, "Breach_ForegroundStalagmites", 790, 940, 658, 146);
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
