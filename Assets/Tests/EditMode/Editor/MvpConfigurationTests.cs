using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TD.Core;
using TD.Enemies;
using TD.Menu;

namespace TD.Tests.EditMode
{
    public class MvpConfigurationTests
    {
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string MenuConfigPath = "Assets/ScriptableObjects/MainMenuConfig_Main.asset";
        private const string BasicEnemyPrefabPath = "Assets/Prefabs/Enemies/Enemy_Basic.prefab";
        private const string RunnerEnemyPrefabPath = "Assets/Prefabs/Enemies/Enemy_Runner.prefab";
        private const string RunnerDataPath = "Assets/ScriptableObjects/Enemies/EnemyData_Runner.asset";
        private const string UpgradeDataPath = "Assets/ScriptableObjects/Towers/TowerUpgradeData_Basic.asset";
        private static readonly string[] BasicEnemyFramePaths =
        {
            "Assets/Art/Enemies/Basic/Enemy_Basic_1.png",
            "Assets/Art/Enemies/Basic/Enemy_Basic_2.png",
            "Assets/Art/Enemies/Basic/Enemy_Basic_3.png",
            "Assets/Art/Enemies/Basic/Enemy_Basic_4.png"
        };
        private static readonly string[] RunnerEnemyFramePaths =
        {
            "Assets/Art/Enemies/Runner/Enemy_Runner_1.png",
            "Assets/Art/Enemies/Runner/Enemy_Runner_2.png",
            "Assets/Art/Enemies/Runner/Enemy_Runner_3.png",
            "Assets/Art/Enemies/Runner/Enemy_Runner_4.png"
        };
        private static readonly Vector2[] RunnerEnemyFrameSizes =
        {
            new Vector2(195f, 286f),
            new Vector2(195f, 264f),
            new Vector2(195f, 264f),
            new Vector2(230f, 275f)
        };

        [Test]
        public void MenuScene_UsesMvpMenuConfig()
        {
            WithScene(MenuScenePath, scene =>
            {
                MainMenuController controller = FindComponent<MainMenuController>(scene);
                Assert.That(controller, Is.Not.Null, "Menu scene needs a MainMenuController.");

                SerializedProperty config = new SerializedObject(controller).FindProperty("config");
                Assert.That(config, Is.Not.Null);
                Assert.That(config.objectReferenceValue, Is.Not.Null, "Main menu config is missing.");
                Assert.That(AssetDatabase.GetAssetPath(config.objectReferenceValue), Is.EqualTo(MenuConfigPath));
            });
        }

        [Test]
        public void GameplayScene_HasRequiredRunnerAndUpgradeReferences()
        {
            WithScene(GameplayScenePath, scene =>
            {
                EnemySpawner spawner = FindComponent<EnemySpawner>(scene);
                Assert.That(spawner, Is.Not.Null, "Gameplay scene needs an EnemySpawner.");

                SerializedObject spawnerObject = new SerializedObject(spawner);
                Assert.That(spawnerObject.FindProperty("startAutomatically").boolValue, Is.True,
                    "Campaign spawning must start automatically.");

                EnemyData runnerData = AssetDatabase.LoadAssetAtPath<EnemyData>(RunnerDataPath);
                Assert.That(runnerData, Is.Not.Null, "Runner enemy data asset is missing.");

                SerializedProperty entries = spawnerObject.FindProperty("spawnEntries");
                bool foundConfiguredRunner = false;
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    if (entry.FindPropertyRelative("enemyData").objectReferenceValue != runnerData)
                        continue;

                    Object runnerPrefab = entry.FindPropertyRelative("enemyPrefab").objectReferenceValue;
                    Assert.That(runnerPrefab, Is.Not.Null,
                        "Runner spawn entry needs an enemy prefab.");
                    Assert.That(AssetDatabase.GetAssetPath(runnerPrefab), Is.EqualTo(RunnerEnemyPrefabPath),
                        "Runner spawn entry must not reuse the Basic enemy prefab.");
                    foundConfiguredRunner = true;
                    break;
                }

                Assert.That(foundConfiguredRunner, Is.True, "Runner enemy is missing from the spawn entries.");

                BuildManager buildManager = FindComponent<BuildManager>(scene);
                Assert.That(buildManager, Is.Not.Null, "Gameplay scene needs a BuildManager.");

                SerializedProperty upgrade = new SerializedObject(buildManager).FindProperty("towerUpgradeData");
                Assert.That(upgrade, Is.Not.Null);
                Assert.That(upgrade.objectReferenceValue, Is.Not.Null, "Basic tower upgrade is missing.");
                Assert.That(AssetDatabase.GetAssetPath(upgrade.objectReferenceValue), Is.EqualTo(UpgradeDataPath));
            });
        }

