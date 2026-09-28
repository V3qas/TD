using UnityEngine;

namespace TD.Menu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    internal sealed class MenuMusicPlayer : MonoBehaviour
    {
        private static MenuMusicPlayer instance;

        private AudioSource audioSource;
        private AudioClip[] rotationClips = System.Array.Empty<AudioClip>();
        private int previousRotationIndex = -1;
        private bool rotationActive;
        private double earliestNextTrackDspTime;

        internal static void EnsurePlaying(AudioClip clip)
        {
            AudioVolumeSettings.ApplyMasterVolume();

            if (clip == null)
                return;

            GetOrCreate().PlayLooping(clip);
        }

        internal static void StartRandomRotation(AudioClip[] clips)
        {
            AudioVolumeSettings.ApplyMasterVolume();
            GetOrCreate().StartRotation(clips);
        }

        internal static void SetVolume(float volume)
        {
            if (instance == null)
                instance = FindAnyObjectByType<MenuMusicPlayer>();

            if (instance != null)
                instance.GetAudioSource().volume = Mathf.Clamp01(volume);
        }

        internal static int ChooseNextRotationIndex(int clipCount, int previousIndex, int randomValue)
        {
            if (clipCount <= 0)
                return -1;

            if (clipCount == 1)
                return 0;

            if (previousIndex < 0 || previousIndex >= clipCount)
                return Mathf.Clamp(randomValue, 0, clipCount - 1);

            int candidate = Mathf.Clamp(randomValue, 0, clipCount - 2);
            return candidate >= previousIndex ? candidate + 1 : candidate;
        }

        internal static void StopAndDestroy()
        {
            if (instance == null)
                instance = FindAnyObjectByType<MenuMusicPlayer>();

            if (instance == null)
                return;

            instance.GetAudioSource().Stop();
            GameObject playerObject = instance.gameObject;
            instance = null;

            if (Application.isPlaying)
                Destroy(playerObject);
            else
                DestroyImmediate(playerObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureAudioSource(GetAudioSource());
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (!rotationActive || rotationClips.Length == 0)
                return;

            AudioSource source = GetAudioSource();
            if (!source.isPlaying && AudioSettings.dspTime >= earliestNextTrackDspTime)
                PlayNextRotationTrack();
        }

        private void PlayLooping(AudioClip clip)
        {
            AudioSource source = GetAudioSource();
            ConfigureAudioSource(source);
            rotationActive = false;
            rotationClips = System.Array.Empty<AudioClip>();
            previousRotationIndex = -1;
            source.loop = true;

            if (source.clip == clip && source.isPlaying)
                return;

            source.clip = clip;
            source.Play();
        }

        private void StartRotation(AudioClip[] clips)
        {
            rotationClips = GetValidUniqueClips(clips);
            previousRotationIndex = -1;
            rotationActive = rotationClips.Length > 0;

            AudioSource source = GetAudioSource();
            ConfigureAudioSource(source);
            source.Stop();
            source.loop = false;

            if (rotationActive)
                PlayNextRotationTrack();
        }

        private void PlayNextRotationTrack()
        {
            int randomValue;
            if (rotationClips.Length == 1)
            {
                randomValue = 0;
            }
            else
            {
                randomValue = previousRotationIndex >= 0 && previousRotationIndex < rotationClips.Length
                    ? Random.Range(0, rotationClips.Length - 1)
                    : Random.Range(0, rotationClips.Length);
            }

            int nextIndex = ChooseNextRotationIndex(rotationClips.Length, previousRotationIndex, randomValue);
            if (nextIndex < 0)
                return;

            previousRotationIndex = nextIndex;
            AudioSource source = GetAudioSource();
            source.clip = rotationClips[nextIndex];
            source.Play();

            // Prevent a clip that is still loading from being mistaken for a finished track.
            earliestNextTrackDspTime = AudioSettings.dspTime + 0.25d;
        }

        private static AudioClip[] GetValidUniqueClips(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return System.Array.Empty<AudioClip>();

            System.Collections.Generic.List<AudioClip> validClips = new System.Collections.Generic.List<AudioClip>();
            for (int index = 0; index < clips.Length; index++)
            {
                AudioClip clip = clips[index];
                if (clip != null && !validClips.Contains(clip))
                    validClips.Add(clip);
            }

            return validClips.ToArray();
        }

        private static MenuMusicPlayer GetOrCreate()
        {
            if (instance == null)
                instance = FindAnyObjectByType<MenuMusicPlayer>();

            if (instance == null)
            {
                GameObject playerObject = new GameObject("MenuMusicPlayer");
                instance = playerObject.AddComponent<MenuMusicPlayer>();
            }

            return instance;
        }

        private AudioSource GetAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
            }

            return audioSource;
        }

        private static void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = AudioVolumeSettings.MusicVolume;
        }
    }
}
