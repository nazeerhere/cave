using UnityEngine;

namespace Cave.CameraSystem
{
    /// <summary>
    /// Player preference for gameplay framing. The scale is relative to each
    /// camera's authored orthographic size, so scene-specific framing remains the
    /// baseline and one saved preference can safely apply across scenes.
    /// </summary>
    public static class CameraZoomSettings
    {
        public const string ZoomScalePlayerPrefsKey = "Cave.Settings.Camera.v1.ZoomScale";
        public const float MinimumZoomScale = 0.85f;
        public const float MaximumZoomScale = 1.15f;
        public const float DefaultZoomScale = 1f;

        private static float zoomScale = DefaultZoomScale;

        public static float ZoomScale => zoomScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadForPlaySession()
        {
            zoomScale = Mathf.Clamp(
                PlayerPrefs.GetFloat(ZoomScalePlayerPrefsKey, DefaultZoomScale),
                MinimumZoomScale,
                MaximumZoomScale);
        }

        public static void SetZoomScale(float value)
        {
            zoomScale = Mathf.Clamp(value, MinimumZoomScale, MaximumZoomScale);
            PlayerPrefs.SetFloat(ZoomScalePlayerPrefsKey, zoomScale);
            PlayerPrefs.Save();

            CameraFollow[] gameplayCameras = Object.FindObjectsOfType<CameraFollow>(true);
            foreach (CameraFollow gameplayCamera in gameplayCameras)
            {
                gameplayCamera.ApplySavedZoom();
            }
        }
    }
}
