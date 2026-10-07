#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cave.UI;
using UnityEditor;
using UnityEngine;

namespace Cave.EditorTools
{
    /// <summary>Deterministic, idempotent extraction of the approved Domain UI
    /// atlases. Background removal is intentionally conservative: only dark
    /// pixels connected to a crop edge are made transparent.</summary>
    public static class DomainUiAtlasExtractor
    {
        private const string Root = "Assets/Cave/Art/UI/Domain";
        private const string FrameSource = Root + "/Source/gothic_gold_ui_asset_atlas.png";
        private const string IconSource = Root + "/Source/ornate_gold_fantasy_ui_icon_atlas.png";
        private static bool initialExtractionQueued;

        private struct Slice
        {
            public Slice(string path, int x, int top, int width, int height, Vector4 border)
            { Path = path; X = x; Top = top; Width = width; Height = height; Border = border; }
            public readonly string Path; public readonly int X; public readonly int Top; public readonly int Width; public readonly int Height; public readonly Vector4 Border;
        }

        [MenuItem("Tools/Cave/UI/Domain/Extract Approved Gothic Domain Atlases")]
        public static void Extract()
        {
            Texture2D frames = Load(FrameSource);
            Texture2D icons = Load(IconSource);
            if (frames == null || icons == null)
            {
                Debug.LogError("[Cave] Domain atlas extraction requires both approved source PNGs under " + Root + "/Source.");
                return;
            }

            Extract(frames, FrameSlices());
            Extract(icons, IconSlices());
            AssetDatabase.Refresh();
            ConfigureImporters(FrameSlices());
            ConfigureImporters(IconSlices());
            DomainUiSkinBuilder.BuildOrUpdate();
            Debug.Log("[Cave] Domain UI atlas extraction complete: " + (FrameSlices().Length + IconSlices().Length) + " reusable sprites.");
        }

        [InitializeOnLoadMethod]
        private static void QueueInitialExtraction()
        {
            if (initialExtractionQueued) return;
            initialExtractionQueued = true;
            EditorApplication.delayCall += EnsureInitialSkin;
        }

        // The checked-in source art is sufficient to restore generated assets
        // in a fresh checkout. Later runs are explicit menu actions and are
        // idempotent, so this never overwrites an already-built skin.
        private static void EnsureInitialSkin()
        {
            initialExtractionQueued = false;
            if (!File.Exists(FrameSource) || !File.Exists(IconSource)) return;
            if (AssetDatabase.LoadAssetAtPath<DomainUiSkin>("Assets/Cave/Resources/UI/Domain/DomainUiSkin.asset") != null) return;
            Extract();
        }

        internal static string[] GetExpectedOutputPaths()
        {
            Slice[] frames = FrameSlices();
            Slice[] icons = IconSlices();
            string[] paths = new string[frames.Length + icons.Length];
            for (int index = 0; index < frames.Length; index++) paths[index] = frames[index].Path;
            for (int index = 0; index < icons.Length; index++) paths[frames.Length + index] = icons[index].Path;
            return paths;
        }

        private static Texture2D Load(string path)
        {
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return texture.LoadImage(bytes, false) ? texture : null;
        }

