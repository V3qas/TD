using NUnit.Framework;
using UnityEngine;
using TD.Enemies;

namespace TD.Tests.EditMode
{
    public class EnemyPathTests
    {
        [TestCase(1)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void Advance_PreservesDistanceAcrossCornersRegardlessOfFrameCount(int frames)
        {
            EnemyPath path = CreatePath();
            int waypoint = 0;
            Vector3 position = Vector3.zero;
            for (int frame = 0; frame < frames; frame++)
                position = path.Advance(position, ref waypoint, 2.5f / frames);

            Assert.That(Vector3.Distance(position, new Vector3(1.5f, 1f)), Is.LessThan(0.0001f));
            Assert.That(path.GetProgress(position, waypoint), Is.EqualTo(0.625f).Within(0.0001f));
        }

        [Test]
        public void Advance_StopsAtGoalAndRemainsThere()
        {
            EnemyPath path = CreatePath();
            int waypoint = 0;
            Vector3 position = path.Advance(Vector3.zero, ref waypoint, 100f);
            Assert.That(position, Is.EqualTo(new Vector3(3f, 1f)));
            Assert.That(waypoint, Is.EqualTo(path.Count));
            Assert.That(path.GetProgress(position, waypoint), Is.EqualTo(1f));
            Assert.That(path.Advance(position, ref waypoint, 100f), Is.EqualTo(position));
        }

        [Test]
        public void Path_IsIndependentOfMutableSource()
        {
            Vector3[] source = { Vector3.zero, Vector3.right };
            EnemyPath path = new EnemyPath(source);
            source[1] = Vector3.up;
            int waypoint = 0;
            Assert.That(path.Advance(Vector3.zero, ref waypoint, 0.5f), Is.EqualTo(Vector3.right * 0.5f));
        }

        private static EnemyPath CreatePath()
        {
            return new EnemyPath(new[] { Vector3.zero, Vector3.zero, Vector3.right, new Vector3(1f, 1f), new Vector3(3f, 1f) });
        }
    }
}
