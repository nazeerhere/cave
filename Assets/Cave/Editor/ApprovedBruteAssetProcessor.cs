#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Builds presentation-only Brute body frames from the four approved transparent sheets.</summary>
    public static class ApprovedBruteAssetProcessor
    {
        public const string SourceRoot = "Assets/Cave/Art/Enemies/Brute/Source/Approved";
        public const string RuntimeRoot = "Assets/Cave/Art/Enemies/Brute/Runtime";
        public const string RegularRuntimeRoot = RuntimeRoot + "/Regular";
        public const string CorruptRuntimeRoot = RuntimeRoot + "/Corrupt";
        public const string HookSourceRoot = "Assets/Cave/Art/Enemies/Brute/Weapons/Approved";
        public const string RegularHookPath = HookSourceRoot + "/BruteHook_Regular.png";
        public const string CorruptHookPath = HookSourceRoot + "/BruteHook_Corrupt.png";
        private const int CanvasWidth = 448;
        private const int CanvasHeight = 352;
        private const int GroundPixels = 18;

        private static readonly Sequence[] Sequences =
        {
            new Sequence("Regular/Brute_Regular_Movement.png", "Regular", "Idle", 4, 0, 4),
            new Sequence("Regular/Brute_Regular_Movement.png", "Regular", "Walk", 4, 1, 4),
            new Sequence("Regular/Brute_Regular_Movement.png", "Regular", "Block", 4, 2, 4),
            new Sequence("Regular/Brute_Regular_Movement.png", "Regular", "Light", 4, 3, 4),
            new Sequence("Regular/Brute_Regular_Combat.png", "Regular", "Heavy", 4, 1, 5),
            new Sequence("Regular/Brute_Regular_Combat.png", "Regular", "Grab", 4, 2, 5),
            new Sequence("Regular/Brute_Regular_Combat.png", "Regular", "Hurt", 4, 3, 2),
            new Sequence("Regular/Brute_Regular_Combat.png", "Regular", "Death", 4, 3, 2),
            new Sequence("Corrupt/Brute_Corrupt_Movement.png", "Corrupt", "Idle", 4, 0, 4),
            new Sequence("Corrupt/Brute_Corrupt_Movement.png", "Corrupt", "Walk", 4, 1, 4),
            new Sequence("Corrupt/Brute_Corrupt_Movement.png", "Corrupt", "Block", 4, 2, 4),
            new Sequence("Corrupt/Brute_Corrupt_Movement.png", "Corrupt", "Light", 4, 3, 4),
            new Sequence("Corrupt/Brute_Corrupt_Combat.png", "Corrupt", "Heavy", 4, 1, 5),
            new Sequence("Corrupt/Brute_Corrupt_Combat.png", "Corrupt", "Grab", 4, 2, 5),
            new Sequence("Corrupt/Brute_Corrupt_Combat.png", "Corrupt", "Hurt", 4, 3, 2),
            new Sequence("Corrupt/Brute_Corrupt_Combat.png", "Corrupt", "Death", 4, 3, 2)
        };

        [MenuItem("Tools/Cave/Enemies/Rebuild Approved Brute Runtime Assets", priority = 213)]
        public static void Build()
        {
            var textures = new Dictionary<string, Texture2D>();
            for (int i = 0; i < Sequences.Length; i++)
            {
                string path = SourceRoot + "/" + Sequences[i].RelativePath;
                if (textures.ContainsKey(path)) continue;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { Debug.LogError("[Cave] Brute build stopped; approved source missing: " + path); return; }
                importer.textureType = TextureImporterType.Default; importer.isReadable = true; importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) { Debug.LogError("[Cave] Brute source could not be read: " + path); return; }
                textures.Add(path, texture);
            }

            for (int i = 0; i < Sequences.Length; i++) BuildSequence(Sequences[i], textures[SourceRoot + "/" + Sequences[i].RelativePath]);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureRuntime(RegularRuntimeRoot); ConfigureRuntime(CorruptRuntimeRoot);
            ConfigureApprovedHookImports();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built approved Regular and Corrupt Brute body frames and configured the approved hook sprites. Gameplay was not modified.");
        }

        [MenuItem("Tools/Cave/Enemies/Import Approved Brute Hook Sprites", priority = 214)]
        public static void ConfigureApprovedHookImports()
        {
            ConfigureHookImport(RegularHookPath, "Regular");
            ConfigureHookImport(CorruptHookPath, "Corrupt");
        }

        public static string RuntimeRootFor(bool corrupt) => corrupt ? CorruptRuntimeRoot : RegularRuntimeRoot;

        private static void BuildSequence(Sequence sequence, Texture2D texture)
        {
            for (int frame = 0; frame < sequence.FrameCount; frame++)
            {
                RectInt rect = GridCell(texture.width, texture.height, sequence.FrameCount, sequence.RowCount, frame, sequence.Row);
                Color[] source = texture.GetPixels(rect.x, rect.y, rect.width, rect.height);
                BoundsInt bounds = AlphaBounds(source, rect.width, rect.height);
                if (bounds.size.x <= 0 || bounds.size.y <= 0) { Debug.LogError("[Cave] Empty Brute frame: " + sequence.Action + " " + frame); continue; }
                if (bounds.size.x > CanvasWidth || bounds.size.y + GroundPixels > CanvasHeight) { Debug.LogError("[Cave] Brute frame exceeds normalized canvas: " + sequence.Action + " " + frame); continue; }
                Color[] output = new Color[CanvasWidth * CanvasHeight]; int dx = (CanvasWidth - bounds.size.x) / 2;
                for (int y = 0; y < bounds.size.y; y++) Array.Copy(source, (bounds.position.y + y) * rect.width + bounds.position.x, output, (GroundPixels + y) * CanvasWidth + dx, bounds.size.x);
                Texture2D generated = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
                generated.SetPixels(output); generated.Apply(false, false);
                string folder = RuntimeRootFor(sequence.Form == "Corrupt"); Directory.CreateDirectory(FullPath(folder));
                File.WriteAllBytes(FullPath(folder + "/Brute_" + sequence.Form + "_" + sequence.Action + "_" + frame.ToString("00") + ".png"), generated.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(generated);
            }
        }

        private static RectInt GridCell(int width, int height, int columns, int rows, int column, int rowFromTop)
        {
            int x0 = Mathf.FloorToInt(column * width / (float)columns); int x1 = Mathf.FloorToInt((column + 1) * width / (float)columns);
            int top = Mathf.FloorToInt(rowFromTop * height / (float)rows); int bottom = Mathf.FloorToInt((rowFromTop + 1) * height / (float)rows);
            return new RectInt(x0, height - bottom, x1 - x0, bottom - top);
        }

        private static BoundsInt AlphaBounds(Color[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (pixels[y * width + x].a > .03f)
            { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            return maxX < 0 ? new BoundsInt() : new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }

        private static void ConfigureRuntime(string root)
        {
            string[] ids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            for (int i = 0; i < ids.Length; i++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(ids[i])) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 128f;
                importer.spritePivot = new Vector2(.5f, GroundPixels / (float)CanvasHeight); importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true; importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureHookImport(string path, string form)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Cave] " + form + " Brute hook import stopped; approved source is missing: " + path);
                return;
            }

            // Both supplied images are individual transparent sprites. Their lower-left
            // attachment rings are deliberately the pivots so the existing tether ends at
            // the chain connection instead of the visual centre.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.spritePivot = new Vector2(0.10f, 0.10f);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static string FullPath(string assetPath) => Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

        private readonly struct Sequence
        {
            public readonly string RelativePath, Form, Action; public readonly int RowCount, Row, FrameCount;
            public Sequence(string relativePath, string form, string action, int rowCount, int row, int frameCount)
            { RelativePath = relativePath; Form = form; Action = action; RowCount = rowCount; Row = row; FrameCount = frameCount; }
        }
    }
}
#endif
