#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Imports the approved, transparent Miner body sheet used by the live player.</summary>
    public sealed class MinerPlayerSpriteSheetImporter : AssetPostprocessor
    {
        private const string SheetPath =
            "Assets/Cave/Resources/Player/MinerFullActionSheet.png";

        private void OnPreprocessTexture()
        {
            if (assetPath != SheetPath)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 160f;
            importer.spritesheet = BuildSlices();
        }

        private static SpriteMetaData[] BuildSlices()
        {
            var slices = new List<SpriteMetaData>(63);
            // Rectangles were measured against the approved 1024x1024 body sheet.
            // They deliberately omit separate weapons, projectiles, pickups and props.
            AddRow(slices, "Miner_Idle_", 40, 140, 56, 51, 110, 169, 229);
            AddRow(slices, "Miner_Run_", 34, 146, 72, 328, 394, 460, 523, 585);
            AddRow(slices, "Miner_Jump_", 32, 148, 69, 682, 747, 812, 872, 931, 988);
            AddRow(slices, "Miner_Heavy_", 247, 146, 71, 43, 121, 200, 287, 372, 459);
            AddRow(slices, "Miner_Spin_", 245, 150, 72, 553, 627, 701, 776, 837, 898, 967);
            AddRow(slices, "Miner_Guard_", 452, 130, 63, 45, 112, 180);
            AddRow(slices, "Miner_Parry_", 452, 130, 63, 279, 348, 417);
            AddRow(slices, "Miner_GuardBreak_", 452, 130, 67, 512, 578, 644);
            AddRow(slices, "Miner_Dash_", 452, 130, 75, 777, 886, 969);
            AddRow(slices, "Miner_Hit_", 650, 132, 67, 121, 201, 282, 363);
            AddRow(slices, "Miner_Knockback_", 650, 132, 75, 574, 648, 722, 799, 887);
            AddRow(slices, "Miner_Death_", 846, 154, 72, 44, 118, 185, 261);
            AddRow(slices, "Miner_Interact_", 846, 154, 68, 322, 392, 456);
            AddRow(slices, "Miner_Cast_", 846, 154, 68, 524, 598, 670);
            AddRow(slices, "Miner_Landing_", 846, 154, 68, 775, 849, 925);
            AddRow(slices, "Miner_Brace_", 846, 154, 65, 932, 990);
            return slices.ToArray();
        }

        private static void AddRow(
            ICollection<SpriteMetaData> slices,
            string prefix,
            int top,
            int height,
            int width,
            params int[] centers)
        {
            const int sheetSize = 1024;
            for (int index = 0; index < centers.Length; index++)
            {
                int x = Mathf.Clamp(centers[index] - width / 2, 0, sheetSize - width);
                slices.Add(new SpriteMetaData
                {
                    name = prefix + (index + 1).ToString("00"),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0.06f),
                    rect = new Rect(x, sheetSize - top - height, width, height)
                });
            }
        }
    }
}
#endif
