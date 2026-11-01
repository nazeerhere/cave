using System;
using System.IO;
using Cave.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class ManifestationVisualVerificationMenu
    {
        [MenuItem("Cave/Verification/Manifestation Visual Replacement")]
        public static void Run()
        {
            ManifestationVisualImportSettings.ApplyAll();
            VerifySprite(ManifestationVisualImportSettings.AnchorPath,
                new Vector2(.5f, 0f), "Anchor");
            VerifySprite(ManifestationVisualImportSettings.OrbitalPath,
                new Vector2(.5f, .5f), "Orbital");

            string failure;
            if (!ManifestationOriginVerification.TryRunAll(out failure))
                throw new InvalidOperationException(failure);

            Debug.Log("[Cave] Manifestation visual replacement verification passed: "
                + "RGBA assets, import settings, pivots, resource references, and gameplay baselines are valid.");
        }

        private static void VerifySprite(string path, Vector2 expectedPivot, string label)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException(label + " approved sprite is missing at " + path + ".");

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(label + " does not have a TextureImporter.");
            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit,
                    ManifestationVisualImportSettings.PixelsPerUnit)
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.filterMode != FilterMode.Bilinear
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                throw new InvalidOperationException(label + " import settings do not match the approved contract.");
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom
                || (settings.spritePivot - expectedPivot).sqrMagnitude > .0001f)
            {
                throw new InvalidOperationException(label + " pivot is not " + expectedPivot + ".");
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.texture == null)
                throw new InvalidOperationException(label + " did not import as a loadable Sprite.");

            string resourcePath = path.Substring("Assets/Cave/Resources/".Length);
            resourcePath = resourcePath.Substring(0, resourcePath.Length - Path.GetExtension(path).Length);
            if (Resources.Load<Sprite>(resourcePath) == null)
                throw new InvalidOperationException(label + " does not resolve from Resources at "
                    + resourcePath + ".");

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(path), false))
                    throw new InvalidOperationException(label + " PNG could not be decoded.");
                Color32[] pixels = source.GetPixels32();
                int width = source.width;
                int height = source.height;
                if (pixels[0].a > 8 || pixels[width - 1].a > 8
                    || pixels[(height - 1) * width].a > 8 || pixels[pixels.Length - 1].a > 8)
                {
                    throw new InvalidOperationException(label + " retains opaque pixels in a canvas corner.");
                }

                int visible = 0;
                int translucent = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    byte alpha = pixels[index].a;
                    if (alpha > 8) visible++;
                    if (alpha > 8 && alpha < 247) translucent++;
                }
                if (visible < pixels.Length / 20 || translucent < pixels.Length / 200)
                    throw new InvalidOperationException(label
                        + " transparency mask lost either the subject or its antialiased/glowing edges.");

                if (label == "Anchor")
                {
                    Color32 portalInterior = pixels[(height / 2) * width + width / 2];
                    if (portalInterior.a < 220)
                        throw new InvalidOperationException(
                            "Anchor portal interior became transparent instead of remaining dimensional darkness.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
