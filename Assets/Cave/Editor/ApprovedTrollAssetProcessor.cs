#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Builds normalized presentation-only Troll sprites from the approved sheets.</summary>
    public static class ApprovedTrollAssetProcessor
    {
        public const string SourceRoot = "Assets/Cave/Art/Enemies/Troll/Source/Approved";
        public const string RuntimeRoot = "Assets/Cave/Art/Enemies/Troll/Runtime";
        public const string RegularRuntimeRoot = RuntimeRoot + "/Regular";
        public const string CorruptRuntimeRoot = RuntimeRoot + "/Corrupt";
        public const string WeaponRuntimeRoot = RuntimeRoot + "/Weapons";
        private const int CanvasWidth = 448;
        private const int CanvasHeight = 384;
        private const int GroundPixels = 16;
        private static bool automaticBuildQueued;

        [InitializeOnLoadMethod]
        private static void QueueFirstApprovedBuild()
        {
            if (automaticBuildQueued) return;
            automaticBuildQueued = true;
            EditorApplication.delayCall += BuildIfRuntimeMissing;
        }

        private static void BuildIfRuntimeMissing()
        {
            automaticBuildQueued = false;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot + "/Regular/Troll_Regular_Movement.png") == null
                || AssetDatabase.LoadAssetAtPath<Sprite>(RegularRuntimeRoot + "/Troll_Regular_Idle_00.png") != null)
            {
                return;
            }
            ApprovedEnemyPresentationInstaller.RebuildAndInstallTrollVisuals();
        }

        private static readonly Sequence[] Sequences =
        {
            new Sequence("Regular/Troll_Regular_Movement.png", "Regular", "Idle", 6, 4, 0, 6),
            new Sequence("Regular/Troll_Regular_Movement.png", "Regular", "Walk", 6, 4, 1, 6),
            new Sequence("Regular/Troll_Regular_Movement.png", "Regular", "Hurt", 6, 4, 2, 5),
            new Sequence("Regular/Troll_Regular_Movement.png", "Regular", "Block", 6, 4, 3, 6),
            new Sequence("Regular/Troll_Regular_Combat.png", "Regular", "Heavy", 7, 7, 1, 5),
            new Sequence("Regular/Troll_Regular_Combat.png", "Regular", "Light", 7, 7, 2, 7),
            new Sequence("Regular/Troll_Regular_Combat.png", "Regular", "Death", 7, 7, 4, 7),
            new Sequence("Corrupt/Troll_Corrupt_Movement.png", "Corrupt", "Idle", 6, 6, 0, 6),
            new Sequence("Corrupt/Troll_Corrupt_Movement.png", "Corrupt", "Heavy", 6, 6, 1, 5),
            new Sequence("Corrupt/Troll_Corrupt_Movement.png", "Corrupt", "Light", 6, 6, 2, 6),
            new Sequence("Corrupt/Troll_Corrupt_Movement.png", "Corrupt", "Block", 6, 6, 3, 6),
            new Sequence("Corrupt/Troll_Corrupt_Movement.png", "Corrupt", "Hurt", 6, 6, 4, 5),
            new Sequence("Corrupt/Troll_Corrupt_Movement.png", "Corrupt", "Death", 6, 6, 5, 6),
            new Sequence("Corrupt/Troll_Corrupt_Combat.png", "Corrupt", "Walk", 6, 7, 1, 6),
            new Sequence("Corrupt/Troll_Corrupt_Combat.png", "Corrupt", "Run", 6, 7, 2, 6)
        };

        [MenuItem("Tools/Cave/Enemies/Rebuild Approved Troll Runtime Assets", priority = 214)]
        public static void Build()
        {
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
            for (int index = 0; index < Sequences.Length; index++)
            {
                string path = SourceRoot + "/" + Sequences[index].RelativePath;
                if (textures.ContainsKey(path)) continue;
                Texture2D texture = PrepareSource(path);
                if (texture == null) return;
                textures.Add(path, texture);
            }

            for (int index = 0; index < Sequences.Length; index++)
            {
                Sequence sequence = Sequences[index];
                BuildSequence(sequence, textures[SourceRoot + "/" + sequence.RelativePath]);
            }

            Texture2D weapons = PrepareSource(SourceRoot + "/TrollBruteWeaponsApproved.png", true);
            if (weapons == null) return;
            if (!BuildWeapon(weapons, new RectInt(0, 0, 627, 620), "Troll_Regular_Club")
                || !BuildWeapon(weapons, new RectInt(627, 0, 627, 620), "Troll_Corrupt_Club")
                || !BuildWeapon(weapons, new RectInt(0, 620, 627, 634), "Brute_Regular_Pickaxe")
                || !BuildWeapon(weapons, new RectInt(627, 620, 627, 634), "Brute_Corrupt_Pickaxe"))
            {
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureRuntime(RegularRuntimeRoot, 128f, GroundPixels / (float)CanvasHeight);
            ConfigureRuntime(CorruptRuntimeRoot, 128f, GroundPixels / (float)CanvasHeight);
            ConfigureRuntime(WeaponRuntimeRoot, 512f, 0.5f);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built approved Regular/Corrupt Troll bodies and approved Troll/Brute weapons.");
        }

        public static string RuntimeRootFor(bool corrupt) => corrupt ? CorruptRuntimeRoot : RegularRuntimeRoot;

        private static Texture2D PrepareSource(string path, bool preserveSourceDimensions = false)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogError("[Cave] Troll build stopped; source missing: " + path); return null; }
            importer.textureType = TextureImporterType.Default;
            if (preserveSourceDimensions) importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BuildSequence(Sequence sequence, Texture2D texture)
        {
            for (int frame = 0; frame < sequence.FrameCount; frame++)
            {
                RectInt rect = GridCell(texture.width, texture.height, sequence.Columns, sequence.Rows, frame, sequence.Row);
                WriteNormalized(texture.GetPixels(rect.x, rect.y, rect.width, rect.height), rect.width, rect.height,
                    RuntimeRootFor(sequence.Form == "Corrupt") + "/Troll_" + sequence.Form + "_" + sequence.Action + "_" + frame.ToString("00"),
                    CanvasWidth, CanvasHeight, GroundPixels);
            }
        }

        private static bool BuildWeapon(Texture2D texture, RectInt topLeft, string name)
        {
            int y = texture.height - topLeft.y - topLeft.height;
            RectInt unityRect = new RectInt(topLeft.x, y, topLeft.width, topLeft.height);
            if (!IsValidCrop(texture, unityRect, name)) return false;
            Color[] pixels = texture.GetPixels(unityRect.x, unityRect.y, unityRect.width, unityRect.height);
            WriteNormalized(pixels, topLeft.width, topLeft.height, WeaponRuntimeRoot + "/" + name, 640, 640, 24);
            return true;
        }

        private static bool IsValidCrop(Texture2D texture, RectInt rect, string cropName)
        {
            bool valid = rect.x >= 0 && rect.y >= 0 && rect.width > 0 && rect.height > 0
                && rect.x + rect.width <= texture.width && rect.y + rect.height <= texture.height;
            if (valid) return true;

            Debug.LogError("[Cave] Weapon crop is outside source bounds. Source=" + texture.name
                + " dimensions=" + texture.width + "x" + texture.height
                + " crop=" + cropName
                + " x=" + rect.x + " y=" + rect.y
                + " width=" + rect.width + " height=" + rect.height);
            return false;
        }

        private static void WriteNormalized(Color[] source, int width, int height, string assetPath, int canvasWidth, int canvasHeight, int ground)
        {
            BoundsInt bounds = AlphaBounds(source, width, height);
            if (bounds.size.x <= 0 || bounds.size.y <= 0) { Debug.LogError("[Cave] Empty approved frame: " + assetPath); return; }
            if (bounds.size.x > canvasWidth || bounds.size.y + ground > canvasHeight)
            {
                float scale = Mathf.Min(canvasWidth / (float)bounds.size.x, (canvasHeight - ground) / (float)bounds.size.y);
                Debug.LogError("[Cave] Approved frame exceeds normalized canvas (scale " + scale.ToString("0.00") + "): " + assetPath);
                return;
            }
            Color[] output = new Color[canvasWidth * canvasHeight];
            int destinationX = (canvasWidth - bounds.size.x) / 2;
            for (int row = 0; row < bounds.size.y; row++)
            {
                Array.Copy(source, (bounds.position.y + row) * width + bounds.position.x,
                    output, (ground + row) * canvasWidth + destinationX, bounds.size.x);
            }
            Texture2D generated = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
            generated.SetPixels(output); generated.Apply(false, false);
            Directory.CreateDirectory(FullPath(Path.GetDirectoryName(assetPath)));
            File.WriteAllBytes(FullPath(assetPath + ".png"), generated.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(generated);
        }

        private static RectInt GridCell(int width, int height, int columns, int rows, int column, int rowFromTop)
        {
            int x0 = Mathf.FloorToInt(column * width / (float)columns);
            int x1 = Mathf.FloorToInt((column + 1) * width / (float)columns);
            int top = Mathf.FloorToInt(rowFromTop * height / (float)rows);
            int bottom = Mathf.FloorToInt((rowFromTop + 1) * height / (float)rows);
            return new RectInt(x0, height - bottom, x1 - x0, bottom - top);
        }

        private static BoundsInt AlphaBounds(Color[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (pixels[y * width + x].a > .03f)
            { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            return maxX < 0 ? new BoundsInt() : new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }

        private static void ConfigureRuntime(string root, float ppu, float pivotY)
        {
            string[] ids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            for (int index = 0; index < ids.Length; index++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(ids[index])) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.spritePivot = new Vector2(.5f, pivotY);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static string FullPath(string assetPath) => Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

        private readonly struct Sequence
        {
            public readonly string RelativePath, Form, Action;
            public readonly int Columns, Rows, Row, FrameCount;
            public Sequence(string path, string form, string action, int columns, int rows, int row, int frames)
            { RelativePath = path; Form = form; Action = action; Columns = columns; Rows = rows; Row = row; FrameCount = frames; }
        }
    }
}
#endif
