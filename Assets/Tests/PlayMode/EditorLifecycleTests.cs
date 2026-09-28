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
using TD.Menu;
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
            MenuMusicPlayer.StopAndDestroy();
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

            MenuMusicPlayer musicPlayer = Object.FindAnyObjectByType<MenuMusicPlayer>();
            Assert.That(musicPlayer, Is.Not.Null, "The map editor should keep the title music player alive.");
            AudioSource musicSource = musicPlayer.GetComponent<AudioSource>();
            Assert.That(musicSource, Is.Not.Null);
            Assert.That(musicSource.clip, Is.Not.Null);
            Assert.That(musicSource.clip.name, Is.EqualTo("Title_Main"));
            Assert.That(musicSource.loop, Is.True);

            for (int run = 0; run < 2; run++)
            {
                Invoke(editor, "StartTestRun");
                yield return null;
                Assert.That(Object.FindAnyObjectByType<MenuMusicPlayer>(), Is.SameAs(musicPlayer),
                    "Starting an editor test run must not replace or destroy the title music player.");
                Assert.That(musicSource.clip.name, Is.EqualTo("Title_Main"));
                Assert.That(musicSource.loop, Is.True);
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
                Bullet bullet = bulletObject.GetComponent<Bullet>();
                bullet.Initialize(data, obstacle.MaxHealth, obstacle);
                state.TrySpendMoney(50);

                float timeout = Time.realtimeSinceStartup + 2f;
                while (bulletObject.activeInHierarchy && Time.realtimeSinceStartup < timeout)
                    yield return null;

                Assert.That(bulletObject.activeInHierarchy, Is.False,
                    "The normal projectile did not finish its flight to the destructible.");
                yield return null;
                Assert.That(obstacle == null, Is.True,
                    "A marked destructible should be destroyed by lethal projectile damage during an editor test run.");
                Assert.That(Destructible.ActiveTargets, Is.Empty);

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
