using System;
using System.Collections.Generic;
using System.IO;
using Cave.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor.VisualQA
{
    /// <summary>
    /// Explicit, editor-only room evidence capture. It uses temporary hidden render objects
    /// and never creates a runtime dependency or modifies the saved scene.
    /// </summary>
    public static class CaveVisualQaCapture
    {
        public const string OutputRoot = "Assets/Cave/VisualQA";
        private const int OverviewWidth = 2048;
        private const int OverviewHeight = 1152;
        private const int GameViewWidth = 1280;
        private const int GameViewHeight = 720;

        public static string CaptureAll(Scene scene)
        {
            EnsureCaptureScene(scene);
            string outputDirectory = GetOutputDirectory(scene);
            Directory.CreateDirectory(ToAbsolutePath(outputDirectory));

            Bounds overviewBounds = CalculateOverviewBounds(scene);
            Camera gameplayCamera = FindGameplayCamera(scene);
            CaptureRoomOverview(scene, overviewBounds, outputDirectory, false);
            CaptureRoomOverview(scene, overviewBounds, outputDirectory, true);
            if (gameplayCamera != null)
            {
                CaptureGameView(gameplayCamera, outputDirectory);
            }
            else
            {
                Debug.LogWarning("Cave Visual QA could not find an enabled gameplay camera; GameView.png was not exported.");
            }

            ExportManifest(scene, overviewBounds, gameplayCamera, outputDirectory);
            AssetDatabase.Refresh();
            Debug.Log("Cave Visual QA exported " + outputDirectory + " for " + scene.name + ".");
            return outputDirectory;
        }

        public static string CaptureRoomOverview(Scene scene)
        {
            EnsureCaptureScene(scene);
            string outputDirectory = GetOutputDirectory(scene);
            Directory.CreateDirectory(ToAbsolutePath(outputDirectory));
            CaptureRoomOverview(scene, CalculateOverviewBounds(scene), outputDirectory, false);
            AssetDatabase.Refresh();
            return outputDirectory;
        }

        public static string CaptureGameView(Scene scene)
        {
            EnsureCaptureScene(scene);
            Camera gameplayCamera = FindGameplayCamera(scene);
            if (gameplayCamera == null)
            {
                throw new InvalidOperationException("Cave Visual QA could not find an enabled gameplay camera in " + scene.name + ".");
            }

            string outputDirectory = GetOutputDirectory(scene);
            Directory.CreateDirectory(ToAbsolutePath(outputDirectory));
            CaptureGameView(gameplayCamera, outputDirectory);
            AssetDatabase.Refresh();
            return outputDirectory;
        }

        public static string ExportManifest(Scene scene)
        {
            EnsureCaptureScene(scene);
            string outputDirectory = GetOutputDirectory(scene);
            Directory.CreateDirectory(ToAbsolutePath(outputDirectory));
            ExportManifest(scene, CalculateOverviewBounds(scene), FindGameplayCamera(scene), outputDirectory);
            AssetDatabase.Refresh();
            return outputDirectory;
        }

        /// <summary>Optional Scene Builder integration point; callers opt in after saving their room.</summary>
        public static string CaptureVisualQa(Scene scene)
        {
            return CaptureAll(scene);
        }

        /// <summary>
        /// Batch-mode support: pass -visualQaScene Assets/Cave/Scenes/YourRoom.unity.
        /// It exists for CI/editor validation and does not run in a player build.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            string scenePath = GetCommandLineArgument("-visualQaScene");
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Visual QA batch capture requires -visualQaScene <Assets/.../Room.unity>.");
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            CaptureAll(scene);
        }

        public static Bounds CalculateOverviewBounds(Scene scene)
        {
            EnsureCaptureScene(scene);
            bool hasBounds = false;
            Bounds bounds = new Bounds();

            // Authored camera bounds describe the intended playable composition and take precedence.
            RoomSceneMarker[] markers = GetSceneComponents<RoomSceneMarker>(scene);
            for (int index = 0; index < markers.Length; index++)
            {
                RoomSceneMarker marker = markers[index];
                if (marker.Kind != RoomSceneMarker.MarkerKind.CameraBounds || marker.BoundsSize.x <= 0f || marker.BoundsSize.y <= 0f)
                {
                    continue;
                }

                Encapsulate(ref bounds, ref hasBounds, new Bounds(marker.transform.position, new Vector3(marker.BoundsSize.x, marker.BoundsSize.y, 1f)));
            }

            if (!hasBounds)
            {
                // Fallback is environment/gameplay geometry, excluding triggers so kill volumes do not dwarf a room.
                foreach (SpriteRenderer renderer in GetSceneComponents<SpriteRenderer>(scene))
                {
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy && !IsTemporary(renderer.gameObject))
                    {
                        Encapsulate(ref bounds, ref hasBounds, renderer.bounds);
                    }
                }

                foreach (Collider2D collider in GetSceneComponents<Collider2D>(scene))
                {
                    if (collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy && !IsTemporary(collider.gameObject))
                    {
                        Encapsulate(ref bounds, ref hasBounds, collider.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));
            }

            float padding = Mathf.Max(2f, Mathf.Max(bounds.size.x, bounds.size.y) * 0.08f);
            bounds.Expand(new Vector3(padding * 2f, padding * 2f, 1f));
            return bounds;
        }

        private static void CaptureRoomOverview(Scene scene, Bounds bounds, string outputDirectory, bool withOverlay)
        {
            Camera camera = null;
            Texture2D capture = null;
            try
            {
                camera = CreateTemporaryOverviewCamera(scene, bounds, OverviewWidth, OverviewHeight);
                capture = RenderCamera(camera, OverviewWidth, OverviewHeight);
                if (withOverlay)
                {
                    DrawQaOverlay(capture, camera, scene);
                }

                string filename = withOverlay ? "RoomOverview_QA.png" : "RoomOverview.png";
                WritePng(capture, CombineAssetPath(outputDirectory, filename));
            }
            finally
            {
                DestroyTemporary(capture);
                DestroyTemporary(camera != null ? camera.gameObject : null);
            }
        }

        private static void CaptureGameView(Camera gameplayCamera, string outputDirectory)
        {
            Texture2D capture = null;
            try
            {
                capture = RenderCamera(gameplayCamera, GameViewWidth, GameViewHeight);
                WritePng(capture, CombineAssetPath(outputDirectory, "GameView.png"));
            }
            finally
            {
                DestroyTemporary(capture);
            }
        }

        private static void ExportManifest(Scene scene, Bounds overviewBounds, Camera gameplayCamera, string outputDirectory)
        {
            CaveVisualQaSceneManifest manifest = CaveVisualQaManifestExporter.Create(scene, overviewBounds, gameplayCamera);
            string json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(ToAbsolutePath(CombineAssetPath(outputDirectory, "SceneManifest.json")), json);
        }

        private static Camera CreateTemporaryOverviewCamera(Scene scene, Bounds bounds, int width, int height)
        {
            GameObject cameraObject = new GameObject("Cave Visual QA Temporary Overview Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.016f, 0.04f, 1f);
            camera.cullingMask = ~0;
            camera.nearClipPlane = -50f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -20f);
            float aspect = (float)width / height;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect);
            return camera;
        }

        private static Texture2D RenderCamera(Camera camera, int width, int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                result.Apply(false, false);
                return result;
            }
            catch
            {
                DestroyTemporary(result);
                throw;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void DrawQaOverlay(Texture2D image, Camera camera, Scene scene)
        {
            foreach (RoomSceneMarker marker in GetSceneComponents<RoomSceneMarker>(scene))
            {
                Color color = GetMarkerColor(marker.Kind);
                Vector2 point = WorldToPixel(camera, image, marker.transform.position);
                DrawCross(image, Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), 8, color);
                if (marker.Kind == RoomSceneMarker.MarkerKind.CameraBounds)
                {
                    DrawWorldRect(image, camera, marker.transform.position, marker.BoundsSize, color);
                }
            }

            foreach (Collider2D collider in GetSceneComponents<Collider2D>(scene))
            {
                if (collider.isTrigger && collider.GetComponent<DeathBoundary>() != null)
                {
                    DrawBoundsRect(image, camera, collider.bounds, new Color(1f, 0.18f, 0.18f, 1f));
                }
            }
        }

        private static Color GetMarkerColor(RoomSceneMarker.MarkerKind kind)
        {
            switch (kind)
            {
                case RoomSceneMarker.MarkerKind.PlayerSpawn: return new Color(0.25f, 0.95f, 1f, 1f);
                case RoomSceneMarker.MarkerKind.EnemySpawn: return new Color(1f, 0.22f, 0.22f, 1f);
                case RoomSceneMarker.MarkerKind.Transition: return new Color(1f, 0.82f, 0.15f, 1f);
                case RoomSceneMarker.MarkerKind.Interactable: return new Color(0.48f, 1f, 0.4f, 1f);
                case RoomSceneMarker.MarkerKind.CameraBounds: return new Color(0.84f, 0.48f, 1f, 1f);
                default: return Color.white;
            }
        }

        private static void DrawWorldRect(Texture2D image, Camera camera, Vector2 center, Vector2 size, Color color)
        {
            DrawBoundsRect(image, camera, new Bounds(center, new Vector3(size.x, size.y, 0f)), color);
        }

        private static void DrawBoundsRect(Texture2D image, Camera camera, Bounds bounds, Color color)
        {
            Vector2 min = WorldToPixel(camera, image, bounds.min);
            Vector2 max = WorldToPixel(camera, image, bounds.max);
            DrawRect(image, Mathf.RoundToInt(min.x), Mathf.RoundToInt(min.y), Mathf.RoundToInt(max.x), Mathf.RoundToInt(max.y), color);
        }

        private static Vector2 WorldToPixel(Camera camera, Texture2D image, Vector3 worldPosition)
        {
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            return new Vector2(viewport.x * (image.width - 1), viewport.y * (image.height - 1));
        }

        private static void DrawCross(Texture2D image, int x, int y, int radius, Color color)
        {
            for (int offset = -radius; offset <= radius; offset++)
            {
                SetPixelSafe(image, x + offset, y, color);
                SetPixelSafe(image, x, y + offset, color);
            }
        }

        private static void DrawRect(Texture2D image, int xMin, int yMin, int xMax, int yMax, Color color)
        {
            int left = Mathf.Min(xMin, xMax);
            int right = Mathf.Max(xMin, xMax);
            int bottom = Mathf.Min(yMin, yMax);
            int top = Mathf.Max(yMin, yMax);
            for (int x = left; x <= right; x++)
            {
                SetPixelSafe(image, x, bottom, color);
                SetPixelSafe(image, x, top, color);
            }

            for (int y = bottom; y <= top; y++)
            {
                SetPixelSafe(image, left, y, color);
                SetPixelSafe(image, right, y, color);
            }
        }

        private static void SetPixelSafe(Texture2D image, int x, int y, Color color)
        {
            if (x >= 0 && x < image.width && y >= 0 && y < image.height)
            {
                image.SetPixel(x, y, color);
            }
        }

        private static Camera FindGameplayCamera(Scene scene)
        {
            Camera fallback = null;
            foreach (Camera camera in GetSceneComponents<Camera>(scene))
            {
                if (!camera.enabled || !camera.gameObject.activeInHierarchy || IsTemporary(camera.gameObject))
                {
                    continue;
                }

                if (camera.CompareTag("MainCamera"))
                {
                    return camera;
                }

                if (fallback == null)
                {
                    fallback = camera;
                }
            }

            return fallback;
        }

        private static T[] GetSceneComponents<T>(Scene scene) where T : Component
        {
            List<T> components = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        private static void Encapsulate(ref Bounds accumulated, ref bool hasBounds, Bounds candidate)
        {
            if (!hasBounds)
            {
                accumulated = candidate;
                hasBounds = true;
            }
            else
            {
                accumulated.Encapsulate(candidate);
            }
        }

        private static string GetOutputDirectory(Scene scene)
        {
            return CombineAssetPath(OutputRoot, SanitizePathSegment(scene.name));
        }

        private static string CombineAssetPath(string left, string right)
        {
            return left.TrimEnd('/') + "/" + right.TrimStart('/');
        }

        private static string ToAbsolutePath(string assetPath)
        {
            if (!assetPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                throw new ArgumentException("Visual QA output path must start with Assets: " + assetPath);
            }

            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        }

        private static string SanitizePathSegment(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int index = 0; index < invalid.Length; index++)
            {
                value = value.Replace(invalid[index], '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "UntitledScene" : value;
        }

        private static string GetCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }

        private static bool IsTemporary(GameObject gameObject)
        {
            return (gameObject.hideFlags & HideFlags.DontSave) != 0;
        }

        private static void DestroyTemporary(UnityEngine.Object objectToDestroy)
        {
            if (objectToDestroy != null)
            {
                UnityEngine.Object.DestroyImmediate(objectToDestroy);
            }
        }

        private static void WritePng(Texture2D image, string assetPath)
        {
            File.WriteAllBytes(ToAbsolutePath(assetPath), image.EncodeToPNG());
        }

        private static void EnsureCaptureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("Cave Visual QA requires a loaded scene.");
            }
        }
    }
}
