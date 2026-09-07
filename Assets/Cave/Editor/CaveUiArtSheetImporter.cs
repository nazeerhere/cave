#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Imports the approved Cave UI sheets once, with named reusable slices.</summary>
    public sealed class CaveUiArtSheetImporter : AssetPostprocessor
    {
        private const string ChromeSheet = "Assets/Cave/Resources/UI/Shared/CaveUiChromeSheet.png";
        private const string IconSheet = "Assets/Cave/Resources/UI/Icons/CaveUiIconSheet.png";
        private static bool importQueued;

        [InitializeOnLoadMethod]
        private static void ValidateAfterDomainReload()
        {
            EditorApplication.delayCall += ApplySlices;
        }

        private void OnPreprocessTexture()
        {
            if (assetPath != ChromeSheet && assetPath != IconSheet)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importQueued)
            {
                return;
            }

            for (int index = 0; index < importedAssets.Length; index++)
            {
                if (importedAssets[index] == ChromeSheet || importedAssets[index] == IconSheet)
                {
                    importQueued = true;
                    EditorApplication.delayCall += ApplySlices;
                    return;
                }
            }
        }

        private static void ApplySlices()
        {
            importQueued = false;
            Apply(ChromeSheet, ChromeSlices());
            Apply(IconSheet, IconSlices());
        }

        private static void Apply(string path, SpriteMetaData[] desired)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || Matches(importer.spritesheet, desired))
            {
                return;
            }

            importer.spritesheet = desired;
            importer.SaveAndReimport();
        }

        private static SpriteMetaData[] ChromeSlices()
        {
            return new[]
            {
                Slice("Ui_MainFrame", 18, 12, 650, 735, 34),
                Slice("Ui_TabNormal", 702, 46, 264, 76, 18),
                Slice("Ui_TabActive", 702, 145, 264, 76, 18),
                Slice("Ui_HeaderStrip", 694, 252, 540, 66, 18),
                Slice("Ui_Row", 692, 529, 544, 92, 18),
                Slice("Ui_Button", 745, 892, 144, 52, 16),
                Slice("Ui_ButtonActive", 848, 1011, 116, 48, 14),
                Slice("Ui_Slot", 18, 754, 148, 150, 18),
                Slice("Ui_SlotActive", 181, 754, 148, 150, 18),
                Slice("Ui_SlotLocked", 515, 917, 148, 150, 18),
                Slice("Ui_Divider", 695, 438, 538, 62, 12)
            };
        }

        private static SpriteMetaData[] IconSlices()
        {
            return new[]
            {
                Slice("Ui_IconMode", 75, 44, 185, 185, 0),
                Slice("Ui_IconSword", 350, 42, 185, 185, 0),
                Slice("Ui_IconMana", 630, 42, 185, 185, 0),
                Slice("Ui_IconShield", 925, 42, 185, 185, 0),
                Slice("Ui_IconHealthPotion", 72, 291, 180, 180, 0),
                Slice("Ui_IconManaPotion", 355, 291, 180, 180, 0),
                Slice("Ui_IconStaminaPotion", 635, 291, 180, 180, 0),
                Slice("Ui_IconVitality", 72, 542, 180, 180, 0),
                Slice("Ui_IconManaReserve", 355, 542, 180, 180, 0),
                Slice("Ui_IconEndurance", 635, 542, 180, 180, 0),
                Slice("Ui_IconFortune", 920, 542, 180, 180, 0),
                Slice("Ui_IconLock", 344, 776, 150, 150, 0),
                Slice("Ui_IconEquipped", 614, 757, 260, 92, 12),
                Slice("Ui_IconOwned", 616, 863, 260, 100, 12),
                Slice("Ui_IconSwordDefault", 68, 952, 178, 178, 0),
                Slice("Ui_IconSwordOne", 244, 952, 178, 178, 0),
                Slice("Ui_IconSwordTwo", 424, 952, 178, 178, 0),
                Slice("Ui_IconSwordThree", 600, 952, 178, 178, 0)
            };
        }

        // Input coordinates use the supplied images' top-left origin.
        private static SpriteMetaData Slice(string name, int x, int top, int width, int height, int border)
        {
            return new SpriteMetaData
            {
                name = name,
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, 0.5f),
                border = new Vector4(border, border, border, border),
                rect = new Rect(x, 1254 - top - height, width, height)
            };
        }

        private static bool Matches(SpriteMetaData[] current, SpriteMetaData[] desired)
        {
            if (current == null || current.Length != desired.Length)
            {
                return false;
            }

            for (int index = 0; index < desired.Length; index++)
            {
                if (current[index].name != desired[index].name || current[index].rect != desired[index].rect)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
