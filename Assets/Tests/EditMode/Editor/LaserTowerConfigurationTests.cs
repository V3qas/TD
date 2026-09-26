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
        private const string LaserBulletSpritePath = "Assets/Art/Bullets/Bullet_Laser.png";
        private const string SharedTowerBaseSpritePath = "Assets/Art/Towers/Tower_Base.png";
        private const string SharedTowerBaseAccentSpritePath = "Assets/Art/Towers/Tower_BaseAccent.png";
        private const string BasicTowerHeadSpritePath = "Assets/Art/Towers/Basic/Tower_BasicHead.png";
        private const string BasicTowerHeadAccentSpritePath = "Assets/Art/Towers/Basic/Tower_BasicHeadAccent.png";
        private const string LaserTowerHeadSpritePath = "Assets/Art/Towers/Laser/Tower_LaserHead.png";
        private const string LaserTowerHeadAccentSpritePath = "Assets/Art/Towers/Laser/Tower_LaserHeadAccent.png";
        private const string LongRangeTowerHeadSpritePath = "Assets/Art/Towers/Long/Tower_LongHead.png";
        private const string LongRangeTowerHeadAccentSpritePath = "Assets/Art/Towers/Long/Tower_LongHeadAccent.png";

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
            Assert.That(basic.bulletData.towerAccentColor, Is.EqualTo(Color.white));
            Assert.That(laser.damage, Is.EqualTo(basic.damage), "Tower variants must share base damage.");
            Assert.That(laser.range, Is.EqualTo(basic.range), "Tower variants must share base range.");
            Assert.That(laser.attackSpeed, Is.EqualTo(basic.attackSpeed), "Tower variants must share base fire rate.");
            Assert.That(laser.bulletData, Is.Not.Null);
            Assert.That(laser.bulletData.bulletType, Is.EqualTo(BulletType.Laser));
            Assert.That(laser.bulletData.damageMultiplier, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(laser.bulletData.attackSpeedMultiplier, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(laser.bulletData.rangeMultiplier, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(
                laser.bulletData.towerAccentColor,
                Is.EqualTo(new Color(0.33333334f, 0.8745098f, 1f, 1f)));
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
            Assert.That(laser.bulletData.laserPiercing, Is.True);
            Assert.That(laser.bulletData.maxTravelDistance, Is.GreaterThanOrEqualTo(100f));
            Assert.That(laser.bulletData.bulletPrefab, Is.Not.Null);
            SpriteRenderer laserRenderer = laser.bulletData.bulletPrefab.GetComponent<SpriteRenderer>();
            Assert.That(laserRenderer, Is.Not.Null);
            Assert.That(laserRenderer.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(laserRenderer.sprite), Is.EqualTo(LaserBulletSpritePath));
            Assert.That(laserRenderer.color, Is.EqualTo(Color.white));
            TextureImporter laserImporter = AssetImporter.GetAtPath(LaserBulletSpritePath) as TextureImporter;
            Assert.That(laserImporter, Is.Not.Null);
            Assert.That(laserImporter.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            TextureImporterSettings laserImporterSettings = new TextureImporterSettings();
            laserImporter.ReadTextureSettings(laserImporterSettings);
            Assert.That(laserImporterSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(laserImporter.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
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
            SpriteRenderer baseRenderer = prefab.GetComponent<SpriteRenderer>();
            Transform baseAccent = prefab.transform.Find("BaseAccent");
            Transform turretPivot = prefab.transform.Find("TurretPivot");
            Transform head = turretPivot != null ? turretPivot.Find("Head") : null;
            Transform firePoint = turretPivot != null ? turretPivot.Find("FirePoint") : null;

            Assert.That(tower, Is.Not.Null, "Tower component must remain on the fixed prefab root.");
            Assert.That(baseRenderer, Is.Not.Null, "The fixed root needs the base visual.");
            Assert.That(baseRenderer.sprite, Is.Not.Null, "The tower base sprite is missing.");
            Assert.That(AssetDatabase.GetAssetPath(baseRenderer.sprite), Is.EqualTo(SharedTowerBaseSpritePath));
            Assert.That(baseRenderer.sprite.pixelsPerUnit, Is.EqualTo(440f).Within(0.001f));
            Assert.That(baseRenderer.color, Is.EqualTo(Color.white), "The authored base colors must not be multiplied by a prefab tint.");
            Assert.That(baseAccent, Is.Not.Null, "The fixed base accent visual is missing.");
            SpriteRenderer accentRenderer = baseAccent.GetComponent<SpriteRenderer>();
            Assert.That(accentRenderer, Is.Not.Null, "The base accent needs a sprite renderer.");
            Assert.That(accentRenderer.sprite, Is.Not.Null, "The base accent sprite is missing.");
            Assert.That(AssetDatabase.GetAssetPath(accentRenderer.sprite), Is.EqualTo(SharedTowerBaseAccentSpritePath));
            Assert.That(accentRenderer.sprite.pixelsPerUnit, Is.EqualTo(440f).Within(0.001f));
            Assert.That(accentRenderer.sortingOrder, Is.GreaterThan(baseRenderer.sortingOrder));
            Assert.That(turretPivot, Is.Not.Null, "The rotating turret pivot is missing.");
            Assert.That(head, Is.Not.Null, "The turret head visual is missing.");
            SpriteRenderer headRenderer = head.GetComponent<SpriteRenderer>();
            Assert.That(headRenderer, Is.Not.Null, "The turret head needs a sprite renderer.");
            Assert.That(headRenderer.sprite, Is.Not.Null, "The turret head sprite is missing.");
            Assert.That(headRenderer.sprite.pixelsPerUnit, Is.EqualTo(440f).Within(0.001f));
            Assert.That(
                headRenderer.sprite.rect.height * head.localScale.y,
                Is.EqualTo(277f).Within(0.01f),
                "Every tower head should render at the same normalized height.");
            Assert.That(headRenderer.sortingOrder, Is.GreaterThan(accentRenderer.sortingOrder));
            Assert.That(firePoint, Is.Not.Null, "The projectile spawn point is missing.");

            SpriteRenderer headAccentRenderer = null;
            int expectedAccentRendererCount = 1;
            if (prefabPath == BasicTowerPrefabPath || prefabPath == LongRangeTowerPrefabPath)
            {
                string expectedHeadSpritePath = prefabPath == BasicTowerPrefabPath
                    ? BasicTowerHeadSpritePath
                    : LongRangeTowerHeadSpritePath;
                string expectedHeadAccentSpritePath = prefabPath == BasicTowerPrefabPath
                    ? BasicTowerHeadAccentSpritePath
                    : LongRangeTowerHeadAccentSpritePath;
                Transform headAccent = head.Find("HeadAccent");
                Assert.That(headAccent, Is.Not.Null, "The authored tower head accent is missing.");
                headAccentRenderer = headAccent.GetComponent<SpriteRenderer>();
                Assert.That(headAccentRenderer, Is.Not.Null, "The authored tower head accent needs a sprite renderer.");
                Assert.That(headRenderer.sprite, Is.Not.Null, "The authored tower head sprite is missing.");
                Assert.That(headAccentRenderer.sprite, Is.Not.Null, "The authored tower head accent sprite is missing.");
                Assert.That(AssetDatabase.GetAssetPath(headRenderer.sprite), Is.EqualTo(expectedHeadSpritePath));
                Assert.That(AssetDatabase.GetAssetPath(headAccentRenderer.sprite), Is.EqualTo(expectedHeadAccentSpritePath));
                Assert.That(headRenderer.sprite.pixelsPerUnit, Is.EqualTo(440f).Within(0.001f));
                Assert.That(headAccentRenderer.sprite.pixelsPerUnit, Is.EqualTo(440f).Within(0.001f));
                Assert.That(headRenderer.sprite.rect, Is.EqualTo(headAccentRenderer.sprite.rect));
                Assert.That(head.localPosition, Is.EqualTo(new Vector3(0.18f, 0f, 0f)));
                Assert.That(head.localScale, Is.EqualTo(Vector3.one));
                Assert.That(headAccent.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(headAccent.localScale, Is.EqualTo(Vector3.one));
                Assert.That(headAccentRenderer.sortingOrder, Is.GreaterThan(headRenderer.sortingOrder));
                Assert.That(firePoint.localPosition.x, Is.EqualTo(0.72f).Within(0.001f));

                TextureImporter headImporter = AssetImporter.GetAtPath(expectedHeadSpritePath) as TextureImporter;
                TextureImporter headAccentImporter = AssetImporter.GetAtPath(expectedHeadAccentSpritePath) as TextureImporter;
                Assert.That(headImporter, Is.Not.Null);
                Assert.That(headAccentImporter, Is.Not.Null);
                Assert.That(headImporter.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(headAccentImporter.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                TextureImporterSettings headImporterSettings = new TextureImporterSettings();
                TextureImporterSettings headAccentImporterSettings = new TextureImporterSettings();
                headImporter.ReadTextureSettings(headImporterSettings);
                headAccentImporter.ReadTextureSettings(headAccentImporterSettings);
                Assert.That(headImporterSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                Assert.That(headAccentImporterSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                Assert.That(headImporter.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(headAccentImporter.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                expectedAccentRendererCount = 2;
            }
            else if (prefabPath == LaserTowerPrefabPath)
            {
                const float expectedScale = 277f / 335f;
                Transform headAccent = head.Find("HeadAccent");
                Assert.That(headAccent, Is.Not.Null, "The Laser Tower head accent is missing.");
                headAccentRenderer = headAccent.GetComponent<SpriteRenderer>();
                Assert.That(headAccentRenderer, Is.Not.Null, "The Laser Tower head accent needs a sprite renderer.");
                Assert.That(headAccentRenderer.sprite, Is.Not.Null, "The Laser Tower head accent sprite is missing.");
                Assert.That(AssetDatabase.GetAssetPath(headRenderer.sprite), Is.EqualTo(LaserTowerHeadSpritePath));
                Assert.That(AssetDatabase.GetAssetPath(headAccentRenderer.sprite), Is.EqualTo(LaserTowerHeadAccentSpritePath));
                Assert.That(headRenderer.sprite.rect, Is.EqualTo(headAccentRenderer.sprite.rect));
                Assert.That(head.localPosition, Is.EqualTo(new Vector3(0.18f, 0f, 0f)));
                Assert.That(head.localScale.x, Is.EqualTo(expectedScale).Within(0.0001f));
                Assert.That(head.localScale.y, Is.EqualTo(expectedScale).Within(0.0001f));
                Assert.That(headAccent.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(headAccent.localScale, Is.EqualTo(Vector3.one));
                Assert.That(headAccentRenderer.sortingOrder, Is.GreaterThan(headRenderer.sortingOrder));
                Assert.That(firePoint.localPosition.x, Is.EqualTo(0.64f).Within(0.001f));

                TextureImporter headImporter = AssetImporter.GetAtPath(LaserTowerHeadSpritePath) as TextureImporter;
                TextureImporter headAccentImporter = AssetImporter.GetAtPath(LaserTowerHeadAccentSpritePath) as TextureImporter;
                Assert.That(headImporter, Is.Not.Null);
                Assert.That(headAccentImporter, Is.Not.Null);
                Assert.That(headImporter.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(headAccentImporter.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                TextureImporterSettings headImporterSettings = new TextureImporterSettings();
                TextureImporterSettings headAccentImporterSettings = new TextureImporterSettings();
                headImporter.ReadTextureSettings(headImporterSettings);
                headAccentImporter.ReadTextureSettings(headAccentImporterSettings);
                Assert.That(headImporterSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                Assert.That(headAccentImporterSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                Assert.That(headImporter.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(headAccentImporter.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                expectedAccentRendererCount = 2;
            }

            SerializedObject serializedTower = new SerializedObject(tower);
            Assert.That(serializedTower.FindProperty("turretPivot").objectReferenceValue, Is.EqualTo(turretPivot));
            Assert.That(serializedTower.FindProperty("firePoint").objectReferenceValue, Is.EqualTo(firePoint));
            SerializedProperty accentRenderers = serializedTower.FindProperty("ammunitionAccentRenderers");
            Assert.That(accentRenderers.arraySize, Is.EqualTo(expectedAccentRendererCount));
            Assert.That(accentRenderers.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(accentRenderer));
            if (headAccentRenderer != null)
                Assert.That(accentRenderers.GetArrayElementAtIndex(1).objectReferenceValue, Is.EqualTo(headAccentRenderer));
        }

        [TestCase(BasicTowerPrefabPath, BasicTowerDataPath, 1f, 1f, 1f)]
        [TestCase(BasicTowerPrefabPath, LaserTowerDataPath, 0.33333334f, 0.8745098f, 1f)]
        [TestCase(LaserTowerPrefabPath, LaserTowerDataPath, 0.33333334f, 0.8745098f, 1f)]
        [TestCase(LongRangeTowerPrefabPath, LongRangeTowerDataPath, 1f, 1f, 1f)]
        public void TowerInitialization_AppliesActiveAmmunitionColor(
            string prefabPath,
            string towerDataPath,
            float red,
            float green,
            float blue)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            TowerData towerData = AssetDatabase.LoadAssetAtPath<TowerData>(towerDataPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(towerData, Is.Not.Null);

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Tower tower = instance.GetComponent<Tower>();
                SpriteRenderer accentRenderer = instance.transform.Find("BaseAccent").GetComponent<SpriteRenderer>();
                Transform headAccent = instance.transform.Find("TurretPivot/Head/HeadAccent");

                tower.Initialize(towerData, towerData.upgradeData);

                Color expectedColor = new Color(red, green, blue, 1f);
                Assert.That(accentRenderer.color, Is.EqualTo(expectedColor));
                if (headAccent != null)
                    Assert.That(headAccent.GetComponent<SpriteRenderer>().color, Is.EqualTo(expectedColor));
            }
            finally
            {
                Object.DestroyImmediate(instance);
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
