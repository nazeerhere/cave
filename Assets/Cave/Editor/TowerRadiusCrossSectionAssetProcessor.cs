#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cave.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    public static class TowerRadiusCrossSectionAssetProcessor
    {
        private const string SourcePath = "Assets/Cave/Art/VFX/TowerRadius/Source/TowerRadiusCrossSectionSheet.png";
        private const string RuntimeRoot = "Assets/Cave/Art/VFX/TowerRadius/Runtime";
        private const string PrefabPath = "Assets/Cave/Prefabs/VFX/TowerRadiusCrossSectionVfx.prefab";
        private const string CatalogPath = "Assets/Cave/Resources/VFX/TowerRadiusCrossSectionCatalog.asset";
        private const int Columns = 6;
        private const int Rows = 5;
        private const int FrameCount = Columns * Rows;
        private const int CanvasWidth = 256;
        private const int CanvasHeight = 205;
        private const int CrossSectionReferencePixels = 46;

        [MenuItem("Tools/Cave/Towers/Build Radius Cross Section VFX", priority = 213)]
        public static void Build()
        {
            Texture2D source = PrepareSource();
            if (source == null || !ValidateSourceAlpha(source)) return;

            for (int frame = 0; frame < FrameCount; frame++)
            {
                RectInt rect;
                if (!TryGetCell(source, frame, out rect)) return;
                Color[] pixels = source.GetPixels(rect.x, rect.y, rect.width, rect.height);
                if (CountVisiblePixels(pixels) == 0)
                {
                    Debug.LogError("[Cave] Tower radius build stopped: empty frame " + frame + " in " + SourcePath + ".");
                    return;
                }
                WriteFrame(pixels, rect.width, rect.height, frame);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureRuntimeSprites();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Sprite[] frames = LoadFrames();
            if (frames.Length != FrameCount)
            {
                Debug.LogError("[Cave] Tower radius build stopped: expected " + FrameCount + " runtime sprites, found " + frames.Length + ".");
                return;
            }
            CreatePrefabAndCatalog(frames);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built Tower Radius Cross Section VFX from the approved source.");
        }

        private static Texture2D PrepareSource()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Cave] Tower radius build stopped: approved source is missing: " + SourcePath);
                return null;
            }
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
        }

        private static bool ValidateSourceAlpha(Texture2D source)
        {
            Color[] pixels = source.GetPixels();
            for (int index = 0; index < pixels.Length; index++) if (pixels[index].a < .995f) return true;
            Debug.LogError("[Cave] Tower radius build stopped: approved source has no usable alpha; no destructive matte removal is permitted.");
            return false;
        }

        private static bool TryGetCell(Texture2D source, int frame, out RectInt rect)
        {
            int column = frame % Columns;
            int rowFromTop = frame / Columns;
            int left = Mathf.FloorToInt(column * source.width / (float)Columns);
            int right = Mathf.FloorToInt((column + 1) * source.width / (float)Columns);
            int top = Mathf.FloorToInt(rowFromTop * source.height / (float)Rows);
            int bottom = Mathf.FloorToInt((rowFromTop + 1) * source.height / (float)Rows);
            rect = new RectInt(left, source.height - bottom, right - left, bottom - top);
            bool valid = rect.x >= 0 && rect.y >= 0 && rect.width > 0 && rect.height > 0 &&
                rect.x + rect.width <= source.width && rect.y + rect.height <= source.height;
            if (valid) return true;
            Debug.LogError("[Cave] Tower radius crop validation failed | frame=" + frame + " source='" + SourcePath +
                "' requested=(x:" + rect.x + ", y:" + rect.y + ", width:" + rect.width + ", height:" + rect.height +
                ") source=" + source.width + "x" + source.height + ".");
            return false;
        }

        private static int CountVisiblePixels(Color[] pixels)
        {
            int visible = 0;
            for (int index = 0; index < pixels.Length; index++) if (pixels[index].a > .03f) visible++;
            return visible;
        }

        private static void WriteFrame(Color[] pixels, int width, int height, int frame)
        {
            Texture2D output = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
            Color[] canvas = new Color[CanvasWidth * CanvasHeight];
            int copyWidth = Mathf.Min(width, CanvasWidth);
            int copyHeight = Mathf.Min(height, CanvasHeight);
            int sourceX = Mathf.Max(0, (width - copyWidth) / 2);
            int sourceY = Mathf.Max(0, (height - copyHeight) / 2);
            int destinationX = (CanvasWidth - copyWidth) / 2;
            int destinationY = (CanvasHeight - copyHeight) / 2;
            for (int y = 0; y < copyHeight; y++) Array.Copy(pixels, (sourceY + y) * width + sourceX, canvas,
                (destinationY + y) * CanvasWidth + destinationX, copyWidth);
            output.SetPixels(canvas);
            output.Apply(false, false);
            string assetPath = RuntimeRoot + "/TowerRadiusCrossSection_" + frame.ToString("00") + ".png";
            string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, ImageConversion.EncodeToPNG(output));
            UnityEngine.Object.DestroyImmediate(output);
        }

        private static Sprite[] LoadFrames()
        {
            var frames = new List<Sprite>(FrameCount);
            for (int index = 0; index < FrameCount; index++)
            {
                string path = RuntimeRoot + "/TowerRadiusCrossSection_" + index.ToString("00") + ".png";
                Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frame != null) frames.Add(frame);
            }
            return frames.ToArray();
        }

        private static void ConfigureRuntimeSprites()
        {
            string[] ids = AssetDatabase.FindAssets("t:Texture2D", new[] { RuntimeRoot });
            for (int index = 0; index < ids.Length; index++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(ids[index])) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128f;
                importer.spritePivot = new Vector2(.5f, CrossSectionReferencePixels / (float)CanvasHeight);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static void CreatePrefabAndCatalog(Sprite[] frames)
        {
            EnsureAssetFolder("Assets/Cave/Prefabs/VFX");
            EnsureAssetFolder("Assets/Cave/Resources/VFX");
            GameObject root = new GameObject("TowerRadiusCrossSectionVfx");
            try
            {
                GameObject visualRoot = new GameObject("VisualRoot", typeof(SpriteRenderer));
                visualRoot.transform.SetParent(root.transform, false);
                SpriteRenderer renderer = visualRoot.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 2;
                TowerRadiusCrossSectionVfx presentation = root.AddComponent<TowerRadiusCrossSectionVfx>();
                presentation.Configure(renderer, frames, 18f, CanvasWidth / 128f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            TowerRadiusCrossSectionVfx prefab = AssetDatabase.LoadAssetAtPath<TowerRadiusCrossSectionVfx>(PrefabPath);
            TowerRadiusCrossSectionCatalog catalog = AssetDatabase.LoadAssetAtPath<TowerRadiusCrossSectionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TowerRadiusCrossSectionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty prefabProperty = serializedCatalog.FindProperty("prefab");
            if (prefabProperty == null)
            {
                Debug.LogError("[Cave] Tower Radius build stopped: catalog prefab field is missing.");
                return;
            }
            prefabProperty.objectReferenceValue = prefab;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
#endif
