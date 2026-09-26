using NUnit.Framework;
using TD.Menu;
using UnityEngine;

namespace TD.Tests.EditMode
{
    public class DisplaySettingsTests
    {
        [TestCase(FullScreenMode.Windowed, false)]
        [TestCase(FullScreenMode.FullScreenWindow, true)]
        [TestCase(FullScreenMode.ExclusiveFullScreen, true)]
        [TestCase(FullScreenMode.MaximizedWindow, true)]
        public void IsFullscreenMode_ReturnsFalseOnlyForWindowed(
            FullScreenMode mode,
            bool expectedFullscreen)
        {
            Assert.That(DisplaySettings.IsFullscreenMode(mode), Is.EqualTo(expectedFullscreen));
        }

        [TestCase(false, FullScreenMode.Windowed)]
        [TestCase(true, FullScreenMode.FullScreenWindow)]
        public void GetMode_UsesWindowedOrBorderlessFullscreen(
            bool fullscreen,
            FullScreenMode expectedMode)
        {
            Assert.That(DisplaySettings.GetMode(fullscreen), Is.EqualTo(expectedMode));
        }
    }
}
