using UnityEngine;

namespace Cave.Audio
{
    public static class CaveAudioSettings
    {
        public const string MasterVolumePlayerPrefsKey = "Cave.Settings.Audio.v1.Master";
        public const string SfxVolumePlayerPrefsKey = "Cave.Settings.Audio.v1.SFX";
        public const string MusicVolumePlayerPrefsKey = "Cave.Settings.Audio.v1.Music";
        public const string UiVolumePlayerPrefsKey = "Cave.Settings.Audio.v1.UI";
        public const string AmbienceVolumePlayerPrefsKey = "Cave.Settings.Audio.v1.Ambience";

        private const float DefaultVolume = 1f;

        private static float masterVolume = DefaultVolume;
        private static float sfxVolume = DefaultVolume;
        private static float musicVolume = DefaultVolume;
        private static float uiVolume = DefaultVolume;
        private static float ambienceVolume = DefaultVolume;

        public static float MasterVolume => masterVolume;
        public static float SfxVolume => sfxVolume;
        public static float MusicVolume => musicVolume;
        public static float UiVolume => uiVolume;
        public static float AmbienceVolume => ambienceVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadForPlaySession()
        {
            masterVolume = LoadVolume(MasterVolumePlayerPrefsKey);
            sfxVolume = LoadVolume(SfxVolumePlayerPrefsKey);
            musicVolume = LoadVolume(MusicVolumePlayerPrefsKey);
            uiVolume = LoadVolume(UiVolumePlayerPrefsKey);
            ambienceVolume = LoadVolume(AmbienceVolumePlayerPrefsKey);
            ApplyCurrentVolumes();
        }

        public static void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            SaveVolume(MasterVolumePlayerPrefsKey, masterVolume);
            ApplyCurrentVolumes();
        }

        public static void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            SaveVolume(SfxVolumePlayerPrefsKey, sfxVolume);
            ApplyCurrentVolumes();
        }

        public static void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            SaveVolume(MusicVolumePlayerPrefsKey, musicVolume);
            ApplyCurrentVolumes();
        }

        public static void SetUiVolume(float value)
        {
            uiVolume = Mathf.Clamp01(value);
            SaveVolume(UiVolumePlayerPrefsKey, uiVolume);
            ApplyCurrentVolumes();
        }

        public static void SetAmbienceVolume(float value)
        {
            ambienceVolume = Mathf.Clamp01(value);
            SaveVolume(AmbienceVolumePlayerPrefsKey, ambienceVolume);
            ApplyCurrentVolumes();
        }

        public static void ApplyCurrentVolumes()
        {
            // Per-route sources apply their own channel gain. The listener remains
            // Master only, so manually authored AudioSources are not reconfigured.
            AudioListener.volume = masterVolume;
        }

        private static float LoadVolume(string key)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, DefaultVolume));
        }

        private static void SaveVolume(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}
