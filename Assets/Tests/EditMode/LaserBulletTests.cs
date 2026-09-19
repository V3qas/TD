using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Bullets;
using TD.Core;
using TD.Enemies;

namespace TD.Tests.EditMode
{
    public class LaserBulletTests
    {
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private EnemyData enemyData;
        private BulletData bulletData;

        [SetUp]
        public void SetUp()
        {
            GameSession.SelectDifficulty(new DifficultySettings());

            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.maxHealth = 100f;
            enemyData.speed = 0f;

            bulletData = ScriptableObject.CreateInstance<BulletData>();
            bulletData.bulletType = BulletType.Laser;
            bulletData.damageMultiplier = 0.5f;
            bulletData.isPiercing = true;
            bulletData.maxTravelDistance = 20f;
            bulletData.beamWidth = 0.12f;
            bulletData.beamDuration = 0.1f;
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = spawnedObjects.Count - 1; index >= 0; index--)
            {
                if (spawnedObjects[index] != null)
                    Object.DestroyImmediate(spawnedObjects[index]);
            }

            Object.DestroyImmediate(bulletData);
            Object.DestroyImmediate(enemyData);
            GameSession.SelectDifficulty(new DifficultySettings());
        }

        [Test]
        public void Initialize_PiercesAllDamageablesAlongEntireBeamOnce()
        {
            Enemy first = CreateEnemy("First", new Vector3(3f, 0f, 0f));
            Enemy second = CreateEnemy("Second", new Vector3(8f, 0f, 0f));
            Enemy outsideBeam = CreateEnemy("OutsideBeam", new Vector3(5f, 2f, 0f));
            Bullet bullet = CreateBullet(Vector3.zero);

            bullet.Initialize(bulletData, 20f, first);

            Assert.That(first.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
            Assert.That(second.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
            Assert.That(outsideBeam.CurrentHealth, Is.EqualTo(100f).Within(0.001f));
            Assert.That(bullet.Destination, Is.EqualTo(new Vector3(20f, 0f, 0f)));
        }

        [Test]
        public void Initialize_StretchesVisualAcrossConfiguredBeamLength()
        {
            Enemy target = CreateEnemy("Target", new Vector3(3f, 0f, 0f));
            Bullet bullet = CreateBullet(Vector3.zero);

            bullet.Initialize(bulletData, 20f, target);

            Assert.That(bullet.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            Assert.That(bullet.transform.localScale.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(bullet.transform.localScale.y, Is.EqualTo(0.12f).Within(0.001f));
        }

        [Test]
        public void Initialize_DoesNotHitTargetBeyondBeamLength()
        {
            Enemy target = CreateEnemy("BeyondBeam", new Vector3(3f, 0f, 0f));
            bulletData.maxTravelDistance = 2f;
            Bullet bullet = CreateBullet(Vector3.zero);

            bullet.Initialize(bulletData, 20f, target);

            Assert.That(target.CurrentHealth, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void Initialize_NonPiercingLaserHitsFirstEnemyOnly()
        {
            Enemy first = CreateEnemy("First", new Vector3(3f, 0f, 0f));
            Enemy second = CreateEnemy("Second", new Vector3(8f, 0f, 0f));
            bulletData.isPiercing = false;
            Bullet bullet = CreateBullet(Vector3.zero);

            bullet.Initialize(bulletData, 20f, second);

            Assert.That(first.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
            Assert.That(second.CurrentHealth, Is.EqualTo(100f).Within(0.001f));
        }

        private Enemy CreateEnemy(string objectName, Vector3 position)
        {
            GameObject enemyObject = new GameObject(objectName);
            spawnedObjects.Add(enemyObject);
            enemyObject.transform.position = position;
            enemyObject.AddComponent<CircleCollider2D>();

            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemy.Initialize(enemyData, new List<Vector3>
            {
                position,
                position + Vector3.right
            });
            return enemy;
        }

        private Bullet CreateBullet(Vector3 position)
        {
            GameObject bulletObject = new GameObject("LaserBullet");
            spawnedObjects.Add(bulletObject);
            bulletObject.transform.position = position;
            bulletObject.AddComponent<SpriteRenderer>();
            return bulletObject.AddComponent<Bullet>();
        }
    }
}
