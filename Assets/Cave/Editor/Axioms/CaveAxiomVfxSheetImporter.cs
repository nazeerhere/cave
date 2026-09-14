#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Non-destructive import policy for the approved Axiom VFX source sheet.</summary>
    public sealed class CaveAxiomVfxSheetImporter : AssetPostprocessor
    {
        internal const string SheetPath = "Assets/Cave/Art/VFX/Axioms/CaveAxiomVfxSheet.png";

        private void OnPreprocessTexture()
        {
            if (assetPath != SheetPath)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 512f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.spritesheet = BuildSpriteSheet();
        }

        private static SpriteMetaData[] BuildSpriteSheet()
        {
            // The supplied 1254px source is an exact 3 x 418 grid. Explicit rects
            // preserve the full transparent canvas for each approved effect.
            int[] starts = { 0, 418, 836 };
            int[] sizes = { 418, 418, 418 };
            string[,] names =
            {
                { "Axiom_PhaseCrystal", "Axiom_Recohere", "Axiom_Glitch" },
                { "Axiom_Heat", "Axiom_Order", "Axiom_Flow" },
                { "Axiom_CyanRing", "Axiom_CyanFracture", "Axiom_Void" }
            };

            SpriteMetaData[] sprites = new SpriteMetaData[9];
            int index = 0;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    sprites[index] = new SpriteMetaData
                    {
                        name = names[row, column],
                        rect = new Rect(starts[column], starts[row], sizes[column], sizes[row]),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(.5f, .5f)
                    };
                    index++;
                }
            }
            return sprites;
        }
    }
}
#endif
