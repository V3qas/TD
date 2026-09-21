using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TD.Bullets;
using TD.Combat;
using TD.Core;
using TD.Enemies;
using TD.Level;
using TD.Towers;
using TD.UI;

namespace TD.Tests.PlayMode
{
    public class EditorLifecycleTests
    {
        private Scene gameplayScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameplayLifecycle.StopCombat();
            if (gameplayScene.IsValid() && gameplayScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(gameplayScene);
            GameSession.EndTestRun();
            GameSession.EndMapEditorMode();
            GameSession.ClearSelectedLevel();
            PrefabPool.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedEditorTests_ClearCombatObjectsAndRestoreMap()
        {
            GameSession.EndTestRun();
            GameSession.BeginMapEditorMode();
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Additive);
            gameplayScene = SceneManager.GetSceneByName("Gameplay");
            SceneManager.SetActiveScene(gameplayScene);
            yield return null;

            RuntimeMapEditorController editor = FindInScene<RuntimeMapEditorController>();
            LevelLoader loader = FindInScene<LevelLoader>();
            BuildManager builder = FindInScene<BuildManager>();
            GameState state = FindInScene<GameState>();
            LevelMapAuthoringState authoring = new LevelMapAuthoringState();
            authoring.CreateNewMap(5, 3, true);
            authoring.PaintCell(new Vector2Int(2, 0), LevelMapPaintTool.Destructible, 100, 15);
            Invoke(editor, "LoadDefinition", authoring.BuildDefinition());
            editor.Open();

            for (int run = 0; run < 2; run++)
            {
                Invoke(editor, "StartTestRun");
                yield return null;
                Assert.That(loader.HasLoadedLevel, Is.True);
                Assert.That(state.Money, Is.EqualTo(100));
                Assert.That(Destructible.ActiveTargets.Count, Is.EqualTo(1));

                Destructible obstacle = Destructible.ActiveTargets[0];
                obstacle.Mark();
                GameObject towerObject = Object.Instantiate(builder.AvailableTowers[0].towerPrefab, new Vector3(1.5f, 0.5f), Quaternion.identity);
                towerObject.GetComponent<Tower>().Initialize(builder.AvailableTowers[0]);
                builder.SelectTowerToBuild(builder.AvailableTowers[0]);
                BulletData data = builder.AvailableTowers[0].bulletData;
                GameObject bulletObject = PrefabPool.Spawn(data.bulletPrefab, Vector3.zero, Quaternion.identity);
                bulletObject.GetComponent<Bullet>().Initialize(data, 10f, obstacle);
                state.TrySpendMoney(50);

                Invoke(editor, "ReturnFromTest");
                Assert.That(GameSession.IsEditorTestRun, Is.False);
                Assert.That(loader.HasLoadedLevel, Is.False);
                Assert.That(bulletObject.activeSelf, Is.False);
                Assert.That(Destructible.ActiveTargets, Is.Empty);
                Assert.That(Enemy.ActiveEnemies, Is.Empty);
                Assert.That(builder.IsPlacingTower, Is.False);
                yield return null;
                Assert.That(Object.FindObjectsByType<Tower>(FindObjectsInactive.Exclude), Is.Empty);
                foreach (SpriteRenderer renderer in loader.GetComponentsInChildren<SpriteRenderer>())
                    Assert.Fail($"Stale gameplay visual remained in editor: {renderer.name}");
            }
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in gameplayScene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            Assert.Fail($"Missing {typeof(T).Name}");
            return null;
        }

        private static void Invoke(object target, string method, params object[] arguments)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }
    }
}
