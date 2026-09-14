#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Copies the approved, already-isolated Oblivion Disk frames into the
    /// runtime Resources tree. The source files are transparent individual
    /// sprites, so this deliberately performs no crop, matte removal, or
    /// recoloring that could damage authored pixels.
    /// </summary>
    public static class OblivionDiskAssetProcessor
    {
        public const string SourceRoot = "Assets/Cave/Art/FieldControl/OblivionDisk/Source/Approved/Oblivion_Disk_Individual_Sprites_COLOR_PRESERVED_v5";
        public const string RuntimeRoot = "Assets/Cave/Resources/VFX/OblivionDisk/Runtime/Approved";

        private const float PixelsPerUnit = 64f;
        [MenuItem("Tools/Cave/Field Control/Build Oblivion Disk Visuals")]
        public static void Build()
        {
            if (!ValidateApprovedSource()) return;

            // Runtime sequences intentionally use a maximum of six frames each.
            CopySelectedFrames("Idle", "Idle", new[] { 1, 2, 4, 5, 7, 8 });
            CopySelectedFrames("Deploy_Arming", "Arming", new[] { 1, 2, 3, 4, 6, 7 });
            CopySelectedFrames("Explosion", "Explosion", new[] { 1, 3, 5, 7, 9, 12 });
            BuildLinkCoreFrames(new[] { 1, 2, 3, 5, 6, 8 });
            CopySelectedFrames("Deactivate_Shutdown", "Shutdown", new[] { 1, 2, 3, 4, 5, 6 });

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureRuntimeSprites();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built Oblivion Disk runtime frames from approved individual sprites.");
        }

        private static bool ValidateApprovedSource()
        {
            string absoluteRoot = Path.Combine(Application.dataPath, SourceRoot.Substring("Assets/".Length));
            if (!Directory.Exists(absoluteRoot))
            {
                Debug.LogError("[Cave] Oblivion Disk build stopped: approved individual source is missing: " + SourceRoot);
                return false;
            }
            return true;
        }

        private static void CopySelectedFrames(string sourceFolder, string runtimeFolder, int[] selectedFrames)
        {
            if (selectedFrames == null || selectedFrames.Length == 0 || selectedFrames.Length > 6)
            {
                Debug.LogError("[Cave] Oblivion Disk build stopped: invalid selected-frame count for " + runtimeFolder + ".");
                return;
            }
            string sourceDirectory = Path.Combine(Application.dataPath, SourceRoot.Substring("Assets/".Length), sourceFolder);
            string runtimeDirectory = Path.Combine(Application.dataPath, RuntimeRoot.Substring("Assets/".Length), runtimeFolder);
            Directory.CreateDirectory(runtimeDirectory);
            for (int index = 0; index < selectedFrames.Length; index++)
            {
                string filename = "frame_" + selectedFrames[index].ToString("00") + ".png";
                string source = Path.Combine(sourceDirectory, filename);
                if (!File.Exists(source))
                {
                    Debug.LogError("[Cave] Oblivion Disk build stopped: missing approved " + sourceFolder + " frame " + filename + ".");
                    return;
                }

                // Copy raw PNG bytes: authored alpha, colour, and crop are canonical.
                File.Copy(source, Path.Combine(runtimeDirectory, "OblivionDisk_" + runtimeFolder + "_" + (index + 1).ToString("00") + ".png"), true);
            }
        }

        private static void BuildLinkCoreFrames(int[] selectedFrames)
        {
            const string sourceFolder = "Field_Link_Beam";
            const string runtimeFolder = "LinkCore";
            const int coreWidth = 32;
            const int coreHeight = 20;
            if (selectedFrames == null || selectedFrames.Length == 0 || selectedFrames.Length > 6)
            {
                Debug.LogError("[Cave] Oblivion Disk build stopped: invalid selected-frame count for Field Link.");
                return;
            }

            string sourceDirectory = Path.Combine(Application.dataPath, SourceRoot.Substring("Assets/".Length), sourceFolder);
            string runtimeDirectory = Path.Combine(Application.dataPath, RuntimeRoot.Substring("Assets/".Length), runtimeFolder);
            Directory.CreateDirectory(runtimeDirectory);
            for (int index = 0; index < selectedFrames.Length; index++)
            {
                string filename = "frame_" + selectedFrames[index].ToString("00") + ".png";
                string sourcePath = SourceRoot + "/" + sourceFolder + "/" + filename;
                string source = Path.Combine(sourceDirectory, filename);
                if (!File.Exists(source) || !MakeReadable(sourcePath))
                {
                    Debug.LogError("[Cave] Oblivion Disk build stopped: missing or unreadable approved Field Link frame " + filename + ".");
                    return;
                }

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                int width = Mathf.Min(coreWidth, texture != null ? texture.width : 0);
                int height = Mathf.Min(coreHeight, texture != null ? texture.height : 0);
                if (texture == null || width <= 0 || height <= 0)
                {
                    Debug.LogError("[Cave] Oblivion Disk build stopped: invalid approved Field Link source " + sourcePath + ".");
                    return;
                }

                // The original link frames contain two endpoint disks. Retain only
                // their centred authored beam segment so the dynamic renderer can
                // tile it without stretching a precomposed two-mine image.
                int x = (texture.width - width) / 2;
                int y = (texture.height - height) / 2;
                Color[] pixels = texture.GetPixels(x, y, width, height);
                WritePng(runtimeDirectory, "OblivionDisk_LinkCore_" + (index + 1).ToString("00") + ".png", pixels, width, height);
            }
        }

        private static bool MakeReadable(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return true;
        }

        private static void WritePng(string folder, string filename, Color[] pixels, int width, int height)
        {
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            output.SetPixels(pixels);
            output.Apply(false, false);
            File.WriteAllBytes(Path.Combine(folder, filename), output.EncodeToPNG());
            Object.DestroyImmediate(output);
        }

        private static void ConfigureRuntimeSprites()
        {
            string absoluteRoot = Path.Combine(Application.dataPath, "Cave/Resources/VFX/OblivionDisk/Runtime");
            if (!Directory.Exists(absoluteRoot)) return;
            string[] paths = Directory.GetFiles(absoluteRoot, "*.png", SearchOption.AllDirectories);
            for (int index = 0; index < paths.Length; index++)
            {
                string assetPath = "Assets" + paths[index].Substring(Application.dataPath.Length).Replace('\\', '/');
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = assetPath.Contains("/LinkCore/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
