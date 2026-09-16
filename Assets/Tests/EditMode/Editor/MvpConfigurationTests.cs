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
        private const string MenuConfigPath = "Assets/ScriptableObjects/MainMenuConfig_Main.asset";
        private const string RunnerDataPath = "Assets/ScriptableObjects/Enemies/EnemyData_Runner.asset";
        private const string UpgradeDataPath = "Assets/ScriptableObjects/Towers/TowerUpgradeData_Basic.asset";

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

                    Assert.That(entry.FindPropertyRelative("enemyPrefab").objectReferenceValue, Is.Not.Null,
                        "Runner spawn entry needs an enemy prefab.");
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
