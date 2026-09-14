#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// The single approved Detective art path. It converts only the four supplied
    /// Detective sheets into grounded runtime frames and binds visual-only prefabs.
    /// It never creates a collider, AI component, projectile, or gameplay clone.
    /// </summary>
    public static class DetectiveApprovedVisualProcessor
    {
        public const string SourceRoot = "Assets/Cave/Art/Enemies/Detective/Source/Approved";
        public const string RuntimeRoot = "Assets/Cave/Art/Enemies/Detective/Runtime";
        public const string RegularRuntimeRoot = RuntimeRoot + "/Regular";
        public const string CorruptRuntimeRoot = RuntimeRoot + "/Corrupt";
        public const string RegularVisualPrefabPath = "Assets/Cave/Prefabs/Enemies/Detective/DetectiveRegularVisual.prefab";
        public const string CorruptVisualPrefabPath = "Assets/Cave/Prefabs/Enemies/Detective/DetectiveCorruptVisual.prefab";
        public const string DetectivePrefabPath = "Assets/Cave/Prefabs/Mobs/Range/Detective.prefab";
        public const string CorruptDetectivePrefabPath = "Assets/Cave/Prefabs/Mobs/Range/Corrupt Detective.prefab";

        private const int CanvasWidth = 384;
        private const int CanvasHeight = 320;
        private const int GroundPixels = 22;

        // All crop coordinates below are measured from the source image's top-left.
        // They are source-specific on purpose: the supplied sheets have different
        // dimensions and layouts, so coordinates may never be shared between them.
        private static readonly Sheet[] Sheets =
        {
            // The newest approved regular replacement is the complete source for the
            // clips that the current Detective presentation actually wires.
            new Sheet("detective_regular_latest_approved.png", "Regular", 2048, 1024, new[]
            {
                LatestRegularSequence("Idle", FrameCrop(20, 18, 108, 132), FrameCrop(140, 18, 108, 132), FrameCrop(260, 18, 108, 132),
                    FrameCrop(380, 18, 108, 132), FrameCrop(500, 18, 108, 132), FrameCrop(620, 18, 108, 132)),
                LatestRegularSequence("Walk", FrameCrop(16, 144, 112, 146), FrameCrop(136, 144, 112, 146), FrameCrop(256, 144, 112, 146),
                    FrameCrop(376, 144, 112, 146), FrameCrop(496, 144, 112, 146), FrameCrop(616, 144, 112, 146),
                    FrameCrop(736, 144, 112, 146), FrameCrop(856, 144, 112, 146), FrameCrop(976, 144, 112, 146),
                    FrameCrop(1096, 144, 112, 146), FrameCrop(1216, 144, 112, 146), FrameCrop(1336, 144, 112, 146),
                    FrameCrop(1456, 144, 80, 146)),
                LatestRegularSequence("Shoot", FrameCrop(18, 286, 184, 132), FrameCrop(208, 286, 204, 132), FrameCrop(416, 286, 204, 132),
                    FrameCrop(624, 286, 196, 132), FrameCrop(936, 286, 184, 132), FrameCrop(1128, 286, 190, 132)),
                LatestRegularSequence("Sweep", FrameCrop(18, 414, 186, 124), FrameCrop(210, 414, 226, 124), FrameCrop(442, 414, 260, 124),
                    FrameCrop(708, 414, 214, 124), FrameCrop(928, 414, 214, 124), FrameCrop(1148, 414, 196, 124)),
                LatestRegularSequence("Smoke", FrameCrop(18, 536, 118, 126), FrameCrop(138, 536, 118, 126), FrameCrop(258, 536, 118, 126),
                    FrameCrop(378, 536, 118, 126), FrameCrop(498, 536, 118, 126), FrameCrop(618, 536, 118, 126)),
                LatestRegularSequence("Study", FrameCrop(18, 650, 112, 136), FrameCrop(138, 650, 112, 136), FrameCrop(258, 650, 112, 136),
                    FrameCrop(378, 650, 112, 136), FrameCrop(498, 650, 112, 136), FrameCrop(618, 650, 112, 136),
                    FrameCrop(738, 650, 112, 136), FrameCrop(858, 650, 112, 136), FrameCrop(978, 650, 112, 136),
                    FrameCrop(1098, 650, 112, 136), FrameCrop(1218, 650, 112, 136), FrameCrop(1338, 650, 112, 136)),
                LatestRegularSequence("Command", FrameCrop(18, 784, 118, 122), FrameCrop(138, 784, 118, 122), FrameCrop(258, 784, 118, 122),
                    FrameCrop(378, 784, 118, 122), FrameCrop(498, 784, 118, 122), FrameCrop(618, 784, 118, 122),
                    FrameCrop(738, 784, 118, 122), FrameCrop(858, 784, 118, 122), FrameCrop(978, 784, 118, 122)),
                LatestRegularSequence("Hurt", FrameCrop(18, 896, 132, 122), FrameCrop(150, 896, 132, 122), FrameCrop(282, 896, 132, 122)),
                LatestRegularSequence("Death", FrameCrop(414, 896, 178, 122), FrameCrop(592, 896, 178, 122), FrameCrop(770, 896, 178, 122),
                    FrameCrop(948, 896, 178, 122))
            }),
            // The clean, transparent corrupt main has its original 1402x1122 content
            // padded at its top-left inside the supplied 2048x2048 source canvas.
            new Sheet("detective_corrupt_main_transparent.png", "Corrupt", 2048, 2048, new[]
            {
                Sequence("Idle", FrameCrop(186, 42, 152, 174), FrameCrop(338, 42, 152, 174), FrameCrop(490, 42, 152, 174),
                    FrameCrop(642, 42, 152, 174), FrameCrop(794, 42, 152, 174)),
                Sequence("Walk", FrameCrop(12, 248, 170, 166), FrameCrop(182, 248, 170, 166), FrameCrop(352, 248, 170, 166),
                    FrameCrop(522, 248, 170, 166), FrameCrop(692, 248, 170, 166), FrameCrop(862, 248, 170, 166),
                    FrameCrop(1032, 248, 170, 166), FrameCrop(1202, 248, 170, 166)),
                Sequence("Study", FrameCrop(10, 470, 122, 164), FrameCrop(132, 470, 122, 164), FrameCrop(254, 470, 122, 164), FrameCrop(376, 470, 122, 164)),
                Sequence("Attack", FrameCrop(514, 470, 164, 164), FrameCrop(678, 470, 164, 164), FrameCrop(842, 470, 164, 164),
                    FrameCrop(1006, 470, 164, 164), FrameCrop(1170, 470, 164, 164)),
                Sequence("Occult", FrameCrop(12, 700, 188, 174), FrameCrop(200, 700, 188, 174), FrameCrop(388, 700, 188, 174),
                    FrameCrop(576, 700, 188, 174), FrameCrop(764, 700, 188, 174)),
                Sequence("Hurt", FrameCrop(12, 920, 230, 158), FrameCrop(242, 920, 230, 158), FrameCrop(472, 920, 230, 158), FrameCrop(702, 920, 230, 158)),
                Sequence("Death", FrameCrop(1015, 920, 180, 158), FrameCrop(1195, 920, 180, 158))
            })
        };

        [MenuItem("Tools/Cave/Enemies/Build Approved Detective Visuals", priority = 211)]
        public static void BuildApprovedDetectiveVisuals()
        {
            if (!ValidateAndPrepareSources()) return;
            if (!ValidateLayoutsBeforeBuild()) return;

            for (int sheet = 0; sheet < Sheets.Length; sheet++) BuildSheet(Sheets[sheet]);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureRuntimeSprites(RegularRuntimeRoot);
            ConfigureRuntimeSprites(CorruptRuntimeRoot);

            Sprite regularIdle = FirstFrame(RegularRuntimeRoot, "Detective_Regular_Idle_");
            Sprite corruptIdle = FirstFrame(CorruptRuntimeRoot, "Detective_Corrupt_Idle_");
            if (regularIdle == null || corruptIdle == null)
            {
                Debug.LogError("[Cave] Detective visual build stopped: canonical idle frames were not imported.");
                return;
            }

            if (!CreateVisualPrefab(RegularVisualPrefabPath, "DetectiveRegularVisual", regularIdle) ||
                !CreateVisualPrefab(CorruptVisualPrefabPath, "DetectiveCorruptVisual", corruptIdle)) return;
            BindAuthoritativeDetectivePrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built and bound the canonical approved Detective visual set.");
        }

        private static bool ValidateAndPrepareSources()
        {
            bool valid = true;
            for (int index = 0; index < Sheets.Length; index++)
            {
                string path = SourceRoot + "/" + Sheets[index].Filename;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("[Cave] Detective visual build stopped: approved source missing: " + path);
                    valid = false;
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return valid;
        }

        private static bool ValidateLayoutsBeforeBuild()
        {
            bool valid = true;
            for (int sheetIndex = 0; sheetIndex < Sheets.Length; sheetIndex++)
            {
                Sheet sheet = Sheets[sheetIndex];
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot + "/" + sheet.Filename);
                if (source == null || !source.isReadable)
                {
                    Debug.LogError("[Cave] Detective crop validation failed: readable source missing: " + sheet.Filename);
                    valid = false;
                    continue;
                }

                if (source.width != sheet.ExpectedWidth || source.height != sheet.ExpectedHeight)
                {
                    Debug.LogError("[Cave] Detective crop validation failed: " + sheet.Filename + " is " + source.width + "x" + source.height +
                        ", but its approved layout is " + sheet.ExpectedWidth + "x" + sheet.ExpectedHeight + ".");
                    valid = false;
                }

                for (int rowIndex = 0; rowIndex < sheet.Rows.Length; rowIndex++)
                {
                    FrameRow row = sheet.Rows[rowIndex];
                    for (int frameIndex = 0; frameIndex < row.Frames.Length; frameIndex++)
                    {
                        RectInt ignored;
                        if (!TryGetValidatedRect(source, sheet, row, frameIndex, out ignored)) valid = false;
                    }
                }
            }
            return valid;
        }

        private static void BuildSheet(Sheet sheet)
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot + "/" + sheet.Filename);
            if (source == null || !source.isReadable)
            {
                Debug.LogError("[Cave] Detective source is unavailable/read-protected: " + sheet.Filename);
                return;
            }

            string destination = sheet.Form == "Regular" ? RegularRuntimeRoot : CorruptRuntimeRoot;
            for (int row = 0; row < sheet.Rows.Length; row++)
            {
                FrameRow definition = sheet.Rows[row];
                for (int frame = 0; frame < definition.Frames.Length; frame++)
                {
                    RectInt rect;
                    if (!TryGetValidatedRect(source, sheet, definition, frame, out rect)) return;
                    FramePixels pixels = Extract(source, rect);
                    if (!ValidateAlphaRetention(sheet.Filename, definition.Action, frame, rect, pixels)) continue;
                    string filename = "Detective_" + sheet.Form + "_" + definition.Action + "_" + frame.ToString("00") + ".png";
                    if (!WriteNormalized(pixels, destination, filename))
                    {
                        Debug.LogWarning("[Cave] Skipped empty Detective runtime frame | source='" + sheet.Filename +
                            "' sequence='" + definition.Action + "' frame=" + frame + " unityRect=(x:" + rect.x +
                            ", y:" + rect.y + ", width:" + rect.width + ", height:" + rect.height + ").");
                    }
                }
            }
        }

        private static FrameRow Sequence(string action, params Crop[] frames)
        {
            return new FrameRow(action, frames);
        }

        // The approved latest Regular source is a 2048x1024 horizontal 4/3 scale of
        // the immediately preceding approved 1536x1024 layout. Rounding is always to
        // the nearest pixel so the edge frame maps exactly to the 2048-pixel boundary.
        private static FrameRow LatestRegularSequence(string action, params Crop[] previousLayoutFrames)
        {
            var frames = new Crop[previousLayoutFrames.Length];
            for (int index = 0; index < previousLayoutFrames.Length; index++)
            {
                Crop frame = previousLayoutFrames[index];
                frames[index] = new Crop(ScaleLatestRegularX(frame.Left), frame.Top,
                    ScaleLatestRegularX(frame.Width), frame.Height);
            }
            return new FrameRow(action, frames);
        }

        private static int ScaleLatestRegularX(int value)
        {
            return Mathf.RoundToInt(value * (2048f / 1536f));
        }

        private static Crop FrameCrop(int left, int top, int width, int height)
        {
            return new Crop(left, top, width, height);
        }

        private static bool TryGetValidatedRect(Texture2D source, Sheet sheet, FrameRow row, int frameIndex, out RectInt rect)
        {
            Crop crop = row.Frames[frameIndex];
            rect = new RectInt(crop.Left, source.height - crop.Top - crop.Height, crop.Width, crop.Height);
            bool inBounds = crop.Left >= 0 && crop.Top >= 0 && crop.Width > 0 && crop.Height > 0 &&
                            rect.x >= 0 && rect.y >= 0 && rect.width > 0 && rect.height > 0 &&
                            rect.x + rect.width <= source.width && rect.y + rect.height <= source.height;
            if (inBounds) return true;

            Debug.LogError("[Cave] Detective crop validation failed | source='" + sheet.Filename + "' sequence='" + row.Action +
                "' frame=" + frameIndex + " requested=(left:" + crop.Left + ", top:" + crop.Top + ", width:" + crop.Width +
                ", height:" + crop.Height + ") unityRect=(x:" + rect.x + ", y:" + rect.y + ", width:" + rect.width +
                ", height:" + rect.height + ") source=" + source.width + "x" + source.height + ".");
            return false;
        }

        private static FramePixels Extract(Texture2D source, RectInt rect)
        {
            Color[] pixels = source.GetPixels(rect.x, rect.y, rect.width, rect.height);
            int sourceVisiblePixels = CountVisiblePixels(pixels);
            bool preservesSourceAlpha = HasMeaningfulTransparency(pixels);
            if (!preservesSourceAlpha)
            {
                // This is deliberately reserved for an opaque source with a baked
                // presentation matte. Alpha-authored Detective frames must retain
                // every source alpha value, including dark clothing and soft VFX.
                RemoveConnectedMatte(pixels, rect.width, rect.height);
            }
            int outputVisiblePixels = CountVisiblePixels(pixels);
            return new FramePixels(pixels, rect.width, AlphaBounds(pixels, rect.width, rect.height),
                sourceVisiblePixels, outputVisiblePixels, preservesSourceAlpha);
        }

        private static bool ValidateAlphaRetention(string source, string sequence, int frame, RectInt rect, FramePixels pixels)
        {
            if (!pixels.PreservesSourceAlpha || pixels.SourceVisiblePixels == 0) return true;
            if (pixels.OutputVisiblePixels == pixels.SourceVisiblePixels) return true;

            float retained = 100f * pixels.OutputVisiblePixels / pixels.SourceVisiblePixels;
            Debug.LogError("[Cave] Detective alpha validation failed | source='" + source + "' sequence='" + sequence +
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

        private static bool WriteNormalized(FramePixels frame, string folder, string filename)
        {
            if (frame.Bounds.size.x <= 0 || frame.Bounds.size.y <= 0)
            {
                return false;
            }

            Color[] output = new Color[CanvasWidth * CanvasHeight];
            int destinationX = Mathf.Max(0, (CanvasWidth - frame.Bounds.size.x) / 2);
            int destinationY = GroundPixels;
            int copyWidth = Mathf.Min(frame.Bounds.size.x, CanvasWidth - destinationX);
            int copyHeight = Mathf.Min(frame.Bounds.size.y, CanvasHeight - destinationY);
            if (copyWidth != frame.Bounds.size.x || copyHeight != frame.Bounds.size.y)
            {
                Debug.LogError("[Cave] Detective runtime frame exceeds its fixed canvas and was not saved: " + filename);
                return false;
            }
            for (int y = 0; y < copyHeight; y++)
            {
                Array.Copy(frame.Pixels, (frame.Bounds.position.y + y) * frame.Width + frame.Bounds.position.x,
                    output, (destinationY + y) * CanvasWidth + destinationX, copyWidth);
            }

            if (frame.PreservesSourceAlpha && CountVisiblePixels(output) != frame.SourceVisiblePixels)
            {
                Debug.LogError("[Cave] Detective alpha validation failed after padding; frame was not saved: " + filename);
                return false;
            }

            Texture2D texture = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
            texture.SetPixels(output);
            texture.Apply(false, false);
            string assetPath = folder + "/" + filename;
            string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, ImageConversion.EncodeToPNG(texture));
            UnityEngine.Object.DestroyImmediate(texture);
            return true;
        }

        private static void ConfigureRuntimeSprites(string root)
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
                importer.spritePivot = new Vector2(.5f, GroundPixels / (float)CanvasHeight);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static bool CreateVisualPrefab(string path, string name, Sprite idle)
        {
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!EnsureAssetFolder(folder)) return false;
            GameObject root = new GameObject(name);
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = idle;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 0;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool EnsureAssetFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || !assetFolder.StartsWith("Assets", StringComparison.Ordinal))
            {
                Debug.LogError("[Cave] Detective visual prefab build stopped: invalid asset folder '" + assetFolder + "'.");
                return false;
            }

            string[] segments = assetFolder.Split('/');
            string current = segments[0];
            if (!AssetDatabase.IsValidFolder(current))
            {
                Debug.LogError("[Cave] Detective visual prefab build stopped: Unity Assets root is unavailable.");
                return false;
            }

            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (AssetDatabase.IsValidFolder(next))
                {
                    current = next;
                    continue;
                }

                string guid = AssetDatabase.CreateFolder(current, segments[index]);
                if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(next))
                {
                    Debug.LogError("[Cave] Detective visual prefab build stopped: could not create folder '" + next + "'.");
                    return false;
                }
                current = next;
            }
            return true;
        }

        private static void BindAuthoritativeDetectivePrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DetectivePrefabPath) == null)
            {
                Debug.LogError("[Cave] Detective visual bind stopped: authoritative Detective prefab is missing.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(CorruptDetectivePrefabPath) == null)
            {
                GameObject source = PrefabUtility.LoadPrefabContents(DetectivePrefabPath);
                try { PrefabUtility.SaveAsPrefabAsset(source, CorruptDetectivePrefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(source); }
            }

            BindPrefab(DetectivePrefabPath, false);
            BindPrefab(CorruptDetectivePrefabPath, true);

            GameObject corruptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorruptDetectivePrefabPath);
            GameObject regular = PrefabUtility.LoadPrefabContents(DetectivePrefabPath);
            try
            {
                AuthoredCorruptedPrefabBinding binding = regular.GetComponent<AuthoredCorruptedPrefabBinding>();
                if (binding == null) binding = regular.AddComponent<AuthoredCorruptedPrefabBinding>();
                SerializedObject serialized = new SerializedObject(binding);
                serialized.FindProperty("authoredCorruptedPrefab").objectReferenceValue = corruptPrefab;
                serialized.FindProperty("isBossMapping").boolValue = false;
                serialized.FindProperty("grantGeneralShardOnFinalDeath").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(regular, DetectivePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(regular); }
        }

        private static void BindPrefab(string prefabPath, bool corrupt)
        {
            string root = corrupt ? CorruptRuntimeRoot : RegularRuntimeRoot;
            string form = corrupt ? "Corrupt" : "Regular";
            Sprite[] idle = Frames(root, "Detective_" + form + "_" + (corrupt ? "Idle_" : "Idle_"));
            Sprite[] walk = Frames(root, "Detective_" + form + "_" + (corrupt ? "Walk_" : "Walk_"));
            Sprite[] light = Frames(root, "Detective_" + form + "_" + (corrupt ? "Attack_" : "Shoot_"));
            Sprite[] utility = Frames(root, "Detective_" + form + "_" + (corrupt ? "Occult_" : "Smoke_"));
            Sprite[] study = Frames(root, "Detective_" + form + "_Study_");
            Sprite[] hurt = Frames(root, "Detective_" + form + "_" + (corrupt ? "Hurt_" : "Hurt_"));
            Sprite[] death = Frames(root, "Detective_" + form + "_Death_");
            if (idle.Length == 0 || walk.Length == 0 || light.Length == 0 || utility.Length == 0 || study.Length == 0 || hurt.Length == 0 || death.Length == 0)
            {
                Debug.LogError("[Cave] Detective visual bind stopped: one or more canonical frame groups are missing for " + form + ".");
                return;
            }

            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer legacy = prefab.GetComponent<SpriteRenderer>();
                if (legacy == null)
                {
                    Debug.LogError("[Cave] Detective prefab has no root SpriteRenderer: " + prefabPath);
                    return;
                }

                Transform visual = FindOrInstantiateVisual(prefab.transform, corrupt ? CorruptVisualPrefabPath : RegularVisualPrefabPath);
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                renderer.sortingLayerID = legacy.sortingLayerID;
                renderer.sortingOrder = legacy.sortingOrder;
                renderer.sprite = idle[0];
                renderer.enabled = true;
                DisableSupersededVisual(prefab.transform, visual);
                legacy.sprite = null;
                legacy.enabled = false;
                Animator oldAnimator = prefab.GetComponent<Animator>();
                if (oldAnimator != null) oldAnimator.enabled = false;

                ApprovedEnemySheetAnimator animator = prefab.GetComponent<ApprovedEnemySheetAnimator>();
                if (animator == null) animator = prefab.AddComponent<ApprovedEnemySheetAnimator>();
                animator.Configure(corrupt ? ApprovedEnemySheetAnimator.VisualRole.CorruptDetective : ApprovedEnemySheetAnimator.VisualRole.Detective,
                    renderer, legacy, EmptyRenderers(), idle, walk, Empty(), light, Empty(), Empty(), study, utility, hurt, death, true);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static Transform FindOrInstantiateVisual(Transform parent, string visualPrefabPath)
        {
            Transform existing = parent.Find("Approved Detective Visual");
            if (existing != null) return existing;
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, parent);
            instance.name = "Approved Detective Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance.transform;
        }

        private static void DisableSupersededVisual(Transform root, Transform canonical)
        {
            Transform old = root.Find("Approved Enemy Sheet Visual");
            if (old == null || old == canonical) return;
            SpriteRenderer oldRenderer = old.GetComponent<SpriteRenderer>();
            if (oldRenderer != null) oldRenderer.enabled = false;
        }

        private static Sprite[] Frames(string folder, string prefix)
        {
            string[] ids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var frames = new List<Sprite>();
            for (int index = 0; index < ids.Length; index++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(ids[index]));
                if (sprite != null && sprite.name.StartsWith(prefix, StringComparison.Ordinal)) frames.Add(sprite);
            }
            frames.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return frames.ToArray();
        }

        private static Sprite FirstFrame(string folder, string prefix)
        {
            Sprite[] frames = Frames(folder, prefix);
            return frames.Length > 0 ? frames[0] : null;
        }

        private static Sprite[] Empty() { return new Sprite[0]; }
        private static SpriteRenderer[] EmptyRenderers() { return new SpriteRenderer[0]; }

        private static void RemoveConnectedMatte(Color[] pixels, int width, int height)
        {
            List<Color> palette = BuildEdgePalette(pixels, width, height);
            bool[] visited = new bool[pixels.Length];
            var queue = new Queue<int>();
            for (int x = 0; x < width; x++) { queue.Enqueue(x); queue.Enqueue((height - 1) * width + x); }
            for (int y = 1; y < height - 1; y++) { queue.Enqueue(y * width); queue.Enqueue(y * width + width - 1); }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (index < 0 || index >= pixels.Length || visited[index] || !IsMatte(pixels[index], palette)) continue;
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
            var palette = new List<Color>(32);
            for (int x = 0; x < width; x++)
            {
                AddPaletteSample(palette, pixels[x]);
                if (height > 1) AddPaletteSample(palette, pixels[(height - 1) * width + x]);
            }
            for (int y = 1; y < height - 1; y++)
            {
                AddPaletteSample(palette, pixels[y * width]);
                if (width > 1) AddPaletteSample(palette, pixels[y * width + width - 1]);
            }
            return palette;
        }

        private static void AddPaletteSample(List<Color> palette, Color candidate)
        {
            if (palette.Count >= 32) return;
            for (int index = 0; index < palette.Count; index++)
            {
                Color existing = palette[index];
                float r = candidate.r - existing.r, g = candidate.g - existing.g, b = candidate.b - existing.b;
                if (r * r + g * g + b * b <= .0025f) return;
            }
            palette.Add(candidate);
        }

        private static bool IsMatte(Color value, List<Color> palette)
        {
            if (value.a < .03f) return true;
            for (int index = 0; index < palette.Count; index++)
            {
                Color edge = palette[index];
                float r = value.r - edge.r, g = value.g - edge.g, b = value.b - edge.b;
                if (r * r + g * g + b * b <= .0121f) return true;
            }
            return false;
        }

        private static void HardenInteriorBodyPixels(Color[] pixels, int width, int height)
        {
            Color[] source = (Color[])pixels.Clone();
            for (int y = 1; y < height - 1; y++) for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                if (source[index].a <= .03f || source[index].a >= .995f ||
                    source[index - 1].a <= .03f || source[index + 1].a <= .03f ||
                    source[index - width].a <= .03f || source[index + width].a <= .03f) continue;
                Color value = source[index]; value.a = 1f; pixels[index] = value;
            }
        }

        private static BoundsInt AlphaBounds(Color[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (pixels[y * width + x].a > .03f)
            { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            return maxX < minX ? new BoundsInt() : new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }

        private readonly struct Sheet
        {
            public readonly string Filename; public readonly string Form; public readonly int ExpectedWidth; public readonly int ExpectedHeight; public readonly FrameRow[] Rows;
            public Sheet(string filename, string form, int expectedWidth, int expectedHeight, FrameRow[] rows)
            {
                Filename = filename; Form = form; ExpectedWidth = expectedWidth; ExpectedHeight = expectedHeight; Rows = rows;
            }
        }

        private readonly struct FrameRow
        {
            public readonly string Action; public readonly Crop[] Frames;
            public FrameRow(string action, Crop[] frames) { Action = action; Frames = frames; }
        }

        private readonly struct Crop
        {
            public readonly int Left; public readonly int Top; public readonly int Width; public readonly int Height;
            public Crop(int left, int top, int width, int height) { Left = left; Top = top; Width = width; Height = height; }
        }

        private sealed class FramePixels
        {
            public readonly Color[] Pixels; public readonly int Width; public readonly BoundsInt Bounds;
            public readonly int SourceVisiblePixels; public readonly int OutputVisiblePixels; public readonly bool PreservesSourceAlpha;
            public FramePixels(Color[] pixels, int width, BoundsInt bounds, int sourceVisiblePixels, int outputVisiblePixels, bool preservesSourceAlpha)
            {
                Pixels = pixels; Width = width; Bounds = bounds; SourceVisiblePixels = sourceVisiblePixels;
                OutputVisiblePixels = outputVisiblePixels; PreservesSourceAlpha = preservesSourceAlpha;
            }
        }
    }
}
#endif
