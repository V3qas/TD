using NUnit.Framework;
using TD.Menu;
using UnityEngine;

namespace TD.Tests.EditMode
{
    public class AudioVolumeSettingsTests
    {
        private bool hadMasterVolume;
        private bool hadMusicVolume;
        private float storedMasterVolume;
        private float storedMusicVolume;
        private float initialListenerVolume;

        [SetUp]
        public void SetUp()
        {
            hadMasterVolume = PlayerPrefs.HasKey(AudioVolumeSettings.MasterVolumePlayerPrefsKey);
            hadMusicVolume = PlayerPrefs.HasKey(AudioVolumeSettings.MusicVolumePlayerPrefsKey);
            storedMasterVolume = PlayerPrefs.GetFloat(AudioVolumeSettings.MasterVolumePlayerPrefsKey, 1f);
            storedMusicVolume = PlayerPrefs.GetFloat(AudioVolumeSettings.MusicVolumePlayerPrefsKey, 1f);
            initialListenerVolume = AudioListener.volume;
        }

        [TearDown]
        public void TearDown()
        {
            RestorePreference(
                AudioVolumeSettings.MasterVolumePlayerPrefsKey,
                hadMasterVolume,
                storedMasterVolume);
            RestorePreference(
                AudioVolumeSettings.MusicVolumePlayerPrefsKey,
                hadMusicVolume,
                storedMusicVolume);
            PlayerPrefs.Save();
            AudioListener.volume = initialListenerVolume;
        }

        [Test]
        public void SetMasterVolume_PersistsAndAppliesClampedValue()
        {
            AudioVolumeSettings.SetMasterVolume(1.5f);

            Assert.That(AudioVolumeSettings.MasterVolume, Is.EqualTo(1f));
            Assert.That(AudioListener.volume, Is.EqualTo(1f));

            AudioVolumeSettings.SetMasterVolume(0.37f);

            Assert.That(AudioVolumeSettings.MasterVolume, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f));
        }

        [Test]
        public void SetMusicVolume_PersistsClampedValue()
        {
            AudioVolumeSettings.SetMusicVolume(-0.5f);
            Assert.That(AudioVolumeSettings.MusicVolume, Is.EqualTo(0f));

            AudioVolumeSettings.SetMusicVolume(0.64f);
            Assert.That(AudioVolumeSettings.MusicVolume, Is.EqualTo(0.64f).Within(0.001f));
        }

        private static void RestorePreference(string key, bool hadValue, float value)
        {
            if (hadValue)
                PlayerPrefs.SetFloat(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        }
    }
}
