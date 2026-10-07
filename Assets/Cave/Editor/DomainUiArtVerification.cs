#if UNITY_EDITOR
using System;
using System.IO;
using Cave.UI;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Source and import guard for the deterministic Domain atlas
    /// extraction. It validates presentation assets only; it never changes
    /// gameplay or invokes the extractor.</summary>
    public static class DomainUiArtVerification
    {
        private const string Root = "Assets/Cave/Art/UI/Domain";
        private const string FrameSource = Root + "/Source/gothic_gold_ui_asset_atlas.png";
        private const string IconSource = Root + "/Source/ornate_gold_fantasy_ui_icon_atlas.png";
        private const string SkinPath = "Assets/Cave/Resources/UI/Domain/DomainUiSkin.asset";

        [MenuItem("Tools/Cave/Verification/Run Domain UI Art Verification")]
        private static void RunFromMenu()
        {
            string failure;
            if (TryRunAll(out failure))
            {
                Debug.Log("[Cave] DOMAIN UI ART PASS");
                return;
            }
            Debug.LogError("[Cave] Domain UI art verification failed: " + failure);
        }

        public static bool TryRunAll(out string failure)
        {
            if (!VerifyOpaqueSource(FrameSource, out failure) || !VerifyOpaqueSource(IconSource, out failure)) return false;

            string[] paths = DomainUiAtlasExtractor.GetExpectedOutputPaths();
            if (paths.Length != 34) { failure = "The Domain crop manifest count changed unexpectedly."; return false; }
            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
                if (!path.StartsWith(Root + "/Frames/") && !path.StartsWith(Root + "/Bars/")
                    && !path.StartsWith(Root + "/Decorations/") && !path.StartsWith(Root + "/Icons/"))
                { failure = "Manifest output is outside its visual role directory: " + path; return false; }
                if (!File.Exists(path)) { failure = "Missing extracted sprite: " + path; return false; }
                if (!VerifyExtractedSprite(path, out failure)) return false;
            }

            if (!VerifyBorder("Frames/LargePanel", out failure)
                || !VerifyBorder("Frames/MediumPanel", out failure)
                || !VerifyBorder("Frames/NavigationFrame", out failure)
                || !VerifyBorder("Frames/CardFrame", out failure)
                || !VerifyBorder("Frames/ActionButtonFrame", out failure)
                || !VerifyBorder("Bars/ProgressBar", out failure)
                || !VerifyBorder("Bars/ReserveBar", out failure)) return false;

            DomainUiSkin skin = AssetDatabase.LoadAssetAtPath<DomainUiSkin>(SkinPath);
            if (skin == null) { failure = "DomainUiSkin was not built from the extracted sprites."; return false; }
            if (skin.ProgressBar == null || skin.ReserveBar == null || skin.ReserveIcon == null || skin.Divider == null)
            { failure = "DomainUiSkin is missing a required extracted presentation reference."; return false; }
            failure = null;
            return true;
        }

        private static bool VerifyOpaqueSource(string path, out string failure)
        {
            if (!File.Exists(path)) { failure = "Missing approved source atlas: " + path; return false; }
            Texture2D texture = Decode(path);
            if (texture == null) { failure = "Could not decode approved source atlas: " + path; return false; }
            try
            {
                if (texture.width != 1448 || texture.height != 1086)
                { failure = "Unexpected source atlas dimensions: " + path; return false; }
                Color32[] pixels = texture.GetPixels32();
                for (int index = 0; index < pixels.Length; index++)
                {
                    if (pixels[index].a != byte.MaxValue)
                    { failure = "Approved source atlas is not opaque RGB: " + path; return false; }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            failure = null;
            return true;
        }

        private static bool VerifyExtractedSprite(string path, out string failure)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || !importer.alphaIsTransparency)
            { failure = "Incorrect UI sprite import settings: " + path; return false; }

            Texture2D texture = Decode(path);
            if (texture == null) { failure = "Could not decode extracted sprite: " + path; return false; }
            try
            {
                Color32[] pixels = texture.GetPixels32();
                bool transparent = false;
                bool visible = false;
                for (int index = 0; index < pixels.Length; index++)
                {
                    transparent |= pixels[index].a == 0;
                    visible |= pixels[index].a != 0;
                }
                if (!transparent || !visible)
                { failure = "Connected-background masking did not preserve both transparent exterior and visible artwork: " + path; return false; }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            failure = null;
            return true;
        }

        private static bool VerifyBorder(string relativePath, out string failure)
        {
            TextureImporter importer = AssetImporter.GetAtPath(Root + "/" + relativePath + ".png") as TextureImporter;
            if (importer == null || importer.spriteBorder == Vector4.zero)
            { failure = "Missing nine-slice border on resizable Domain artwork: " + relativePath; return false; }
            failure = null;
            return true;
        }

        private static Texture2D Decode(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return texture.LoadImage(bytes, false) ? texture : null;
        }
    }
}
#endif
