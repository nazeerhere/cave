using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Idempotent named extraction for the approved Nexus Skill atlas.</summary>
    [InitializeOnLoad]
    public static class NexusSkillIconAtlasExtractor
    {
        private const string SourcePath =
            "Assets/Cave/Art/UI/NexusSkill/Source/Fantasy RPG Skill Icon Atlas.png";
        private const string OutputRoot = "Assets/Cave/Resources/UI/NexusSkill/Icons";

        private readonly struct Crop
        {
            public Crop(string name, int x, int top, int width, int height)
            {
                Name = name; X = x; Top = top; Width = width; Height = height;
            }
            public string Name { get; }
            public int X { get; }
            public int Top { get; }
            public int Width { get; }
            public int Height { get; }
        }

        private static readonly Crop[] Manifest =
        {
            new Crop("SlowShot", 35, 0, 365, 350),
            new Crop("BurnShot", 385, 0, 355, 350),
            // The generated Flight and Strength ornaments touch in the source;
            // stop before Strength's left flourish without clipping either wing.
            new Crop("Flight", 715, 0, 335, 350),
            new Crop("Strength", 1050, 0, 398, 350),
            new Crop("Precision", 25, 325, 285, 285),
            new Crop("Automatic", 300, 325, 290, 285),
            new Crop("Burst", 575, 325, 300, 285),
            new Crop("Volley", 855, 325, 290, 285),
            new Crop("SpoolUp", 1120, 325, 328, 285),
            new Crop("Body", 160, 555, 350, 320),
            new Crop("Anchor", 540, 555, 390, 320),
            new Crop("Orbital", 945, 555, 360, 320),
            new Crop("ManifestationSeed", 160, 830, 230, 256)
        };

        static NexusSkillIconAtlasExtractor()
        {
            EditorApplication.delayCall += ExtractIfAvailable;
        }

        [MenuItem("Cave/UI/Extract Nexus Skill Icons")]
        public static void ExtractIfAvailable()
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (source == null) return;
            TextureImporter sourceImporter = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (sourceImporter == null) return;
            if (!sourceImporter.isReadable
                || sourceImporter.maxTextureSize < 2048
                || sourceImporter.textureCompression != TextureImporterCompression.Uncompressed
                || sourceImporter.npotScale != TextureImporterNPOTScale.None
                || sourceImporter.mipmapEnabled)
            {
                sourceImporter.isReadable = true;
                sourceImporter.maxTextureSize = 2048;
                sourceImporter.textureCompression = TextureImporterCompression.Uncompressed;
                sourceImporter.npotScale = TextureImporterNPOTScale.None;
                sourceImporter.mipmapEnabled = false;
                sourceImporter.alphaIsTransparency = true;
                sourceImporter.SaveAndReimport();
                // The current callback may still hold Unity's pre-reimport 1024px
                // object. Continue on the next editor tick with the new 2048px import.
                EditorApplication.delayCall += ExtractIfAvailable;
                return;
            }

            ValidateManifest(source.width, source.height);
            Directory.CreateDirectory(OutputRoot);
            Color32[] sourcePixels = source.GetPixels32();
            bool changed = false;
            for (int index = 0; index < Manifest.Length; index++)
            {
                Crop crop = Manifest[index];
                Color32[] pixels = CopyRectangle(sourcePixels, source.width, source.height, crop);
                pixels = KeepPrimaryAlphaComponent(pixels, crop.Width, crop.Height);
                RemoveTouchingNeighborBleed(pixels, crop);
                Texture2D output = new Texture2D(crop.Width, crop.Height, TextureFormat.RGBA32, false);
                output.SetPixels32(pixels);
                output.Apply(false, false);
                byte[] png = output.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(output);
                string path = OutputRoot + "/" + crop.Name + ".png";
                if (!File.Exists(path) || !BytesEqual(File.ReadAllBytes(path), png))
                {
                    File.WriteAllBytes(path, png);
                    changed = true;
                }
            }

            if (changed) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int index = 0; index < Manifest.Length; index++)
            {
                ConfigureOutput(OutputRoot + "/" + Manifest[index].Name + ".png");
            }
        }

        private static Color32[] CopyRectangle(Color32[] source, int sourceWidth,
            int sourceHeight, Crop crop)
        {
            Color32[] result = new Color32[crop.Width * crop.Height];
            int sourceBottom = sourceHeight - crop.Top - crop.Height;
            for (int row = 0; row < crop.Height; row++)
            {
                Array.Copy(source, (sourceBottom + row) * sourceWidth + crop.X,
                    result, row * crop.Width, crop.Width);
            }
            return result;
        }

        private static void ValidateManifest(int width, int height)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Manifest.Length; index++)
            {
                Crop crop = Manifest[index];
                if (!names.Add(crop.Name)) throw new InvalidOperationException(
                    "Duplicate Nexus Skill crop name: " + crop.Name);
                if (crop.X < 0 || crop.Top < 0 || crop.Width <= 0 || crop.Height <= 0
                    || crop.X + crop.Width > width || crop.Top + crop.Height > height)
                    throw new InvalidOperationException("Nexus Skill crop is outside source bounds: " + crop.Name);
            }
        }

        private static Color32[] KeepPrimaryAlphaComponent(Color32[] pixels, int width, int height)
        {
            bool[] visited = new bool[pixels.Length];
            List<int> best = new List<int>();
            Queue<int> open = new Queue<int>();
            for (int seed = 0; seed < pixels.Length; seed++)
            {
                if (visited[seed] || pixels[seed].a <= 4) continue;
                List<int> component = new List<int>();
                visited[seed] = true;
                open.Enqueue(seed);
                while (open.Count > 0)
                {
                    int current = open.Dequeue();
                    component.Add(current);
                    int x = current % width;
                    int y = current / width;
                    Visit(current - 1, x > 0);
                    Visit(current + 1, x + 1 < width);
                    Visit(current - width, y > 0);
                    Visit(current + width, y + 1 < height);
                }
                if (component.Count > best.Count) best = component;

                void Visit(int next, bool inBounds)
                {
                    if (!inBounds || visited[next] || pixels[next].a <= 4) return;
                    visited[next] = true;
                    open.Enqueue(next);
                }
            }

            bool[] keep = new bool[pixels.Length];
            for (int index = 0; index < best.Count; index++) keep[best[index]] = true;
            for (int index = 0; index < pixels.Length; index++)
                if (!keep[index]) pixels[index] = new Color32(0, 0, 0, 0);
            return pixels;
        }

        private static void RemoveTouchingNeighborBleed(Color32[] pixels, Crop crop)
        {
            if (!string.Equals(crop.Name, "Burst", StringComparison.Ordinal)) return;

            // The lower Burst ornament and the Anchor crown physically overlap
            // in the generated atlas, so connectivity alone cannot split them.
            // Retain the original central taper and remove only the widening
            // neighbor band beneath it; no source pixels are synthesized.
            const int taperStart = 254;
            const int taperEnd = 269;
            int center = (crop.Width - 1) / 2;
            for (int yFromTop = taperStart; yFromTop < crop.Height; yFromTop++)
            {
                int halfWidth = 15 - (yFromTop - taperStart);
                int unityRow = crop.Height - 1 - yFromTop;
                for (int x = 0; x < crop.Width; x++)
                {
                    if (yFromTop >= taperEnd || Mathf.Abs(x - center) > halfWidth)
                        pixels[unityRow * crop.Width + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        private static void ConfigureOutput(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool dirty = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.filterMode != FilterMode.Bilinear || !importer.alphaIsTransparency;
            if (!dirty) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] != right[index]) return false;
            return true;
        }
    }
}
