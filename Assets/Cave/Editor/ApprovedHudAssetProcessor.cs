#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cave.Editor
{
    /// <summary>Builds reusable HUD sprites from the three approved GameHud design sheets.</summary>
    public sealed class ApprovedHudAssetProcessor : AssetPostprocessor
    {
        private const string SourceRoot = "Assets/Cave/Art/UI/HUD/Source/Approved/";
        private const string RuntimeRoot = "Assets/Cave/Resources/UI/HUD/Approved/";
        private const string ResourceSource = SourceRoot + "ResourceHudApproved.png";
        private const string SkillSource = SourceRoot + "SkillPathShopApproved.png";
        private const string WorldSource = SourceRoot + "WorldLevelApproved.png";
        private static bool rebuildQueued;

        [InitializeOnLoadMethod]
        private static void QueueValidation()
        {
            EditorApplication.delayCall += Rebuild;
        }

        [MenuItem("Cave/UI/Rebuild Approved HUD Art")]
        private static void RebuildFromMenu()
        {
            Rebuild();
        }

        private void OnPreprocessTexture()
        {
            TextureImporter importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(SourceRoot))
            {
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                return;
            }

            if (!assetPath.StartsWith(RuntimeRoot)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = BorderFor(Path.GetFileNameWithoutExtension(assetPath));
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (rebuildQueued) return;
            for (int index = 0; index < importedAssets.Length; index++)
            {
                if (importedAssets[index] == ResourceSource
                    || importedAssets[index] == SkillSource
                    || importedAssets[index] == WorldSource)
                {
                    rebuildQueued = true;
                    EditorApplication.delayCall += Rebuild;
                    return;
                }
            }
        }

        private static void Rebuild()
        {
            rebuildQueued = false;
            Texture2D resource = AssetDatabase.LoadAssetAtPath<Texture2D>(ResourceSource);
            Texture2D skill = AssetDatabase.LoadAssetAtPath<Texture2D>(SkillSource);
            Texture2D world = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldSource);
            if (resource == null || skill == null || world == null) return;

            Directory.CreateDirectory(RuntimeRoot);
            // Latest approved source is 1774x887 with authored transparency.
            // These are complete authored HUD rows, measured from the top-left of
            // that source.  The earlier partial crops cut through the gryphon and
            // the left-hand frame assembly, then the runtime attempted to cover
            // the missing art with a second cropped gryphon Image.
            WriteCrop(resource, new RectInt(0, 38, resource.width, 406), "ResourceHudHealthFrame", false, MatteKind.None);
            WriteCrop(resource, new RectInt(0, 447, resource.width, 153), "ResourceHudShortFrame", false, MatteKind.None);

            WriteCrop(skill, new RectInt(270, 32, 240, 98), "SkillPathTabActive", true, MatteKind.Dark);
            WriteCrop(skill, new RectInt(14, 32, 240, 98), "SkillPathTabInactive", true, MatteKind.Dark);
            WriteCrop(skill, new RectInt(1036, 32, 248, 98), "ShopTabActive", true, MatteKind.Dark);
            WriteCrop(skill, new RectInt(780, 32, 242, 98), "ShopTabInactive", true, MatteKind.Dark);
            WriteCrop(skill, new RectInt(14, 186, 246, 320), "SkillPathPanelFrame", true, MatteKind.Dark);
            WriteCrop(skill, new RectInt(780, 186, 246, 320), "ShopPanelFrame", true, MatteKind.Dark);
            WriteCrop(skill, new RectInt(14, 526, 246, 100), "HudCardRow", true, MatteKind.Dark);
            WriteCrop(skill, new RectInt(14, 788, 360, 52), "HudSectionDivider", true, MatteKind.Dark);

            WriteCrop(world, new RectInt(0, 0, world.width, world.height), "WorldLevelFrame", false, MatteKind.None);
            AssetDatabase.Refresh();
        }

        private static void WriteCrop(
            Texture2D source,
            RectInt topLeftRect,
            string outputName,
            bool clearConnectedMatte,
            MatteKind matteKind)
        {
            if (topLeftRect.x < 0 || topLeftRect.y < 0 || topLeftRect.width <= 0 || topLeftRect.height <= 0
                || topLeftRect.xMax > source.width || topLeftRect.yMax > source.height)
            {
                Debug.LogError("Approved HUD crop does not fit source: " + outputName);
                return;
            }

            int unityY = source.height - topLeftRect.y - topLeftRect.height;
            Color32[] pixels = source.GetPixels32();
            Color32[] crop = new Color32[topLeftRect.width * topLeftRect.height];
            for (int row = 0; row < topLeftRect.height; row++)
            {
                int sourceOffset = (unityY + row) * source.width + topLeftRect.x;
                System.Array.Copy(pixels, sourceOffset, crop, row * topLeftRect.width, topLeftRect.width);
            }

            if (clearConnectedMatte) ClearEdgeConnectedMatte(crop, topLeftRect.width, topLeftRect.height, matteKind);
            Texture2D output = new Texture2D(topLeftRect.width, topLeftRect.height, TextureFormat.RGBA32, false);
            output.SetPixels32(crop);
            output.Apply(false, false);
            File.WriteAllBytes(RuntimeRoot + outputName + ".png", output.EncodeToPNG());
            Object.DestroyImmediate(output);
        }

        private static void ClearEdgeConnectedMatte(Color32[] pixels, int width, int height, MatteKind kind)
        {
            bool[] visited = new bool[pixels.Length];
            Queue<int> queue = new Queue<int>();
            for (int x = 0; x < width; x++)
            {
                Enqueue(x, pixels, visited, queue, kind);
                Enqueue((height - 1) * width + x, pixels, visited, queue, kind);
            }
            for (int y = 1; y < height - 1; y++)
            {
                Enqueue(y * width, pixels, visited, queue, kind);
                Enqueue(y * width + width - 1, pixels, visited, queue, kind);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;
                pixels[index].a = 0;
                if (x > 0) Enqueue(index - 1, pixels, visited, queue, kind);
                if (x + 1 < width) Enqueue(index + 1, pixels, visited, queue, kind);
                if (y > 0) Enqueue(index - width, pixels, visited, queue, kind);
                if (y + 1 < height) Enqueue(index + width, pixels, visited, queue, kind);
            }
        }

        private static void Enqueue(int index, Color32[] pixels, bool[] visited, Queue<int> queue, MatteKind kind)
        {
            if (visited[index]) return;
            visited[index] = true;
            if (IsMatte(pixels[index], kind)) queue.Enqueue(index);
        }

        private static bool IsMatte(Color32 pixel, MatteKind kind)
        {
            if (pixel.a < 8) return true;
            byte maximum = (byte)Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            byte minimum = (byte)Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            if (kind == MatteKind.Dark) return maximum < 42 && maximum - minimum < 18;
            return maximum < 72 && maximum - minimum < 18;
        }

        private static Vector4 BorderFor(string name)
        {
            if (name.EndsWith("TabActive") || name.EndsWith("TabInactive")) return new Vector4(28, 20, 28, 20);
            if (name.EndsWith("PanelFrame")) return new Vector4(30, 34, 30, 34);
            if (name == "HudCardRow") return new Vector4(24, 24, 24, 24);
            return Vector4.zero;
        }

        private enum MatteKind { None, Neutral, Dark }
    }
}
#endif
