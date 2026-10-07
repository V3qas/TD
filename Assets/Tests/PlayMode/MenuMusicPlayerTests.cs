using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TD.Menu;
using UnityEngine;
using UnityEngine.TestTools;

namespace TD.Tests.PlayMode
{
    public class MenuMusicPlayerTests
    {
        private readonly List<AudioClip> createdClips = new List<AudioClip>();
        private Random.State randomState;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            randomState = Random.state;
            MenuMusicPlayer.StopAndDestroy();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MenuMusicPlayer.StopAndDestroy();
            foreach (AudioClip clip in createdClips)
                Object.Destroy(clip);
            createdClips.Clear();
            Random.state = randomState;
            yield return null;
        }

        [Test]
        public void TitlePlaylist_StartsWithTitleOne_ThenAlternatesForever()
        {
            AudioClip first = CreateClip("Title1");
            AudioClip second = CreateClip("Title2");
            MenuMusicPlayer.EnsurePlaylist(new[] { first, second });
            MenuMusicPlayer player = Object.FindAnyObjectByType<MenuMusicPlayer>();
            AudioSource source = player.GetComponent<AudioSource>();
            Assert.That(source.clip, Is.SameAs(first));
            Assert.That(source.loop, Is.False);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                FinishPlaylistTrack(player);
                Assert.That(source.clip, Is.SameAs(second));
                FinishPlaylistTrack(player);
                Assert.That(source.clip, Is.SameAs(first));
            }
        }

        [Test]
        public void ReapplyingTitlePlaylist_KeepsCurrentTrackAndPlaybackPosition()
        {
            AudioClip first = CreateClip("Title1");
            AudioClip second = CreateClip("Title2");
            MenuMusicPlayer.EnsurePlaylist(new[] { first, second });
            MenuMusicPlayer player = Object.FindAnyObjectByType<MenuMusicPlayer>();
            AudioSource source = player.GetComponent<AudioSource>();
            FinishPlaylistTrack(player);
            source.timeSamples = 22050;

            MenuMusicPlayer.EnsurePlaylist(new[] { first, second });

            Assert.That(Object.FindAnyObjectByType<MenuMusicPlayer>(), Is.SameAs(player));
            Assert.That(source.clip, Is.SameAs(second));
            Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(22050));
        }

        [UnityTest]
        public IEnumerator FightMusic_LoopsOneSelectedTrack_AndReturnsToTitleOne()
        {
            AudioClip first = CreateClip("Title1");
            AudioClip second = CreateClip("Title2");
            MenuMusicPlayer.EnsurePlaylist(new[] { first, second });
            MenuMusicPlayer player = Object.FindAnyObjectByType<MenuMusicPlayer>();
            FinishPlaylistTrack(player);

            AudioClip[] fightClips = new AudioClip[5];
            for (int index = 0; index < fightClips.Length; index++)
                fightClips[index] = CreateClip($"Fight{index + 1}", 4410);
            Random.InitState(42);
            MenuMusicPlayer.StartRandomLoop(fightClips);
            AudioSource source = player.GetComponent<AudioSource>();
            AudioClip selectedClip = source.clip;
            Assert.That(fightClips, Does.Contain(selectedClip));
            Assert.That(source.loop, Is.True);

            yield return new WaitForSecondsRealtime(0.35f);

            Assert.That(source.clip, Is.SameAs(selectedClip));
            Assert.That(source.loop, Is.True);
            MenuMusicPlayer.EnsurePlaylist(new[] { first, second });
            Assert.That(source.clip, Is.SameAs(first));
            Assert.That(source.loop, Is.False);
        }

        [Test]
        public void Playlist_IgnoresMissingAndDuplicateClips()
        {
            AudioClip first = CreateClip("Title1");
            AudioClip second = CreateClip("Title2");
            MenuMusicPlayer.EnsurePlaylist(new[] { null, first, first, second, null });
            MenuMusicPlayer player = Object.FindAnyObjectByType<MenuMusicPlayer>();
            AudioSource source = player.GetComponent<AudioSource>();
            Assert.That(source.clip, Is.SameAs(first));
            FinishPlaylistTrack(player);
            Assert.That(source.clip, Is.SameAs(second));
            FinishPlaylistTrack(player);
            Assert.That(source.clip, Is.SameAs(first));
        }

        private AudioClip CreateClip(string name, int sampleCount = 44100)
        {
            AudioClip clip = AudioClip.Create(name, sampleCount, 1, 44100, false);
            createdClips.Add(clip);
            return clip;
        }

        private static void FinishPlaylistTrack(MenuMusicPlayer player)
        {
            player.GetComponent<AudioSource>().Stop();
            typeof(MenuMusicPlayer).GetField("earliestNextTrackDspTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, 0d);
            typeof(MenuMusicPlayer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, null);
        }
    }
}
