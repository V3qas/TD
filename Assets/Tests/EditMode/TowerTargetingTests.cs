using NUnit.Framework;
using UnityEngine;
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
    }
}
