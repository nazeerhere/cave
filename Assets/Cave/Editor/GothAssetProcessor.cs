#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Deterministically converts the approved, action-specific Anti-Pyre source
    /// sheets into transparent, ground-anchored runtime sprites. This processor
    /// deliberately does not read the retired four presentation pages or the
    /// retired composite Goth VFX atlas.
    /// </summary>
    public static class GothAssetProcessor
    {
        // The approved clean action sheets are the sole Anti-Pyre body source.
        // Keep the historical Goth tree name so existing gameplay class names and
        // prefab GUIDs remain stable, but never use it as a presentation identity.
        public const string CharacterSourceRoot = "Assets/Cave/Art/Enemies/Goth/Source/Approved";
        public const string VfxSourceRoot = "Assets/Cave/Art/VFX/Goth/Source/CleanActions";
        // These two folders are the only active Anti-Pyre body outputs. Historical
        // generations live under Goth/Legacy and must never be read by runtime
        // presentation code.
        public const string CharacterRuntimeRoot = "Assets/Cave/Art/Enemies/Goth/Runtime";
        public const string RegularRuntimeRoot = CharacterRuntimeRoot + "/Regular";
        public const string CorruptRuntimeRoot = CharacterRuntimeRoot + "/Corrupt";
        public const string VfxRuntimeRoot = "Assets/Cave/Art/VFX/Goth/Runtime/AntiPyre";
        public const string LegacyRoot = "Assets/Cave/Art/Enemies/Goth/Legacy";

        private const int BodyCanvasWidth = 384;
        private const int BodyCanvasHeight = 320;
        private const int BodyGroundPixels = 22;

        private static readonly CharacterSheet[] CharacterSheets =
        {
            new CharacterSheet("Corrected/Regular/Pyre_Regular_Movement_Casts.png", "Regular", "Idle", 4, 0, 6),
            new CharacterSheet("Corrected/Regular/Pyre_Regular_Movement_Casts.png", "Regular", "Walk", 4, 1, 6, 0, 1, 2, 4, 5, 6),
            new CharacterSheet("Corrected/Regular/Pyre_Regular_Movement_Casts.png", "Regular", "FireballCast", 4, 2, 6),
            new CharacterSheet("Corrected/Regular/Pyre_Regular_Movement_Casts.png", "Regular", "LaserCharge", 4, 3, 6),
            new CharacterSheet("Corrected/Regular/Pyre_Regular_Release_Damage.png", "Regular", "LaserRelease", 3, 0, 6),
            new CharacterSheet("Corrected/Regular/Pyre_Regular_Release_Damage.png", "Regular", "Hurt", 3, 1, 6),
            new CharacterSheet("Corrected/Regular/Pyre_Regular_Release_Damage.png", "Regular", "Death", 3, 2, 5),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Movement_Casts.png", "Corrupt", "Idle", 4, 0, 4),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Movement_Casts.png", "Corrupt", "Move", 4, 1,4),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Movement_Casts.png", "Corrupt", "FireballCastVolley", 4, 2, 5),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Movement_Casts.png", "Corrupt", "MeteorRitual", 4, 3, 5),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Major_Damage.png", "Corrupt", "AntimatterBurst", 4, 0, 5),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Major_Damage.png", "Corrupt", "FocusOrbCast", 4, 1, 5),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Major_Damage.png", "Corrupt", "AntimatterBeam", 4, 2, 5),
            new CharacterSheet("Corrected/Corrupt/Pyre_Corrupt_Major_Damage.png", "Corrupt", "Death", 4, 3, 5)
        };

        private static readonly VfxSheet[] VfxSheets =
        {
            new VfxSheet("Fireball.png", "Fireball", 6, 6, 1),
            new VfxSheet("Fireball_Sequence.png", "FireballSequence", 4, 4, 1),
            new VfxSheet("Meteor_Fireball.png", "MeteorFireball", 6, 6, 1),
            new VfxSheet("Antimatter_Burst.png", "AntimatterBurst", 7, 7, 1),
            new VfxSheet("Antimatter_Beam.png", "AntimatterBeam", 12, 4, 3),
            new VfxSheet("Focus_Orb_Ground_Travel.png", "FocusOrbTravel", 8, 8, 1),
            new VfxSheet("Focus_Orb_Impact_Charge.png", "FocusOrbCharge", 8, 8, 1),
            new VfxSheet("Meteor_Storm_Magic_Circle.png", "MeteorStormCircle", 6, 6, 1)
        };

        [MenuItem("Tools/Cave/Enemies/Rebuild Canonical Anti-Pyre Runtime Assets", priority = 212)]
        public static void Build()
        {
            if (!ValidateSources()) return;

            ArchiveSupersededRuntimeFrames();
            for (int index = 0; index < CharacterSheets.Length; index++) BuildCharacterSheet(CharacterSheets[index]);
            for (int index = 0; index < VfxSheets.Length; index++) BuildVfxSheet(VfxSheets[index]);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureRuntimeSprites(RegularRuntimeRoot, true);
            ConfigureRuntimeSprites(CorruptRuntimeRoot, true);
            ConfigureRuntimeSprites(VfxRuntimeRoot, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built Anti-Pyre runtime sprites exclusively from the clean action and VFX source packs.");
        }

        private static bool ValidateSources()
        {
            bool valid = true;
            for (int index = 0; index < CharacterSheets.Length; index++) valid &= ConfigureSourceForRead(CharacterSourceRoot + "/" + CharacterSheets[index].RelativePath);
            for (int index = 0; index < VfxSheets.Length; index++) valid &= ConfigureSourceForRead(VfxSourceRoot + "/" + VfxSheets[index].RelativePath);
            return valid;
        }

        private static bool ConfigureSourceForRead(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Cave] Anti-Pyre build stopped: clean source is missing or has not been imported: " + path);
                return false;
            }

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return true;
        }

        private static void BuildCharacterSheet(CharacterSheet sheet)
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterSourceRoot + "/" + sheet.RelativePath);
            if (source == null || !source.isReadable)
            {
                Debug.LogError("[Cave] Anti-Pyre body source is unavailable/read-protected: " + sheet.RelativePath);
                return;
            }

            var extracted = new List<FramePixels>(sheet.FrameCount);
            for (int frame = 0; frame < sheet.FrameCount; frame++)
            {
                int sourceFrame = sheet.SourceFrameIndices == null ? frame : sheet.SourceFrameIndices[frame];
                int sourceCount = sheet.SourceFrameIndices == null ? sheet.FrameCount : 8;
                RectInt rect = GridCell(source.width, source.height, sourceCount, sheet.RowCount, sourceFrame, sheet.Row);
                FramePixels pixels = Extract(source, rect, true);
                if (!ValidateAlphaRetention(sheet.RelativePath, sheet.Action, frame, rect, pixels)) continue;
                extracted.Add(pixels);
                WriteNormalized(pixels, BodyCanvasWidth, BodyCanvasHeight, BodyGroundPixels, true,
                    RuntimeRootFor(sheet.Form),
                    "AntiPyre_" + sheet.Form + "_" + sheet.Action + "_" + frame.ToString("00") + ".png");
            }
            if (sheet.Form == "Regular" && sheet.Action == "Walk") ValidateRegularWalk(extracted);
        }

        private static void ValidateRegularWalk(List<FramePixels> frames)
        {
            if (frames.Count < 2) { Debug.LogError("[Cave] Corrected Regular Pyre walk validation failed: fewer than two frames."); return; }
            int distinctPairs = 0;
            for (int index = 1; index < frames.Count; index++)
            {
                FramePixels a = frames[index - 1]; FramePixels b = frames[index];
                int width = Mathf.Min(a.Width, b.Width);
                int aHeight = a.Width > 0 ? a.Pixels.Length / a.Width : 0;
                int bHeight = b.Width > 0 ? b.Pixels.Length / b.Width : 0;
                int height = Mathf.Min(aHeight, bHeight) / 2;
                int changed = 0; int sampled = 0;
                for (int y = 0; y < height; y += 2) for (int x = 0; x < width; x += 2)
                {
                    Color left = a.Pixels[y * a.Width + x]; Color right = b.Pixels[y * b.Width + x];
                    if (Mathf.Abs(left.a - right.a) + Mathf.Abs(left.r - right.r) + Mathf.Abs(left.g - right.g) + Mathf.Abs(left.b - right.b) > .12f) changed++;
                    sampled++;
                }
                if (changed > sampled * .01f) distinctPairs++;
            }
            if (distinctPairs < frames.Count - 2) Debug.LogError("[Cave] Corrected Regular Pyre walk validation failed: lower-body frames are duplicate or near-duplicate.");
            else Debug.Log("[Cave] Corrected Regular Pyre walk validation passed: " + frames.Count + " authored frames contain distinct lower-body motion.");
        }

        private static void ArchiveSupersededRuntimeFrames()
        {
            ArchiveUnexpectedFrames(RegularRuntimeRoot, "Regular");
            ArchiveUnexpectedFrames(CorruptRuntimeRoot, "Corrupt");
        }

        private static void ArchiveUnexpectedFrames(string root, string form)
        {
            string archive = LegacyRoot + "/PreCorrectedPyreRuntime/" + form;
            EnsureAssetFolder(archive);
            string[] ids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            for (int index = 0; index < ids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(ids[index]); string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("AntiPyre_" + form + "_", StringComparison.Ordinal) || IsExpectedRuntimeName(name, form)) continue;
                string destination = archive + "/" + Path.GetFileName(path);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(destination) == null) AssetDatabase.MoveAsset(path, destination);
            }
        }

        private static bool IsExpectedRuntimeName(string name, string form)
        {
            for (int i = 0; i < CharacterSheets.Length; i++)
            {
                CharacterSheet sheet = CharacterSheets[i];
                if (sheet.Form != form) continue;
                for (int frame = 0; frame < sheet.FrameCount; frame++) if (name == "AntiPyre_" + form + "_" + sheet.Action + "_" + frame.ToString("00")) return true;
            }
            return false;
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] pieces = path.Split('/'); string current = pieces[0];
            for (int i = 1; i < pieces.Length; i++) { string next = current + "/" + pieces[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, pieces[i]); current = next; }
        }

        public static string RuntimeRootFor(string form)
        {
            return form == "Regular" ? RegularRuntimeRoot : CorruptRuntimeRoot;
        }

        private static void BuildVfxSheet(VfxSheet sheet)
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxSourceRoot + "/" + sheet.RelativePath);
            if (source == null || !source.isReadable)
            {
                Debug.LogError("[Cave] Anti-Pyre VFX source is unavailable/read-protected: " + sheet.RelativePath);
                return;
            }

            var frames = new List<FramePixels>(sheet.FrameCount);
            int canvasWidth = 1;
            int canvasHeight = 1;
            for (int frame = 0; frame < sheet.FrameCount; frame++)
            {
                RectInt rect = GridCell(source.width, source.height, sheet.Columns, sheet.Rows, frame % sheet.Columns, frame / sheet.Columns);
                FramePixels pixels = Extract(source, rect, false);
                if (!ValidateAlphaRetention(sheet.RelativePath, sheet.Action, frame, rect, pixels)) continue;
                frames.Add(pixels);
                canvasWidth = Mathf.Max(canvasWidth, pixels.Bounds.size.x);
                canvasHeight = Mathf.Max(canvasHeight, pixels.Bounds.size.y);
            }

            canvasWidth += 12;
            canvasHeight += 12;
            bool beam = sheet.Action == "AntimatterBeam";
            for (int frame = 0; frame < frames.Count; frame++)
            {
                WriteNormalized(frames[frame], canvasWidth, canvasHeight, 6, !beam,
                    VfxRuntimeRoot + "/" + sheet.Action,
                    "AntiPyre_Vfx_" + sheet.Action + "_" + frame.ToString("00") + ".png");
            }
        }

        private static RectInt HorizontalCell(int width, int height, int count, int index)
        {
            int xMin = Mathf.FloorToInt(index * width / (float)count);
            int xMax = Mathf.FloorToInt((index + 1) * width / (float)count);
            return new RectInt(xMin, 0, Mathf.Max(1, xMax - xMin), height);
        }

        private static RectInt GridCell(int width, int height, int columns, int rows, int column, int rowFromTop)
        {
            int xMin = Mathf.FloorToInt(column * width / (float)columns);
            int xMax = Mathf.FloorToInt((column + 1) * width / (float)columns);
            int top = Mathf.FloorToInt(rowFromTop * height / (float)rows);
            int bottom = Mathf.FloorToInt((rowFromTop + 1) * height / (float)rows);
            return new RectInt(xMin, height - bottom, Mathf.Max(1, xMax - xMin), Mathf.Max(1, bottom - top));
        }

        private static FramePixels Extract(Texture2D source, RectInt rect, bool characterBody)
        {
            Color[] pixels = source.GetPixels(rect.x, rect.y, rect.width, rect.height);
            int sourceVisiblePixels = CountVisiblePixels(pixels);
            bool preservesSourceAlpha = HasMeaningfulTransparency(pixels);
            if (!preservesSourceAlpha)
            {
                // Clean action and VFX sources that already carry alpha must pass
                // through unchanged. Only a fully opaque, baked-matte source uses
                // this source-specific fallback.
                RemoveConnectedMatte(pixels, rect.width, rect.height);
                if (characterBody) HardenInteriorCharacterPixels(pixels, rect.width, rect.height);
            }
            int outputVisiblePixels = CountVisiblePixels(pixels);
            return new FramePixels(pixels, rect.width, rect.height, AlphaBounds(pixels, rect.width, rect.height),
                sourceVisiblePixels, outputVisiblePixels, preservesSourceAlpha);
        }

        private static bool ValidateAlphaRetention(string source, string sequence, int frame, RectInt rect, FramePixels pixels)
        {
            if (!pixels.PreservesSourceAlpha || pixels.SourceVisiblePixels == 0) return true;
            if (pixels.OutputVisiblePixels == pixels.SourceVisiblePixels) return true;

            float retained = 100f * pixels.OutputVisiblePixels / pixels.SourceVisiblePixels;
            Debug.LogError("[Cave] Anti-Pyre alpha validation failed | source='" + source + "' sequence='" + sequence +
                "' frame=" + frame + " unityRect=(x:" + rect.x + ", y:" + rect.y + ", width:" + rect.width +
                ", height:" + rect.height + ") sourceVisible=" + pixels.SourceVisiblePixels + " outputVisible=" +
                pixels.OutputVisiblePixels + " retained=" + retained.ToString("F1") + "%.");
            return false;
        }

        private static bool HasMeaningfulTransparency(Color[] pixels)
        {
            for (int index = 0; index < pixels.Length; index++) if (pixels[index].a < .995f) return true;
            return false;
        }

        private static int CountVisiblePixels(Color[] pixels)
        {
            int count = 0;
            for (int index = 0; index < pixels.Length; index++) if (pixels[index].a > .03f) count++;
            return count;
        }

        private static void WriteNormalized(FramePixels source, int canvasWidth, int canvasHeight, int groundPixels, bool centerHorizontally, string folder, string filename)
        {
            if (source.Bounds.size.x <= 0 || source.Bounds.size.y <= 0)
            {
                Debug.LogWarning("[Cave] Skipped empty Anti-Pyre runtime frame: " + filename);
                return;
            }

            Color[] outputPixels = new Color[canvasWidth * canvasHeight];
            int destinationX = centerHorizontally ? Mathf.Max(0, (canvasWidth - source.Bounds.size.x) / 2) : 6;
            int destinationY = Mathf.Max(0, groundPixels);
            int copyWidth = Mathf.Min(source.Bounds.size.x, canvasWidth - destinationX);
            int copyHeight = Mathf.Min(source.Bounds.size.y, canvasHeight - destinationY);
            if (copyWidth != source.Bounds.size.x || copyHeight != source.Bounds.size.y)
            {
                Debug.LogError("[Cave] Anti-Pyre runtime frame exceeds its fixed canvas and was not saved: " + filename);
                return;
            }
            for (int y = 0; y < copyHeight; y++)
            {
                Array.Copy(source.Pixels, (source.Bounds.position.y + y) * source.Width + source.Bounds.position.x,
                    outputPixels, (destinationY + y) * canvasWidth + destinationX, copyWidth);
            }

            if (source.PreservesSourceAlpha && CountVisiblePixels(outputPixels) != source.SourceVisiblePixels)
            {
                Debug.LogError("[Cave] Anti-Pyre alpha validation failed after padding; frame was not saved: " + filename);
                return;
            }

            Texture2D output = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
            output.SetPixels(outputPixels);
            output.Apply(false, false);
            string assetPath = folder + "/" + filename;
            string fullPath = AssetPathToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, ImageConversion.EncodeToPNG(output));
            UnityEngine.Object.DestroyImmediate(output);
        }

        private static string AssetPathToFullPath(string assetPath) => Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

        // The clean source sheets are presentation cells, not alpha-authored runtime
        // frames. Their board matte can be black, neutral, or slightly tinted. Build
        // a palette from each cell's outer border and remove only pixels connected to
        // that border which are sufficiently close to that local palette. This is
        // deliberately not a "black equals transparent" rule: dark clothing remains
        // because it is not edge-connected to the cell matte.
        private static void RemoveConnectedMatte(Color[] pixels, int width, int height)
        {
            List<Color> edgePalette = BuildEdgePalette(pixels, width, height);
            bool[] visited = new bool[pixels.Length];
            var queue = new Queue<int>();
            for (int x = 0; x < width; x++) { queue.Enqueue(x); queue.Enqueue((height - 1) * width + x); }
            for (int y = 1; y < height - 1; y++) { queue.Enqueue(y * width); queue.Enqueue(y * width + width - 1); }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (index < 0 || index >= pixels.Length || visited[index] || !IsConnectedMatte(pixels[index], edgePalette)) continue;
                visited[index] = true;
                pixels[index].a = 0f;
                int x = index % width;
                if (x > 0) queue.Enqueue(index - 1);
                if (x < width - 1) queue.Enqueue(index + 1);
                if (index >= width) queue.Enqueue(index - width);
                if (index < pixels.Length - width) queue.Enqueue(index + width);
            }
        }

        private static List<Color> BuildEdgePalette(Color[] pixels, int width, int height)
        {
            // A compact palette is enough for the board gradient and avoids turning
            // this editor-only flood fill into an edge-pixel-times-frame-pixel scan.
            var palette = new List<Color>(32);
            for (int x = 0; x < width; x++)
            {
                AddEdgePaletteSample(palette, pixels[x]);
                if (height > 1) AddEdgePaletteSample(palette, pixels[(height - 1) * width + x]);
            }
            for (int y = 1; y < height - 1; y++)
            {
                AddEdgePaletteSample(palette, pixels[y * width]);
                if (width > 1) AddEdgePaletteSample(palette, pixels[y * width + width - 1]);
            }
            return palette;
        }

        private static void AddEdgePaletteSample(List<Color> palette, Color candidate)
        {
            if (palette.Count >= 32) return;
            for (int index = 0; index < palette.Count; index++)
            {
                Color existing = palette[index];
                float red = candidate.r - existing.r;
                float green = candidate.g - existing.g;
                float blue = candidate.b - existing.b;
                if (red * red + green * green + blue * blue <= .0025f) return;
            }
            palette.Add(candidate);
        }

        private static bool IsConnectedMatte(Color value, List<Color> edgePalette)
        {
            if (value.a < .03f) return true;
            for (int index = 0; index < edgePalette.Count; index++)
            {
                Color edge = edgePalette[index];
                if (edge.a < .03f) continue;
                float red = value.r - edge.r;
                float green = value.g - edge.g;
                float blue = value.b - edge.b;
                if (red * red + green * green + blue * blue <= .0121f) return true;
            }

            // Covers a very dark board gradient even when it has no exactly matching
            // outer-edge sample. Connectivity still prevents dark body art removal.
            float maximum = Mathf.Max(value.r, Mathf.Max(value.g, value.b));
            float minimum = Mathf.Min(value.r, Mathf.Min(value.g, value.b));
            return maximum < .16f && maximum - minimum < .10f;
        }

        // Character sheets occasionally encode a solid body as a low-alpha interior.
        // Harden only partially transparent pixels enclosed on all four sides by other
        // visible pixels. Outer anti-aliased/glow edges remain untouched, and VFX
        // sheets never call this method.
        private static void HardenInteriorCharacterPixels(Color[] pixels, int width, int height)
        {
            Color[] source = (Color[])pixels.Clone();
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    Color value = source[index];
                    if (value.a <= .03f || value.a >= .995f) continue;
                    if (source[index - 1].a <= .03f || source[index + 1].a <= .03f ||
                        source[index - width].a <= .03f || source[index + width].a <= .03f) continue;
                    value.a = 1f;
                    pixels[index] = value;
                }
            }
        }

        private static BoundsInt AlphaBounds(Color[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (pixels[y * width + x].a > .03f)
            { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            return maxX < minX ? new BoundsInt() : new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }

        private static void ConfigureRuntimeSprites(string root, bool bodies)
        {
            string[] ids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            for (int index = 0; index < ids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(ids[index]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128f;
                importer.spritePivot = PivotFor(path, bodies);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static Vector2 PivotFor(string path, bool bodies)
        {
            if (bodies) return new Vector2(.5f, BodyGroundPixels / (float)BodyCanvasHeight);
            if (path.IndexOf("AntimatterBeam", StringComparison.Ordinal) >= 0) return new Vector2(.05f, .5f);
            if (path.IndexOf("FocusOrb", StringComparison.Ordinal) >= 0 || path.IndexOf("MeteorStormCircle", StringComparison.Ordinal) >= 0) return new Vector2(.5f, .08f);
            if (path.IndexOf("Fireball", StringComparison.Ordinal) >= 0 || path.IndexOf("MeteorFireball", StringComparison.Ordinal) >= 0) return new Vector2(.62f, .5f);
            return new Vector2(.5f, .5f);
        }

        private readonly struct CharacterSheet
        {
            public readonly string RelativePath; public readonly string Form; public readonly string Action; public readonly int RowCount; public readonly int Row; public readonly int FrameCount; public readonly int[] SourceFrameIndices;
            public CharacterSheet(string relativePath, string form, string action, int rowCount, int row, int frameCount, params int[] sourceFrameIndices)
            { RelativePath = relativePath; Form = form; Action = action; RowCount = rowCount; Row = row; FrameCount = frameCount; SourceFrameIndices = sourceFrameIndices != null && sourceFrameIndices.Length > 0 ? sourceFrameIndices : null; }
        }

        private readonly struct VfxSheet
        {
            public readonly string RelativePath; public readonly string Action; public readonly int FrameCount; public readonly int Columns; public readonly int Rows;
            public VfxSheet(string relativePath, string action, int frameCount, int columns, int rows) { RelativePath = relativePath; Action = action; FrameCount = frameCount; Columns = columns; Rows = rows; }
        }

        private sealed class FramePixels
        {
            public readonly Color[] Pixels; public readonly int Width; public readonly BoundsInt Bounds;
            public readonly int SourceVisiblePixels; public readonly int OutputVisiblePixels; public readonly bool PreservesSourceAlpha;
            public FramePixels(Color[] pixels, int width, int height, BoundsInt bounds, int sourceVisiblePixels, int outputVisiblePixels, bool preservesSourceAlpha)
            {
                Pixels = pixels; Width = width; Bounds = bounds; SourceVisiblePixels = sourceVisiblePixels;
                OutputVisiblePixels = outputVisiblePixels; PreservesSourceAlpha = preservesSourceAlpha;
            }
        }
    }
}
#endif
