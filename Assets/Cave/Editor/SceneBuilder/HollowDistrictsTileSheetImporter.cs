using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor.SceneBuilder
{
    /// <summary>Non-destructively exposes the approved Hollow Districts sheet as named room pieces.</summary>
    public sealed class HollowDistrictsTileSheetImporter : AssetPostprocessor
    {
        public const string SheetPath = "Assets/Cave/Art/Rooms/HollowDistricts/HollowDistrictsEnvironmentSheet_Source.png";
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
            Add(sprites, "Hollow_IrregularFloor", 4, 38, 404, 171);
            Add(sprites, "Hollow_RockShelf", 3, 87, 293, 128);
            Add(sprites, "Hollow_BridgeSegment", 423, 38, 405, 165);
            Add(sprites, "Hollow_StairRamp", 972, 37, 187, 176);
            Add(sprites, "Hollow_StoneSupports", 1174, 37, 270, 179);
            Add(sprites, "Hollow_CaveWall", 2, 267, 319, 150);
            Add(sprites, "Hollow_CatacombWall", 338, 267, 312, 155);
            Add(sprites, "Hollow_DoorArch", 668, 267, 289, 150);
            Add(sprites, "Hollow_TunnelArch", 968, 266, 468, 155);
            // Header text is part of the approved source reference sheet, not room art.
            // These bounds begin below each label and avoid the bottom-right layout guide.
            Add(sprites, "Hollow_LavaLight", 4, 512, 318, 125);
            Add(sprites, "Hollow_HangingLanterns", 338, 508, 248, 124);
            Add(sprites, "Hollow_FactionBanners", 600, 507, 251, 122);
            Add(sprites, "Hollow_MiningDebris", 864, 507, 334, 124);
            Add(sprites, "Hollow_BrokenRubble", 1205, 507, 234, 124);
            Add(sprites, "Hollow_CrystalFlora", 4, 696, 302, 98);
            Add(sprites, "Hollow_TransitionHall", 316, 699, 670, 84);
            Add(sprites, "Hollow_WizardRoom", 4, 806, 374, 270);
            Add(sprites, "Hollow_DetectiveRoom", 390, 806, 350, 270);
            Add(sprites, "Hollow_CatacombHall", 750, 806, 346, 218);
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
