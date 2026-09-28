using NUnit.Framework;
using TD.Menu;

namespace TD.Tests.EditMode
{
    public class MenuMusicPlayerTests
    {
        [Test]
        public void ChooseNextRotationIndex_ReturnsNoTrackForEmptyRotation()
        {
            Assert.That(MenuMusicPlayer.ChooseNextRotationIndex(0, -1, 0), Is.EqualTo(-1));
        }

        [Test]
        public void ChooseNextRotationIndex_UsesRandomFirstTrack()
        {
            Assert.That(MenuMusicPlayer.ChooseNextRotationIndex(2, -1, 0), Is.EqualTo(0));
            Assert.That(MenuMusicPlayer.ChooseNextRotationIndex(2, -1, 1), Is.EqualTo(1));
        }

        [Test]
        public void ChooseNextRotationIndex_DoesNotRepeatPreviousTrack()
        {
            Assert.That(MenuMusicPlayer.ChooseNextRotationIndex(2, 0, 0), Is.EqualTo(1));
            Assert.That(MenuMusicPlayer.ChooseNextRotationIndex(2, 1, 0), Is.EqualTo(0));
            Assert.That(MenuMusicPlayer.ChooseNextRotationIndex(3, 1, 0), Is.EqualTo(0));
            Assert.That(MenuMusicPlayer.ChooseNextRotationIndex(3, 1, 1), Is.EqualTo(2));
        }
    }
}
