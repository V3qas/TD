using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Bullets;
using TD.Core;
using TD.Enemies;

namespace TD.Tests.EditMode
{
    public class ProjectilePredictionTests
    {
        private GameObject enemyObject;
        private GameObject bulletObject;
        private EnemyData enemyData;
        private BulletData bulletData;

        [SetUp]
        public void SetUp()
        {
            GameSession.SelectDifficulty(new DifficultySettings());

            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.maxHealth = 100f;
            enemyData.speed = 8f;

            enemyObject = new GameObject("Runner");
            enemyObject.transform.position = new Vector3(5f, 0f, 0f);
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemy.Initialize(enemyData, new List<Vector3>
            {
                new Vector3(5f, 0f, 0f),
                new Vector3(5f, 10f, 0f)
            });

            bulletData = ScriptableObject.CreateInstance<BulletData>();
            bulletData.travelSpeed = 16f;

            bulletObject = new GameObject("Bullet");
            Bullet bullet = bulletObject.AddComponent<Bullet>();
            bullet.Initialize(bulletData, 10f, enemy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(bulletObject);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(bulletData);
            Object.DestroyImmediate(enemyData);
            GameSession.SelectDifficulty(new DifficultySettings());
        }

        [Test]
        public void Initialize_AimsAheadAndKeepsFixedDestination()
        {
            Bullet bullet = bulletObject.GetComponent<Bullet>();
            Vector3 launchDestination = bullet.Destination;

            Assert.That(launchDestination.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(launchDestination.y, Is.GreaterThan(0f));

            enemyObject.transform.position = new Vector3(8f, 4f, 0f);

            Assert.That(bullet.Destination, Is.EqualTo(launchDestination));
        }
    }
}