        [Test]
        public void DesktopPlayer_UsesSinglePlayerWindowAndFocusPolicy()
        {
            string projectSettings = File.ReadAllText(Path.GetFullPath(ProjectSettingsPath));

            Assert.That(projectSettings, Does.Contain("  fullscreenMode: 3"),
                "The desktop player should start windowed; Unity restores later user changes itself.");
            Assert.That(projectSettings, Does.Contain("  resizableWindow: 1"),
                "The desktop window should remain user-resizable.");
            Assert.That(projectSettings, Does.Contain("  runInBackground: 0"),
                "Unfocused single-player gameplay must pause instead of progressing unseen.");
        }

        [Test]
        public void BasicEnemyPrefab_UsesMovementFramesAtGameplayScale()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasicEnemyPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(BasicEnemyFramePaths[0]));
            Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(235f));
            Assert.That(renderer.color, Is.EqualTo(Color.white));
            Assert.That(prefab.transform.localScale, Is.EqualTo(new Vector3(0.8f, 0.8f, 1f)));

            EnemyFrameAnimator animator = prefab.GetComponent<EnemyFrameAnimator>();
            Assert.That(animator, Is.Not.Null);
            SerializedObject animatorObject = new SerializedObject(animator);
            SerializedProperty frames = animatorObject.FindProperty("movementFrames");
            Assert.That(frames.arraySize, Is.EqualTo(BasicEnemyFramePaths.Length));
            for (int index = 0; index < BasicEnemyFramePaths.Length; index++)
            {
                Sprite frame = frames.GetArrayElementAtIndex(index).objectReferenceValue as Sprite;
                Assert.That(frame, Is.Not.Null, $"Movement frame {index + 1} is missing.");
                Assert.That(AssetDatabase.GetAssetPath(frame), Is.EqualTo(BasicEnemyFramePaths[index]));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(235f));
                Assert.That(frame.rect.size, Is.EqualTo(new Vector2(200f, 266f)));
            }

            Assert.That(animatorObject.FindProperty("framesPerWorldUnit").floatValue, Is.EqualTo(4f));

            CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.radius, Is.EqualTo(0.3f));
        }

        [Test]
        public void RunnerEnemyPrefab_UsesOwnNormalizedMovementFrames()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerEnemyPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(RunnerEnemyFramePaths[0]));
            Assert.That(renderer.color, Is.EqualTo(Color.white));
            Assert.That(prefab.transform.localScale, Is.EqualTo(new Vector3(0.8f, 0.8f, 1f)));

            EnemyFrameAnimator animator = prefab.GetComponent<EnemyFrameAnimator>();
            Assert.That(animator, Is.Not.Null);
            SerializedObject animatorObject = new SerializedObject(animator);
            SerializedProperty frames = animatorObject.FindProperty("movementFrames");
            Assert.That(frames.arraySize, Is.EqualTo(RunnerEnemyFramePaths.Length));
            for (int index = 0; index < RunnerEnemyFramePaths.Length; index++)
            {
                Sprite frame = frames.GetArrayElementAtIndex(index).objectReferenceValue as Sprite;
                Assert.That(frame, Is.Not.Null, $"Runner movement frame {index + 1} is missing.");
                Assert.That(AssetDatabase.GetAssetPath(frame), Is.EqualTo(RunnerEnemyFramePaths[index]));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(235f));
                Assert.That(frame.rect.size, Is.EqualTo(RunnerEnemyFrameSizes[index]));
                Assert.That(frame.pivot, Is.EqualTo(RunnerEnemyFrameSizes[index] * 0.5f),
                    $"Runner movement frame {index + 1} should use its canvas center as pivot.");
            }

            Assert.That(animatorObject.FindProperty("framesPerWorldUnit").floatValue, Is.EqualTo(1.5f));

            CircleCollider2D collider = prefab.GetComponent<CircleCollider2D>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.radius, Is.EqualTo(0.3f));
        }

        private static void WithScene(string path, System.Action<Scene> assertion)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                assertion(scene);
            }
            finally
            {
                if (openedByTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
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
    }
}
