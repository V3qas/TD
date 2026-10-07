using UnityEngine;

namespace TD.Menu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    internal sealed class MenuMusicPlayer : MonoBehaviour
    {
        private static MenuMusicPlayer instance;

        private AudioSource audioSource;
        private AudioClip[] playlistClips = System.Array.Empty<AudioClip>();
        private int playlistIndex = -1;
        private double earliestNextTrackDspTime;

        internal static void EnsurePlaylist(AudioClip[] clips)
        {
            AudioVolumeSettings.ApplyMasterVolume();

            AudioClip[] validClips = GetValidUniqueClips(clips);
            if (validClips.Length == 0)
                return;

            GetOrCreate().PlayPlaylist(validClips);
        }

        internal static void StartRandomLoop(AudioClip[] clips)
        {
            AudioVolumeSettings.ApplyMasterVolume();
            AudioClip[] validClips = GetValidUniqueClips(clips);
            if (validClips.Length == 0)
            {
                StopAndDestroy();
                return;
            }

            GetOrCreate().PlayLooping(validClips[Random.Range(0, validClips.Length)]);
        }

        internal static void SetVolume(float volume)
        {
            if (instance == null)
                instance = FindAnyObjectByType<MenuMusicPlayer>();

            if (instance != null)
                instance.GetAudioSource().volume = Mathf.Clamp01(volume);
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
            if (playlistClips.Length == 0)
                return;

            AudioSource source = GetAudioSource();
            if (!source.isPlaying && AudioSettings.dspTime >= earliestNextTrackDspTime)
                PlayNextPlaylistTrack();
        }

        private void PlayLooping(AudioClip clip)
        {
            AudioSource source = GetAudioSource();
            ConfigureAudioSource(source);
            playlistClips = System.Array.Empty<AudioClip>();
            playlistIndex = -1;
            source.Stop();
            source.loop = true;

            source.clip = clip;
            source.Play();
        }

        private void PlayPlaylist(AudioClip[] clips)
        {
            AudioSource source = GetAudioSource();
            ConfigureAudioSource(source);

            if (HasSamePlaylist(clips))
                return;

            playlistClips = clips;
            playlistIndex = -1;
            source.Stop();
            source.loop = false;
            PlayNextPlaylistTrack();
        }

        private bool HasSamePlaylist(AudioClip[] clips)
        {
            if (playlistClips.Length != clips.Length)
                return false;

            for (int index = 0; index < clips.Length; index++)
            {
                if (playlistClips[index] != clips[index])
                    return false;
            }

            return true;
        }

        private void PlayNextPlaylistTrack()
        {
            playlistIndex = (playlistIndex + 1) % playlistClips.Length;
            AudioSource source = GetAudioSource();
            source.clip = playlistClips[playlistIndex];
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
