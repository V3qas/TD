using NUnit.Framework;
using UnityEngine;
using TD.Core;
using TD.Towers;

namespace TD.Tests.EditMode
{
    public class TowerUpgradeServiceTests
    {
        private GameState state;
        private Tower tower;
        private TowerData data;
        private TowerUpgradeData upgrades;

        [SetUp]
        public void SetUp()
        {
            GameSession.EndMapEditorMode();
            GameSession.EndTestRun();
            state = new GameObject("UpgradeState").AddComponent<GameState>();
            state.ResetState();
            data = ScriptableObject.CreateInstance<TowerData>();
            data.damage = 10f;
            upgrades = ScriptableObject.CreateInstance<TowerUpgradeData>();
            upgrades.levels.Add(new TowerUpgradeData.UpgradeLevel { cost = 75, damageBonus = 5f });
            tower = new GameObject("UpgradeTower").AddComponent<Tower>();
            tower.Initialize(data, upgrades);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tower.gameObject);
            Object.DestroyImmediate(state.gameObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(upgrades);
        }

        [Test]
        public void Purchase_DeductsCostAndAppliesUpgradeOnce()
        {
            Assert.That(TowerUpgradeService.TryPurchase(tower, state), Is.True);
            Assert.That(state.Money, Is.EqualTo(25));
            Assert.That(tower.Damage, Is.EqualTo(15));
            Assert.That(TowerUpgradeService.TryPurchase(tower, state), Is.False);
            Assert.That(state.Money, Is.EqualTo(25));
        }

        [Test]
        public void Purchase_InsufficientFundsChangesNeitherTowerNorMoney()
        {
            state.TrySpendMoney(50);
            Assert.That(TowerUpgradeService.TryPurchase(tower, state), Is.False);
            Assert.That(state.Money, Is.EqualTo(50));
            Assert.That(tower.CurrentUpgradeLevel, Is.Zero);
        }

        [Test]
        public void Purchase_AfterMatchEndIsRejected()
        {
            state.Win();
            Assert.That(TowerUpgradeService.TryPurchase(tower, state), Is.False);
            Assert.That(state.Money, Is.EqualTo(100));
            Assert.That(tower.CurrentUpgradeLevel, Is.Zero);
        }
    }
}
