using NUnit.Framework;
using UnityEngine;
using TD.Bullets;
using TD.Towers;

namespace TD.Tests.EditMode
{
    public class TowerTargetingTests
    {
        private GameObject firstObject;
        private GameObject secondObject;
        private TowerData towerData;

        [SetUp]
        public void SetUp()
        {
            towerData = ScriptableObject.CreateInstance<TowerData>();
            firstObject = new GameObject("FirstTower");
            secondObject = new GameObject("SecondTower");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
            Object.DestroyImmediate(towerData);
        }

        [Test]
        public void TargetingMode_IsStoredPerTowerInstance()
        {
            Tower first = firstObject.AddComponent<Tower>();
            Tower second = secondObject.AddComponent<Tower>();
            first.Initialize(towerData);
            second.Initialize(towerData);

            first.SetTargetingMode(TargetingMode.Fastest);

            Assert.That(first.TargetingMode, Is.EqualTo(TargetingMode.Fastest));
            Assert.That(second.TargetingMode, Is.EqualTo(TargetingMode.First));
        }

        [Test]
        public void CycleTargetingMode_VisitsAllModesAndWrapsToFirst()
        {
            Tower tower = firstObject.AddComponent<Tower>();
            tower.Initialize(towerData);

            Assert.That(tower.CycleTargetingMode(), Is.EqualTo(TargetingMode.Last));
            Assert.That(tower.CycleTargetingMode(), Is.EqualTo(TargetingMode.HighestHealth));
            Assert.That(tower.CycleTargetingMode(), Is.EqualTo(TargetingMode.LowestHealth));
            Assert.That(tower.CycleTargetingMode(), Is.EqualTo(TargetingMode.Fastest));
            Assert.That(tower.CycleTargetingMode(), Is.EqualTo(TargetingMode.First));
        }

        [Test]
        public void BulletType_ModifiesTowerBaseStats()
        {
            BulletData laserData = ScriptableObject.CreateInstance<BulletData>();
            try
            {
                laserData.damageMultiplier = 1.5f;
                laserData.attackSpeedMultiplier = 0.75f;
                laserData.rangeMultiplier = 1.5f;

                towerData.damage = 20f;
                towerData.attackSpeed = 1f;
                towerData.range = 3f;
                towerData.bulletData = laserData;

                Tower tower = firstObject.AddComponent<Tower>();
                tower.Initialize(towerData);

                Assert.That(tower.Damage, Is.EqualTo(30f).Within(0.001f));
                Assert.That(tower.AttackSpeed, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(tower.Range, Is.EqualTo(4.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(laserData);
            }
        }

        [Test]
        public void AimRotation_UsesPositiveXAxisAsForward()
        {
            bool hasDirection = Tower.TryGetAimRotation(Vector3.zero, Vector3.up, out Quaternion rotation);

            Assert.That(hasDirection, Is.True);
            Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(0.001f));
        }

        [Test]
        public void AimRotation_RejectsOverlappingTarget()
        {
            bool hasDirection = Tower.TryGetAimRotation(Vector3.one, Vector3.one, out Quaternion rotation);

            Assert.That(hasDirection, Is.False);
            Assert.That(rotation, Is.EqualTo(Quaternion.identity));
        }
    }
}
