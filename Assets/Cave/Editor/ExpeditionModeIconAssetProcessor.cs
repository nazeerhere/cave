#if UNITY_EDITOR
using System.IO;
using Cave.Missions;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Extracts the supplied transparent 2x2 expedition icon sheet and binds it to the mission catalog.</summary>
    public static class ExpeditionModeIconAssetProcessor
    {
        private const string SourcePath = "Assets/Cave/Art/UI/Missions/Source/ExpeditionModeIcons.png";
        private const string RuntimeFolder = "Assets/Cave/Resources/UI/Missions/ModeIcons";
        private const string CatalogPath = "Assets/Cave/Resources/Missions/CaveMissionCatalog.asset";
        private const int SheetSize = 1254;
        private const int CellSize = 627;

        private static readonly IconSlice[] Slices =
        {
            new IconSlice(CaveGameMode.ClassicSweep, "MissionMode_ClassicSweep", 0, 0),
            new IconSlice(CaveGameMode.CorruptionPurge, "MissionMode_CorruptionPurge", 627, 0),
            new IconSlice(CaveGameMode.CorruptBounty, "MissionMode_CorruptBounty", 0, 627),
            new IconSlice(CaveGameMode.Containment, "MissionMode_Containment", 627, 627)
        };

        [MenuItem("Tools/Cave/Missions/Build Expedition Mode Icons")]
        public static void Build()
        {
            Texture2D source = PrepareReadableSource();
            if (source == null)
            {
                Debug.LogError("[Cave] Expedition icon build stopped: missing source " + SourcePath);
                return;
            }

            if (source.width != SheetSize || source.height != SheetSize)
            {
                Debug.LogError("[Cave] Expedition icon build stopped: source is " + source.width + "x" + source.height
                    + ", expected " + SheetSize + "x" + SheetSize + ".");
                return;
            }

            EnsureFolder(RuntimeFolder);
            Sprite[] icons = new Sprite[Slices.Length];
            for (int index = 0; index < Slices.Length; index++)
            {
                IconSlice slice = Slices[index];
                RectInt crop = ToUnityRect(slice);
                ValidateCrop(source, slice, crop);
                icons[index] = WriteRuntimeSprite(source, slice, crop);
                if (icons[index] == null)
                {
                    Debug.LogError("[Cave] Expedition icon build stopped: failed to import " + slice.spriteName + ".");
                    return;
                }
            }

            BindCatalogIcons(icons);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cave] Built and bound four approved Expedition Mode icons.");
        }

        private static Texture2D PrepareReadableSource()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null) return null;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = Mathf.Max(2048, importer.maxTextureSize);
            AssetDatabase.WriteImportSettingsIfDirty(SourcePath);
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceUpdate);

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (source != null)
            {
                Debug.Log("[Cave] Expedition icon source imported: " + SourcePath
                    + " | texture=" + source.name
                    + " | size=" + source.width + "x" + source.height + ".");
            }
            return source;
        }

        private static RectInt ToUnityRect(IconSlice slice)
        {
            // Authored sheet measurements use top-left origin; Texture2D.GetPixels uses bottom-left.
            return new RectInt(slice.x, SheetSize - slice.top - CellSize, CellSize, CellSize);
        }

        private static void ValidateCrop(Texture2D source, IconSlice slice, RectInt crop)
        {
            if (crop.x < 0 || crop.y < 0 || crop.width <= 0 || crop.height <= 0
                || crop.xMax > source.width || crop.yMax > source.height)
            {
                throw new System.ArgumentOutOfRangeException(slice.spriteName,
                    "Expedition icon crop outside source " + source.width + "x" + source.height + ": "
                    + crop.x + "," + crop.y + "," + crop.width + "," + crop.height);
            }
        }

        private static Sprite WriteRuntimeSprite(Texture2D source, IconSlice slice, RectInt crop)
        {
            Color[] pixels = source.GetPixels(crop.x, crop.y, crop.width, crop.height);
            Texture2D output = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false, false);
            output.SetPixels(pixels);
            output.Apply(false, false);

            string path = RuntimeFolder + "/" + slice.spriteName + ".png";
            File.WriteAllBytes(path, output.EncodeToPNG());
            Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BindCatalogIcons(Sprite[] icons)
        {
            CaveMissionCatalog catalog = AssetDatabase.LoadAssetAtPath<CaveMissionCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Cave] Expedition icon build stopped: missing mission catalog " + CatalogPath);
                return;
            }

            CaveModeDefinition[] modes = catalog.Modes;
            for (int iconIndex = 0; iconIndex < Slices.Length; iconIndex++)
            {
                bool assigned = false;
                for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
                {
                    CaveModeDefinition mode = modes[modeIndex];
                    if (mode == null || mode.mode != Slices[iconIndex].mode) continue;
                    mode.icon = icons[iconIndex];
                    assigned = true;
                    break;
                }
                if (!assigned)
                {
                    Debug.LogError("[Cave] Expedition icon binding skipped: catalog is missing " + Slices[iconIndex].mode + ".");
                }
            }
            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int separator = folder.LastIndexOf('/');
            string parent = folder.Substring(0, separator);
            string name = folder.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private struct IconSlice
        {
            public readonly CaveGameMode mode;
            public readonly string spriteName;
            public readonly int x;
            public readonly int top;

            public IconSlice(CaveGameMode mode, string spriteName, int x, int top)
            {
                this.mode = mode;
                this.spriteName = spriteName;
                this.x = x;
                this.top = top;
            }
        }
    }
}
#endif
