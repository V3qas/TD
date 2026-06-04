using NUnit.Framework;
using UnityEngine;
using TD.Level;

namespace TD.Tests.EditMode
{
    public class MapCameraFrameTests
    {
        [Test]
        public void ClampPosition_LeavesValueUntouchedWhenInsideBounds()
        {
            MapCameraFrame frame = new MapCameraFrame
            {
                IsValid = true,
                MinCenter = new Vector2(0f, 0f),
                MaxCenter = new Vector2(10f, 10f)
            };

            Vector3 clamped = frame.ClampPosition(new Vector3(5f, 5f, -10f));

            Assert.AreEqual(new Vector3(5f, 5f, -10f), clamped);
        }

        [Test]
        public void ClampPosition_ClampsBeyondMinMax()
        {
            MapCameraFrame frame = new MapCameraFrame
            {
                IsValid = true,
                MinCenter = new Vector2(0f, 0f),
                MaxCenter = new Vector2(10f, 10f)
            };

            Vector3 clampedLow = frame.ClampPosition(new Vector3(-5f, -5f, 0f));
            Vector3 clampedHigh = frame.ClampPosition(new Vector3(20f, 20f, 0f));

            Assert.AreEqual(new Vector3(0f, 0f, 0f), clampedLow);
            Assert.AreEqual(new Vector3(10f, 10f, 0f), clampedHigh);
        }

        [Test]
        public void ClampPosition_IsNoOpWhenFrameIsInvalid()
        {
            MapCameraFrame frame = new MapCameraFrame
            {
                IsValid = false,
                MinCenter = new Vector2(0f, 0f),
                MaxCenter = new Vector2(10f, 10f)
            };

            Vector3 clamped = frame.ClampPosition(new Vector3(999f, -999f, 0f));

            Assert.AreEqual(new Vector3(999f, -999f, 0f), clamped);
        }

        [Test]
        public void CanPan_IsTrueWhenAtLeastOneAxisHasRange()
        {
            MapCameraFrame fixedFrame = new MapCameraFrame
            {
                IsValid = true,
                MinCenter = new Vector2(5f, 5f),
                MaxCenter = new Vector2(5f, 5f)
            };
            MapCameraFrame pannableFrame = new MapCameraFrame
            {
                IsValid = true,
                MinCenter = new Vector2(0f, 5f),
                MaxCenter = new Vector2(10f, 5f)
            };

            Assert.IsFalse(fixedFrame.CanPan);
            Assert.IsTrue(pannableFrame.CanPan);
        }
    }
}
