using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TD.Bullets;
using TD.Core;
using TD.Towers;

namespace TD.Tests.EditMode
{
    public class LaserTowerConfigurationTests
    {
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string BasicTowerDataPath = "Assets/ScriptableObjects/Towers/TowerData_Basic.asset";
        private const string LaserTowerDataPath = "Assets/ScriptableObjects/Towers/TowerData_Laser.asset";

        [Test]
        public void LaserTower_UsesRequestedCombatProfile()
        {
            TowerData basic = AssetDatabase.LoadAssetAtPath<TowerData>(BasicTowerDataPath);
            TowerData laser = AssetDatabase.LoadAssetAtPath<TowerData>(LaserTowerDataPath);

            Assert.That(basic, Is.Not.Null);
            Assert.That(laser, Is.Not.Null);
            Assert.That(basic.bulletData, Is.Not.Null);
            Assert.That(basic.bulletData.damageMultiplier, Is.EqualTo(1f));
            Assert.That(basic.bulletData.attackSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(basic.bulletData.rangeMultiplier, Is.EqualTo(1f));
            Assert.That(laser.damage, Is.EqualTo(basic.damage), "Tower variants must share base damage.");
            Assert.That(laser.range, Is.EqualTo(basic.range), "Tower variants must share base range.");
            Assert.That(laser.attackSpeed, Is.EqualTo(basic.attackSpeed), "Tower variants must share base fire rate.");
            Assert.That(laser.bulletData, Is.Not.Null);
            Assert.That(laser.bulletData.bulletType, Is.EqualTo(BulletType.Laser));
            Assert.That(laser.bulletData.damageMultiplier, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(laser.bulletData.attackSpeedMultiplier, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(laser.bulletData.rangeMultiplier, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(laser.DamagePerShot, Is.EqualTo(30f).Within(0.001f));
            Assert.That(laser.AttacksPerSecond, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(laser.TargetingRange, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(laser.bulletData.isPiercing, Is.True);
            Assert.That(laser.bulletData.maxTravelDistance, Is.GreaterThanOrEqualTo(100f));
            Assert.That(laser.bulletData.bulletPrefab, Is.Not.Null);
            Assert.That(laser.towerPrefab, Is.Not.Null);
        }

        [Test]
        public void GameplayScene_OffersLaserTowerForBuilding()
        {
            TowerData laser = AssetDatabase.LoadAssetAtPath<TowerData>(LaserTowerDataPath);
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);

            try
            {
                BuildManager buildManager = FindComponent<BuildManager>(scene);
                Assert.That(buildManager, Is.Not.Null);

                SerializedProperty towers = new SerializedObject(buildManager).FindProperty("availableTowers");
                bool foundLaser = false;
                for (int index = 0; index < towers.arraySize; index++)
                {
                    if (towers.GetArrayElementAtIndex(index).objectReferenceValue == laser)
                    {
                        foundLaser = true;
                        break;
                    }
                }

                Assert.That(foundLaser, Is.True, "Laser tower is missing from the build menu.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }
    }
}
