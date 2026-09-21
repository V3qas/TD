using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TD.Core;
using TD.Enemies;
using TD.Grid;
using TD.Level;
using TD.Towers;

namespace TD.Tests.PlayMode
{
    public class CampaignSceneSmokeTests
    {
        private Scene gameplayScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSession.EndTestRun();
            GameSession.EndMapEditorMode();
            GameSession.ClearSelectedLevel();
            PrefabPool.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gameplayScene.IsValid() && gameplayScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(gameplayScene);
                if (unload != null)
                    yield return unload;
            }

            PrefabPool.Clear();
            GameSession.EndTestRun();
            GameSession.EndMapEditorMode();
            GameSession.ClearSelectedLevel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameplayScene_LoadsCampaign_SpawnsEnemy_AndHandlesMatchEnd()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "Gameplay scene is missing from Build Settings.");
            yield return load;

            gameplayScene = SceneManager.GetSceneByName("Gameplay");
            Assert.That(gameplayScene.IsValid() && gameplayScene.isLoaded, Is.True);

            LevelLoader levelLoader = FindComponent<LevelLoader>(gameplayScene);
            GridManager gridManager = FindComponent<GridManager>(gameplayScene);
            GameState gameState = FindComponent<GameState>(gameplayScene);
            EnemySpawner spawner = FindComponent<EnemySpawner>(gameplayScene);

            Assert.That(levelLoader, Is.Not.Null);
            Assert.That(gridManager, Is.Not.Null);
            Assert.That(gameState, Is.Not.Null);
            Assert.That(spawner, Is.Not.Null);

            float timeout = Time.realtimeSinceStartup + 3f;
            while ((!levelLoader.HasLoadedLevel || !gridManager.HasGrid) && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.That(levelLoader.HasLoadedLevel, Is.True, "Campaign level did not load.");
            Assert.That(gridManager.HasGrid, Is.True, "Campaign grid was not built.");

            timeout = Time.realtimeSinceStartup + 3f;
            while (CountActiveEnemies() == 0 && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.That(CountActiveEnemies(), Is.GreaterThan(0), "Campaign did not spawn an enemy.");

            Assert.That(gameState.Lose(), Is.True);
            yield return null;

            Assert.That(gameState.State, Is.EqualTo(MatchState.Lost));
            Assert.That(CountActiveEnemies(), Is.Zero, "Enemies were not cleared at match end.");
        }

        [UnityTest]
        public IEnumerator Campaign_BuildUpgradeSellAndReload_RestoreStartingState()
        {
            for (int run = 0; run < 2; run++)
            {
                yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Additive);
                gameplayScene = SceneManager.GetSceneByName("Gameplay");
                SceneManager.SetActiveScene(gameplayScene);
                yield return null;

                GridManager grid = FindComponent<GridManager>(gameplayScene);
                GameState state = FindComponent<GameState>(gameplayScene);
                BuildManager builder = FindComponent<BuildManager>(gameplayScene);
                Assert.That(state.Money, Is.EqualTo(100));
                Assert.That(state.Lives, Is.EqualTo(state.StartingLives));
                Assert.That(state.CurrentRound, Is.EqualTo(1));
                Assert.That(state.IsPlaying, Is.True);
                Assert.That(builder.GetComponentsInChildren<Tower>(), Is.Empty);

                Vector2Int? buildCell = null;
                for (int row = 0; row < grid.Height && !buildCell.HasValue; row++)
                    for (int column = 0; column < grid.Width; column++)
                        if (grid.CanBuildAt(new Vector2Int(column, row)))
                        {
                            buildCell = new Vector2Int(column, row);
                            break;
                        }
                Assert.That(buildCell.HasValue, Is.True);

                TowerData data = builder.AvailableTowers[0];
                builder.SelectTowerToBuild(data);
                Assert.That(builder.TryBuildAtCell(grid.StartCell), Is.False);
                Assert.That(state.Money, Is.EqualTo(100));
                Assert.That(builder.TryBuildAtCell(buildCell.Value), Is.True);
                Assert.That(state.Money, Is.EqualTo(100 - data.cost));
                Assert.That(builder.TryBuildAtCell(buildCell.Value), Is.False);
                builder.ClearSelectedTowerToBuild();
                yield return null;
                Tower tower = builder.GetComponentInChildren<Tower>();
                Assert.That(tower, Is.Not.Null);

                int upgradeCost = tower.GetNextUpgradeCost();
                state.AddMoney(upgradeCost);
                int moneyBeforeUpgrade = state.Money;
                Assert.That(TowerUpgradeService.TryPurchase(tower, state), Is.True);
                Assert.That(state.Money, Is.EqualTo(moneyBeforeUpgrade - upgradeCost));
                Assert.That(tower.CurrentUpgradeLevel, Is.EqualTo(1));
                int sellValue = tower.GetSellValue();
                int moneyBeforeSale = state.Money;
                builder.SellTower(tower);
                Assert.That(state.Money, Is.EqualTo(moneyBeforeSale + sellValue));
                Assert.That(grid.GetCell(buildCell.Value).IsOccupied, Is.False);
                state.Lose();
                yield return SceneManager.UnloadSceneAsync(gameplayScene);
                yield return null;
                Assert.That(GameState.Instance, Is.Null);
                Assert.That(Enemy.ActiveEnemies, Is.Empty);
            }
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        private static int CountActiveEnemies()
        {
            int count = 0;
            for (int index = 0; index < Enemy.ActiveEnemies.Count; index++)
            {
                if (Enemy.ActiveEnemies[index] != null)
                    count++;
            }

            return count;
        }
    }
}
