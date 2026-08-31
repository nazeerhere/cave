using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Audio
{
    public static class CaveSfx
    {
        private const string LibraryResourceName = "CaveSfxLibrary";

        private static CaveSfxLibrary library;
        private static AudioSource source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            library = null;
            source = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureSource();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInitialListener()
        {
            EnsureSingleListener();
        }

        public static void Play(CaveSfxCue cue, float volumeScale = 1f)
        {
            EnsureSource();
            AudioClip clip = library != null ? library.GetClip(cue) : null;
            PlayClip(clip, volumeScale);
        }

        // Keeps optional Inspector-assigned clips on the same persistent SFX
        // source as the existing cue library, without creating transient sources.
        public static void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            EnsureSource();
            if (source == null || clip == null)
            {
                return;
            }

            source.PlayOneShot(
                clip,
                Mathf.Clamp01(volumeScale) * CaveAudioSettings.SfxVolume);
        }

        public static void PlayUi(CaveSfxCue cue, float volumeScale = 1f)
        {
            EnsureSource();
            AudioClip clip = library != null ? library.GetClip(cue) : null;
            if (source == null || clip == null)
            {
                return;
            }

            source.PlayOneShot(
                clip,
                Mathf.Clamp01(volumeScale) * CaveAudioSettings.UiVolume);
        }

        private static void EnsureSource()
        {
            if (source != null)
            {
                return;
            }

            library = Resources.Load<CaveSfxLibrary>(LibraryResourceName);
            GameObject audioObject = new GameObject("[Cave] SFX");
            Object.DontDestroyOnLoad(audioObject);
            source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = 128;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureSingleListener();
        }

        private static void EnsureSingleListener()
        {
            AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>();
            if (listeners.Length == 1)
            {
                return;
            }

            if (listeners.Length > 1)
            {
                Debug.LogWarning(
                    "Multiple AudioListeners are active/configured. Cave preserved them for manual review.");
                return;
            }

            Camera targetCamera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (targetCamera != null)
            {
                targetCamera.gameObject.AddComponent<AudioListener>();
            }
        }
    }
}
