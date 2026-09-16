using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TD.Core;
using TD.Enemies;
using TD.Grid;
using TD.Level;

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
