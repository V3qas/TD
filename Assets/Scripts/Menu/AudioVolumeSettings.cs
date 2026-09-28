using UnityEngine;

namespace TD.Menu
{
    internal static class AudioVolumeSettings
    {
        internal const string MasterVolumePlayerPrefsKey = "TD.MasterVolume";
        internal const string MusicVolumePlayerPrefsKey = "TD.MusicVolume";

        private const float DefaultVolume = 1f;

        internal static float MasterVolume => GetStoredVolume(MasterVolumePlayerPrefsKey);
        internal static float MusicVolume => GetStoredVolume(MusicVolumePlayerPrefsKey);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStartup()
        {
            ApplyMasterVolume();
        }

        internal static void SetMasterVolume(float volume, bool saveImmediately = true)
        {
            float sanitizedVolume = Sanitize(volume);
            PlayerPrefs.SetFloat(MasterVolumePlayerPrefsKey, sanitizedVolume);
            if (saveImmediately)
                PlayerPrefs.Save();
            AudioListener.volume = sanitizedVolume;
        }

        internal static void SetMusicVolume(float volume, bool saveImmediately = true)
        {
            float sanitizedVolume = Sanitize(volume);
            PlayerPrefs.SetFloat(MusicVolumePlayerPrefsKey, sanitizedVolume);
            if (saveImmediately)
                PlayerPrefs.Save();
            MenuMusicPlayer.SetVolume(sanitizedVolume);
        }

        internal static void Save()
        {
            PlayerPrefs.Save();
        }

        internal static void ApplyMasterVolume()
        {
            AudioListener.volume = MasterVolume;
        }

        private static float GetStoredVolume(string key)
        {
            return Sanitize(PlayerPrefs.GetFloat(key, DefaultVolume));
        }

        private static float Sanitize(float volume)
        {
            return float.IsNaN(volume) || float.IsInfinity(volume)
                ? DefaultVolume
                : Mathf.Clamp01(volume);
        }
    }
}