        private static void Extract(Texture2D source, Slice[] slices)
        {
            if (source == null || !source.isReadable)
                throw new InvalidOperationException("Domain atlas source must be readable before cropping.");
            Color32[] sourcePixels = source.GetPixels32();
            for (int index = 0; index < slices.Length; index++)
            {
                Slice slice = slices[index];
                string directory = System.IO.Path.GetDirectoryName(slice.Path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                Texture2D crop = Crop(source, sourcePixels, slice);
                File.WriteAllBytes(slice.Path, crop.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(crop);
            }
        }

        private static Texture2D Crop(Texture2D source, Color32[] sourcePixels, Slice slice)
        {
            int sourceY = source.height - slice.Top - slice.Height;
            if (slice.X < 0 || sourceY < 0 || slice.Width <= 0 || slice.Height <= 0
                || slice.X + slice.Width > source.width || sourceY + slice.Height > source.height)
                throw new ArgumentOutOfRangeException(nameof(slice), "Domain atlas crop is outside the source texture bounds.");

            // Unity 2021 exposes GetPixels32() only for the whole texture.
            // Keep the existing top-origin manifest conversion above, then copy
            // the same bottom-origin rectangle into the crop buffer row by row.
            Color32[] pixels = new Color32[slice.Width * slice.Height];
            for (int y = 0; y < slice.Height; y++)
            {
                Array.Copy(sourcePixels, (sourceY + y) * source.width + slice.X,
                    pixels, y * slice.Width, slice.Width);
            }
            // GetPixels is bottom-origin; the connected-edge property is origin independent.
            bool[] removed = new bool[pixels.Length];
            Queue<int> pending = new Queue<int>();
            for (int x = 0; x < slice.Width; x++) { TrySeed(x, 0); TrySeed(x, slice.Height - 1); }
            for (int y = 1; y < slice.Height - 1; y++) { TrySeed(0, y); TrySeed(slice.Width - 1, y); }
            while (pending.Count > 0)
            {
                int current = pending.Dequeue(); int x = current % slice.Width; int y = current / slice.Width;
                TrySeed(x - 1, y); TrySeed(x + 1, y); TrySeed(x, y - 1); TrySeed(x, y + 1);
            }
            for (int index = 0; index < pixels.Length; index++) if (removed[index]) pixels[index].a = 0;
            Texture2D crop = new Texture2D(slice.Width, slice.Height, TextureFormat.RGBA32, false);
            crop.SetPixels32(pixels); crop.Apply(false, false); return crop;

            void TrySeed(int x, int y)
            {
                if (x < 0 || y < 0 || x >= slice.Width || y >= slice.Height) return;
                int item = y * slice.Width + x;
                if (removed[item] || !IsExternalBlack(pixels[item])) return;
                removed[item] = true; pending.Enqueue(item);
            }
        }

        private static bool IsExternalBlack(Color32 value)
        {
            return value.a > 0 && value.r <= 24 && value.g <= 24 && value.b <= 24
                && Mathf.Abs(value.r - value.g) <= 8 && Mathf.Abs(value.g - value.b) <= 8;
        }

        private static void ConfigureImporters(Slice[] slices)
        {
            for (int index = 0; index < slices.Length; index++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(slices[index].Path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = slices[index].Border;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static Slice[] FrameSlices()
        {
            return new[]
            {
                new Slice(Root+"/Frames/LargePanel.png", 16,48,494,312,new Vector4(34,28,34,28)),
                new Slice(Root+"/Frames/MediumPanel.png", 518,56,284,304,new Vector4(28,28,28,28)),
                new Slice(Root+"/Frames/SmallPanel.png", 812,154,272,214,new Vector4(24,24,24,24)),
                new Slice(Root+"/Frames/HeaderFrame.png", 1095,112,340,78,new Vector4(24,20,24,20)),
                new Slice(Root+"/Frames/NavigationFrame.png", 1095,200,340,75,new Vector4(22,20,22,20)),
                new Slice(Root+"/Frames/SelectedNavigationFrame.png", 470,380,232,72,new Vector4(18,18,18,18)),
                new Slice(Root+"/Frames/ButtonFrame.png", 18,380,222,72,new Vector4(16,16,16,16)),
                new Slice(Root+"/Frames/SelectedButtonFrame.png", 470,380,232,72,new Vector4(18,18,18,18)),
                new Slice(Root+"/Frames/CardFrame.png", 510,558,218,226,new Vector4(22,22,22,22)),
                new Slice(Root+"/Frames/SelectedCardFrame.png", 907,560,96,225,new Vector4(14,14,14,14)),
                new Slice(Root+"/Frames/DisabledCardFrame.png", 1102,558,94,226,new Vector4(14,14,14,14)),
                new Slice(Root+"/Frames/ActionButtonFrame.png", 18,942,550,106,new Vector4(28,24,28,24)),
                new Slice(Root+"/Frames/SmallButtonFrame.png", 604,794,366,76,new Vector4(20,18,20,18)),
                new Slice(Root+"/Bars/SegmentedProgressBar.png", 728,470,336,74,new Vector4(22,16,22,16)),
                new Slice(Root+"/Bars/ProgressBar.png", 386,470,328,74,new Vector4(22,16,22,16)),
                new Slice(Root+"/Bars/ReserveBar.png", 1074,470,352,74,new Vector4(22,16,22,16)),
                new Slice(Root+"/Decorations/DividerLong.png", 998,792,428,30,new Vector4(18,0,18,0)),
                new Slice(Root+"/Decorations/DividerShort.png", 584,948,480,30,new Vector4(18,0,18,0)),
                new Slice(Root+"/Decorations/CornerOrnament.png", 1100,892,112,122,Vector4.zero)
            };
        }

        private static Slice[] IconSlices()
        {
            return new[]
            {
                new Slice(Root+"/Icons/Combat/Sword.png", 20,28,100,108,Vector4.zero),
                new Slice(Root+"/Icons/Combat/Shield.png", 128,28,100,108,Vector4.zero),
                new Slice(Root+"/Icons/States/Heart.png", 344,28,100,108,Vector4.zero),
                new Slice(Root+"/Icons/States/Heal.png", 452,28,100,108,Vector4.zero),
                new Slice(Root+"/Icons/Decorative/Star.png", 560,28,100,108,Vector4.zero),
                new Slice(Root+"/Icons/Decorative/Book.png", 668,28,100,108,Vector4.zero),
                new Slice(Root+"/Icons/States/Reserve.png", 1080,470,110,112,Vector4.zero),
                new Slice(Root+"/Icons/Elements/Fire.png", 1120,350,104,110,Vector4.zero),
                new Slice(Root+"/Icons/Elements/Snow.png", 1230,350,104,110,Vector4.zero),
                new Slice(Root+"/Icons/Elements/Lightning.png", 1340,350,104,110,Vector4.zero),
                new Slice(Root+"/Icons/Navigation/Check.png", 880,240,104,110,Vector4.zero),
                new Slice(Root+"/Icons/Navigation/Cross.png", 990,240,104,110,Vector4.zero),
                new Slice(Root+"/Icons/Navigation/ArrowLeft.png", 20,728,76,72,Vector4.zero),
                new Slice(Root+"/Icons/Navigation/ArrowRight.png", 112,728,76,72,Vector4.zero),
                new Slice(Root+"/Icons/Decorative/Diamond.png", 1090,728,72,72,Vector4.zero)
            };
        }
    }
}
#endif
