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
        private const string LongRangeTowerDataPath = "Assets/ScriptableObjects/Towers/TowerData_LongRange.asset";
        private const string BasicTowerPrefabPath = "Assets/Prefabs/Towers/Tower_Basic.prefab";
        private const string LaserTowerPrefabPath = "Assets/Prefabs/Towers/Tower_Laser.prefab";
        private const string LongRangeTowerPrefabPath = "Assets/Prefabs/Towers/Tower_LongRange.prefab";

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
            Assert.That(laser.bulletData.attackSpeedMultiplier, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(laser.bulletData.rangeMultiplier, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(laser.DamagePerShot, Is.EqualTo(30f).Within(0.001f));
            Assert.That(laser.AttacksPerSecond, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(laser.TargetingRange, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(laser.upgradeData, Is.Not.Null);
            Assert.That(laser.upgradeData.levels, Has.Count.GreaterThan(0));
            Assert.That(laser.upgradeData.levels[0].rangeBonus, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(
                laser.GetTargetingRange(laser.upgradeData.levels[0].rangeBonus) - laser.TargetingRange,
                Is.EqualTo(0.15f).Within(0.001f),
                "The laser range upgrade should only add a small effective range increase.");
            Assert.That(laser.bulletData.isPiercing, Is.True);
            Assert.That(laser.bulletData.maxTravelDistance, Is.GreaterThanOrEqualTo(100f));
            Assert.That(laser.bulletData.bulletPrefab, Is.Not.Null);
            Assert.That(laser.towerPrefab, Is.Not.Null);
        }

        [Test]
        public void LongRangeTower_TradesDamageAndFireRateForRange()
        {
            TowerData basic = AssetDatabase.LoadAssetAtPath<TowerData>(BasicTowerDataPath);
            TowerData longRange = AssetDatabase.LoadAssetAtPath<TowerData>(LongRangeTowerDataPath);

            Assert.That(basic, Is.Not.Null);
            Assert.That(longRange, Is.Not.Null);
            Assert.That(longRange.bulletData, Is.SameAs(basic.bulletData), "Long Range Tower should fire normal bullets.");
            Assert.That(longRange.DamagePerShot, Is.LessThan(basic.DamagePerShot));
            Assert.That(longRange.AttacksPerSecond, Is.LessThan(basic.AttacksPerSecond));
            Assert.That(longRange.AttacksPerSecond, Is.GreaterThanOrEqualTo(0.8f), "It should only be moderately slower than Basic.");
            Assert.That(longRange.TargetingRange, Is.GreaterThan(basic.TargetingRange));
            Assert.That(
                longRange.DamagePerShot * longRange.AttacksPerSecond,
                Is.LessThan(basic.DamagePerShot * basic.AttacksPerSecond),
                "The extra range must be paid for with lower damage output.");
            Assert.That(longRange.upgradeData, Is.Not.Null);
            Assert.That(longRange.towerPrefab, Is.Not.Null);
        }

        [Test]
        public void GameplayScene_OffersConfiguredTowerVariantsForBuilding()
        {
            TowerData laser = AssetDatabase.LoadAssetAtPath<TowerData>(LaserTowerDataPath);
            TowerData longRange = AssetDatabase.LoadAssetAtPath<TowerData>(LongRangeTowerDataPath);
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);

            try
            {
                BuildManager buildManager = FindComponent<BuildManager>(scene);
                Assert.That(buildManager, Is.Not.Null);

                SerializedProperty towers = new SerializedObject(buildManager).FindProperty("availableTowers");
                bool foundLaser = false;
                bool foundLongRange = false;
                for (int index = 0; index < towers.arraySize; index++)
                {
                    Object tower = towers.GetArrayElementAtIndex(index).objectReferenceValue;
                    if (tower == laser)
                        foundLaser = true;
                    if (tower == longRange)
                        foundLongRange = true;
                }

                Assert.That(foundLaser, Is.True, "Laser tower is missing from the build menu.");
                Assert.That(foundLongRange, Is.True, "Long Range Tower is missing from the build menu.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase(BasicTowerPrefabPath)]
        [TestCase(LaserTowerPrefabPath)]
        [TestCase(LongRangeTowerPrefabPath)]
        public void TowerPrefab_HasFixedBaseAndRotatingHead(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            Tower tower = prefab.GetComponent<Tower>();
            Transform turretPivot = prefab.transform.Find("TurretPivot");
            Transform head = turretPivot != null ? turretPivot.Find("Head") : null;
            Transform firePoint = turretPivot != null ? turretPivot.Find("FirePoint") : null;

            Assert.That(tower, Is.Not.Null, "Tower component must remain on the fixed prefab root.");
            Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Not.Null, "The fixed root needs the base visual.");
            Assert.That(turretPivot, Is.Not.Null, "The rotating turret pivot is missing.");
            Assert.That(head, Is.Not.Null, "The turret head visual is missing.");
            Assert.That(head.GetComponent<SpriteRenderer>(), Is.Not.Null, "The turret head needs a sprite renderer.");
            Assert.That(firePoint, Is.Not.Null, "The projectile spawn point is missing.");

            SerializedObject serializedTower = new SerializedObject(tower);
            Assert.That(serializedTower.FindProperty("turretPivot").objectReferenceValue, Is.EqualTo(turretPivot));
            Assert.That(serializedTower.FindProperty("firePoint").objectReferenceValue, Is.EqualTo(firePoint));
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
