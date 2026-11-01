#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>
    /// Keeps the supplied, individually-cropped skin frames compatible with the existing
    /// player visual envelope. These settings affect presentation only.
    /// </summary>
    public sealed class PlayerSkinSpriteImporter : AssetPostprocessor
    {
        private const string SkinRoot = "Assets/Cave/Resources/Player/Skins/";
        private const int ExpectedFrameCount = 672;
        private static readonly string[] KnownStaleFramePaths =
        {
            "Assets/Cave/Resources/Player/Skins/AshenMonarch/Brace/frame_03.png",
            "Assets/Cave/Resources/Player/Skins/AshenMonarch/Heavy_Charge/frame_04.png",
            "Assets/Cave/Resources/Player/Skins/CrimsonExile/GuardBreak/frame_04.png",
            "Assets/Cave/Resources/Player/Skins/CrimsonExile/Heavy_5/frame_04.png",
            "Assets/Cave/Resources/Player/Skins/CyberpunkStarlet/Heavy_1/CyberpunkStarlet_Heavy_1_00.png",
            "Assets/Cave/Resources/Player/Skins/CyberpunkStarlet/Heavy_Charge/CyberpunkStarlet_Heavy_Charge_00.png",
            "Assets/Cave/Resources/Player/Skins/IvorySovereign/Heavy_1/frame_02.png",
            "Assets/Cave/Resources/Player/Skins/OrnateRoseWitch/Run/OrnateRoseWitch_Run_02.png"
        };

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SkinRoot) || !assetPath.EndsWith(".png")) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            ApplyContract(importer);
        }

        [MenuItem("Tools/Cave/Reimport Player Skin Imports")]
        private static void ReimportPlayerSkinImports()
        {
            List<string> paths = FindSkinTexturePaths();
            List<string> discoveryFailures = ValidatePhysicalEnumeration(paths);
            if (discoveryFailures.Count > 0)
            {
                Debug.LogError("Player Skin Reimport aborted: " + string.Join("\n", discoveryFailures));
                return;
            }

            var failures = new List<string>();
            int alreadyCorrect = 0;
            int reimportAttempted = 0;
            int reimportSucceeded = 0;
            for (int index = 0; index < paths.Count; index++)
            {
                string failure;
                if (!HasValidPngStructure(paths[index], out failure))
                {
                    failures.Add(paths[index] + ": PNG structure failure: " + failure);
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(paths[index]) as TextureImporter;
                if (importer == null)
                {
                    failures.Add(paths[index] + ": missing TextureImporter");
                    continue;
                }

                bool importerCompliant = IsCompliant(importer, out failure);
                bool spriteValid = HasValidSpriteAsset(paths[index], out failure);
                if (importerCompliant && spriteValid)
                {
                    alreadyCorrect++;
                    continue;
                }

                reimportAttempted++;
                try
                {
                    ApplyContract(importer);
                    importer.SaveAndReimport();
                }
                catch (Exception exception)
                {
                    failures.Add(paths[index] + ": reimport exception: " + exception.Message);
                    continue;
                }

                if (!IsCompliant(importer, out failure))
                {
                    failures.Add(paths[index] + ": importer validation failure after reimport: " + failure);
                    continue;
                }

                if (!HasValidSpriteAsset(paths[index], out failure))
                {
                    failures.Add(paths[index] + ": sprite validation failure after reimport: " + failure);
                    continue;
                }

                reimportSucceeded++;
            }

            AssetDatabase.SaveAssets();
            if (failures.Count > 0)
            {
                Debug.LogError("Player Skin Reimport FAIL: TOTAL FOUND " + paths.Count
                    + "; ALREADY CORRECT " + alreadyCorrect
                    + "; REIMPORT ATTEMPTED " + reimportAttempted
                    + "; REIMPORT SUCCEEDED " + reimportSucceeded
                    + "; FAILED " + failures.Count
                    + ".\n" + string.Join("\n", failures));
                return;
            }

            Debug.Log("Player Skin Reimport PASS: TOTAL FOUND " + paths.Count
                + "; ALREADY CORRECT " + alreadyCorrect
                + "; REIMPORT ATTEMPTED " + reimportAttempted
                + "; REIMPORT SUCCEEDED " + reimportSucceeded
                + "; FAILED 0.");
        }

        [MenuItem("Tools/Cave/Verify Player Skin Imports")]
        private static void VerifyPlayerSkinImports()
        {
            List<string> paths = FindSkinTexturePaths();
            List<string> discoveryFailures = ValidatePhysicalEnumeration(paths);
            if (discoveryFailures.Count > 0)
            {
                Debug.LogError("Player Skin Imports FAIL: TOTAL PNG FILES FOUND " + paths.Count
                    + "; expected " + ExpectedFrameCount + ".\n" + string.Join("\n", discoveryFailures));
                return;
            }

            int compliant = 0;
            int structurallyValid = 0;
            int spriteAssets = 0;
            var failures = new List<string>();
            for (int index = 0; index < paths.Count; index++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(paths[index]) as TextureImporter;
                var assetFailures = new List<string>();
                string failure;
                if (HasValidPngStructure(paths[index], out failure))
                {
                    structurallyValid++;
                }
                else
                {
                    assetFailures.Add("PNG structure failure: " + failure);
                }

                bool isCompliant = importer != null && IsCompliant(importer, out failure);
                if (isCompliant)
                {
                    compliant++;
                }
                else
                {
                    assetFailures.Add(importer == null ? "missing TextureImporter" : failure);
                }

                if (HasValidSpriteAsset(paths[index], out failure))
                {
                    spriteAssets++;
                }
                else
                {
                    assetFailures.Add("sprite validation failure: " + failure);
                }

                if (assetFailures.Count > 0)
                {
                    failures.Add(paths[index] + ": " + string.Join("; ", assetFailures));
                }
            }

            if (paths.Count == ExpectedFrameCount && structurallyValid == ExpectedFrameCount
                && compliant == ExpectedFrameCount && spriteAssets == ExpectedFrameCount)
            {
                Debug.Log("PLAYER SKIN ASSET VERIFICATION PASS: Physical PNGs " + paths.Count + " / " + ExpectedFrameCount
                    + "; PNG structure " + structurallyValid + " / " + paths.Count
                    + "; Importer configuration " + compliant + " / " + paths.Count
                    + "; Sprite assets valid " + spriteAssets + " / " + paths.Count
                    + "; Failures 0.");
                return;
            }

            Debug.LogError("PLAYER SKIN ASSET VERIFICATION FAIL: Physical PNGs " + paths.Count + " / " + ExpectedFrameCount
                + "; PNG structure " + structurallyValid + " / " + paths.Count
                + "; Importer configuration " + compliant + " / " + paths.Count
                + "; Sprite assets valid " + spriteAssets + " / " + paths.Count
                + "; Failures " + failures.Count + ".\n" + string.Join("\n", failures));
        }

        private static List<string> FindSkinTexturePaths()
        {
            // Do not query AssetDatabase by importer type: malformed/default imports are
            // precisely the frames this tool must find and repair.
            string assetsPath = Application.dataPath.Replace('\\', '/');
            string absoluteSkinRoot = Path.Combine(assetsPath, "Cave/Resources/Player/Skins");
            string[] files = Directory.GetFiles(absoluteSkinRoot, "*.png", SearchOption.AllDirectories);
            var paths = new List<string>(files.Length);
            for (int index = 0; index < files.Length; index++)
            {
                string path = files[index].Replace('\\', '/');
                if (path.StartsWith(SkinRoot) && path.EndsWith(".png"))
                {
                    paths.Add(path);
                }
                else if (path.StartsWith(assetsPath + "/") && path.EndsWith(".png"))
                {
                    paths.Add("Assets" + path.Substring(assetsPath.Length));
                }
            }

            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }

        private static List<string> ValidatePhysicalEnumeration(List<string> paths)
        {
            var failures = new List<string>();
            if (paths.Count != ExpectedFrameCount)
            {
                failures.Add("TOTAL PNG FILES FOUND " + paths.Count + "; expected " + ExpectedFrameCount + ".");
            }

            for (int index = 0; index < KnownStaleFramePaths.Length; index++)
            {
                if (!paths.Contains(KnownStaleFramePaths[index]))
                {
                    failures.Add("Required stale frame was not enumerated: " + KnownStaleFramePaths[index]);
                }
            }

            return failures;
        }

        private static void ApplyContract(TextureImporter importer)
        {
            if (IsCompliant(importer, out _)) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 800f;
            importer.spritePivot = new Vector2(.5f, 0f);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            // Unity 2021 exposes single-sprite alignment through the importer
            // settings object rather than TextureImporter.spriteAlignment.
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0f);
            importer.SetTextureSettings(settings);
        }

        private static bool IsCompliant(TextureImporter importer, out string failure)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType != TextureImporterType.Sprite) { failure = "texture type is not Sprite"; return false; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { failure = "sprite mode is not Single"; return false; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 800f)) { failure = "pixels per unit is not 800"; return false; }
            if (importer.filterMode != FilterMode.Point) { failure = "filter mode is not Point"; return false; }
            if (importer.mipmapEnabled) { failure = "mipmaps are enabled"; return false; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { failure = "texture compression is not Uncompressed"; return false; }
            if (!importer.alphaIsTransparency) { failure = "alpha transparency is disabled"; return false; }
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom) { failure = "sprite alignment is not Custom"; return false; }
            if (settings.spritePivot != new Vector2(.5f, 0f)) { failure = "sprite pivot is not bottom-center"; return false; }
            failure = null;
            return true;
        }

        /// <summary>
        /// Validates the PNG container using managed bytes only. It deliberately avoids
        /// Unity's native image decoder so a 672-frame verifier run does not allocate
        /// native textures or emit Unity allocation diagnostics.
        /// </summary>
        private static bool HasValidPngStructure(string assetPath, out string failure)
        {
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                failure = "asset path is not a PNG";
                return false;
            }

            if (!File.Exists(absolutePath))
            {
                failure = "file does not exist";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(absolutePath);
            }
            catch (Exception exception)
            {
                failure = "could not read file bytes: " + exception.Message;
                return false;
            }

            if (bytes.Length == 0)
            {
                failure = "file is empty";
                return false;
            }

            if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78
                || bytes[3] != 71 || bytes[4] != 13 || bytes[5] != 10 || bytes[6] != 26 || bytes[7] != 10)
            {
                failure = "invalid PNG signature";
                return false;
            }

            bool foundHeader = false;
            bool foundImageData = false;
            bool foundEnd = false;
            int offset = 8;
            while (offset < bytes.Length)
            {
                // A PNG chunk is length + type + payload + CRC.
                if (bytes.Length - offset < 12)
                {
                    failure = "truncated PNG chunk header";
                    return false;
                }

                uint payloadLength = ReadBigEndianUInt32(bytes, offset);
                int typeOffset = offset + 4;
                long chunkEnd = (long)offset + 12L + payloadLength;
                if (chunkEnd > bytes.Length)
                {
                    failure = "truncated PNG chunk payload";
                    return false;
                }

                bool isHeader = bytes[typeOffset] == (byte)'I' && bytes[typeOffset + 1] == (byte)'H'
                    && bytes[typeOffset + 2] == (byte)'D' && bytes[typeOffset + 3] == (byte)'R';
                bool isImageData = bytes[typeOffset] == (byte)'I' && bytes[typeOffset + 1] == (byte)'D'
                    && bytes[typeOffset + 2] == (byte)'A' && bytes[typeOffset + 3] == (byte)'T';
                bool isEnd = bytes[typeOffset] == (byte)'I' && bytes[typeOffset + 1] == (byte)'E'
                    && bytes[typeOffset + 2] == (byte)'N' && bytes[typeOffset + 3] == (byte)'D';

                if (!foundHeader)
                {
                    if (!isHeader || payloadLength != 13)
                    {
                        failure = "IHDR is missing or malformed";
                        return false;
                    }

                    uint width = ReadBigEndianUInt32(bytes, offset + 8);
                    uint height = ReadBigEndianUInt32(bytes, offset + 12);
                    if (width == 0 || height == 0)
                    {
                        failure = "IHDR has non-positive dimensions";
                        return false;
                    }

                    foundHeader = true;
                }
                else if (isHeader)
                {
                    failure = "duplicate IHDR chunk";
                    return false;
                }

                if (isImageData)
                {
                    foundImageData = true;
                }

                if (isEnd)
                {
                    if (payloadLength != 0)
                    {
                        failure = "IEND payload is not empty";
                        return false;
                    }

                    foundEnd = true;
                    offset = (int)chunkEnd;
                    break;
                }

                offset = (int)chunkEnd;
            }

            if (!foundHeader) { failure = "IHDR is missing"; return false; }
            if (!foundImageData) { failure = "IDAT is missing"; return false; }
            if (!foundEnd) { failure = "IEND is missing"; return false; }
            if (offset != bytes.Length) { failure = "trailing bytes after IEND"; return false; }

            failure = null;
            return true;
        }

        private static uint ReadBigEndianUInt32(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static bool HasValidSpriteAsset(string assetPath, out string failure)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                failure = "AssetDatabase.LoadAssetAtPath<Sprite> returned null";
                return false;
            }

            failure = null;
            return true;
        }
    }
}
#endif
