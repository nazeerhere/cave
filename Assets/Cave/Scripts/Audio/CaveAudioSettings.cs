using UnityEngine;

namespace Cave.Audio
{
    public static class CaveAudioSettings
    {
        public const string MasterVolumePlayerPrefsKey = "Cave.Settings.Audio.v1.Master";
        public const string SfxVolumePlayerPrefsKey = "Cave.Settings.Audio.v1.SFX";

        private const float DefaultVolume = 1f;

        private static float masterVolume = DefaultVolume;
        private static float sfxVolume = DefaultVolume;

        public static float MasterVolume => masterVolume;
        public static float SfxVolume => sfxVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadForPlaySession()
        {
            masterVolume = LoadVolume(MasterVolumePlayerPrefsKey);
            sfxVolume = LoadVolume(SfxVolumePlayerPrefsKey);
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

        public static void ApplyCurrentVolumes()
        {
            // All sounds currently in the project are SFX. Multiplying at the listener
            // keeps manually configured AudioSources untouched until mixer routing is approved.
            AudioListener.volume = masterVolume * sfxVolume;
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
