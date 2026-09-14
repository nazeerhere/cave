#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Deterministically imports the approved Brute presentation
    /// sheets. Coordinates were measured from the supplied source art; every
    /// sprite uses a bottom-centre pivot so body placement is stable.
    /// </summary>
    public sealed class ApprovedEnemySheetImporter : AssetPostprocessor
    {
        public const string BruteSheetPath =
            "Assets/Cave/Art/Enemies/Brute/brutish_fighter_pixel_sprite_sheet.png";
        public const string CorruptBruteSheetPath =
            "Assets/Cave/Art/Enemies/CorruptBrute/corrupted_brute_pixel_sprite_sheet.png";
        private void OnPreprocessTexture()
        {
            SpriteMetaData[] slices = BuildSlices(assetPath);
            if (slices == null)
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
            importer.spritePixelsPerUnit = 128f;
            importer.spritesheet = slices;
        }

        [MenuItem("Tools/Cave/Enemies/Import Approved Enemy Sheets")]
        public static void ImportApprovedSheets()
        {
            Import(BruteSheetPath);
            Import(CorruptBruteSheetPath);
            Debug.Log("[Cave] Imported the approved Brute presentation sheets. Detective and Anti-Pyre use their dedicated canonical processors.");
        }

        internal static SpriteMetaData[] BuildSlices(string path)
        {
            if (path == BruteSheetPath)
            {
                return BuildBruteSlices();
            }

            if (path == CorruptBruteSheetPath)
            {
                return BuildCorruptBruteSlices();
            }

            return null;
        }

        private static void Import(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                Debug.LogWarning("[Cave] Approved enemy sheet is missing: " + path);
                return;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static SpriteMetaData[] BuildBruteSlices()
        {
            const int width = 1536;
            const int height = 1024;
            var slices = new List<SpriteMetaData>(46);
            AddRow(slices, "Brute_Idle_", width, height, 8, 155, 175,
                140, 370, 600, 830, 1060, 1290);
            AddRow(slices, "Brute_Walk_", width, height, 166, 145, 165,
                160, 350, 540, 730, 920, 1110, 1300, 1490);
            AddRow(slices, "Brute_Run_", width, height, 325, 150, 175,
                148, 340, 532, 724, 916, 1108, 1300, 1492);
            AddRow(slices, "Brute_Light_", width, height, 478, 155, 210,
                158, 392, 626, 860, 1094, 1328);
            AddRow(slices, "Brute_Heavy_", width, height, 632, 185, 190,
                144, 338, 532, 726, 920, 1114, 1308, 1502);
            AddRow(slices, "Brute_Hurt_", width, height, 822, 125, 230,
                154, 536, 918, 1300);
            AddRow(slices, "Brute_Death_", width, height, 940, 84, 235,
                126, 404, 682, 960, 1238, 1516);
            return slices.ToArray();
        }

        private static SpriteMetaData[] BuildCorruptBruteSlices()
        {
            const int width = 1448;
            const int height = 1086;
            var slices = new List<SpriteMetaData>(33);
            // The Idle crop begins at the source top so the corrupted crown and
            // its upper spikes are never clipped.
            AddRow(slices, "CorruptBrute_Idle_", width, height, 0, 172, 185,
                250, 465, 680, 895);
            AddRow(slices, "CorruptBrute_Walk_", width, height, 145, 175, 190,
                258, 476, 694, 912, 1130, 1348);
            AddRow(slices, "CorruptBrute_Light_", width, height, 312, 165, 220,
                252, 500, 748, 996);
            AddRow(slices, "CorruptBrute_Heavy_", width, height, 465, 212, 200,
                192, 426, 660, 894, 1128, 1362);
            AddRow(slices, "CorruptBrute_Grab_", width, height, 665, 170, 220,
                214, 474, 734, 994, 1254);
            AddRow(slices, "CorruptBrute_Hurt_", width, height, 818, 140, 235,
                250, 692, 1134);
            AddRow(slices, "CorruptBrute_Death_", width, height, 940, 146, 245,
                160, 450, 740, 1030, 1320);
            return slices.ToArray();
        }

        private static void AddRow(
            ICollection<SpriteMetaData> slices,
            string prefix,
            int sheetWidth,
            int sheetHeight,
            int top,
            int frameHeight,
            int frameWidth,
            params int[] centers)
        {
            for (int index = 0; index < centers.Length; index++)
            {
                int left = Mathf.Clamp(centers[index] - frameWidth / 2, 0, sheetWidth - frameWidth);
                slices.Add(new SpriteMetaData
                {
                    name = prefix + (index + 1).ToString("00"),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0.04f),
                    rect = new Rect(left, sheetHeight - top - frameHeight, frameWidth, frameHeight)
                });
            }
        }
    }
}
#endif
