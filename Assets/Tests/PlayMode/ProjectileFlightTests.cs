using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TD.Bullets;
using TD.Core;
using TD.Enemies;

namespace TD.Tests.PlayMode
{
    public class ProjectileFlightTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSession.SelectDifficulty(new DifficultySettings());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.Destroy(cleanup[index]);
            }

            cleanup.Clear();
            GameSession.SelectDifficulty(new DifficultySettings());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Projectile_HitsEnemyAlongItsStraightPath()
        {
            Enemy enemy = CreateEnemy(new Vector3(1003f, 1000f, 0f), 0f);
            BulletData bulletData = CreateBulletData(40f);
            Bullet bullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            bullet.Initialize(bulletData, 10f, enemy);

            yield return WaitUntilReleased(bullet);

            Assert.That(enemy.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Projectile_MissesWhenEnemyLeavesTheStraightPath()
        {
            Enemy enemy = CreateEnemy(new Vector3(1005f, 1000f, 0f), 0f);
            BulletData bulletData = CreateBulletData(40f);
            Bullet bullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            bullet.Initialize(bulletData, 10f, enemy);
            Vector3 launchDestination = bullet.Destination;

            enemy.transform.position = new Vector3(1005f, 1004f, 0f);
            yield return WaitUntilReleased(bullet);

            Assert.That(launchDestination, Is.EqualTo(new Vector3(1005f, 1000f, 0f)));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ProjectileHit_AppliesSlowToSurvivingEnemy()
        {
            Enemy enemy = CreateEnemy(new Vector3(1003f, 1000f, 0f), 8f);
            BulletData bulletData = CreateBulletData(1000f);
            bulletData.slowFactor = 0.5f;
            bulletData.slowDuration = 5f;
            Bullet bullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            bullet.Initialize(bulletData, 10f, enemy);

            yield return WaitUntilReleased(bullet);

            Assert.That(enemy.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
            Assert.That(enemy.CurrentSpeed, Is.EqualTo(4f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator SlowedEnemy_ReceivesShorterLeadAtLaunch()
        {
            Enemy enemy = CreateEnemy(new Vector3(1005f, 1000f, 0f), 8f);
            BulletData bulletData = CreateBulletData(16f);
            Bullet normalBullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            normalBullet.Initialize(bulletData, 10f, enemy);
            Vector3 normalDestination = normalBullet.Destination;

            enemy.ApplySlow(0.25f, 5f);
            Bullet slowedBullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            slowedBullet.Initialize(bulletData, 10f, enemy);

            Assert.That(enemy.CurrentSpeed, Is.EqualTo(2f).Within(0.001f));
            Assert.That(slowedBullet.Destination.x, Is.EqualTo(normalDestination.x).Within(0.001f));
            Assert.That(slowedBullet.Destination.y, Is.LessThan(normalDestination.y));
            yield return null;
        }

        private Enemy CreateEnemy(Vector3 position, float speed)
        {
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            cleanup.Add(enemyData);
            enemyData.maxHealth = 100f;
            enemyData.speed = speed;

            GameObject enemyObject = new GameObject("ProjectileTestEnemy");
            cleanup.Add(enemyObject);
            enemyObject.transform.position = position;
            enemyObject.AddComponent<CircleCollider2D>().radius = 0.4f;
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemy.Initialize(enemyData, new List<Vector3>
            {
                position,
                position + Vector3.up * 10f
            });
            return enemy;
        }

        private BulletData CreateBulletData(float speed)
        {
            BulletData bulletData = ScriptableObject.CreateInstance<BulletData>();
            cleanup.Add(bulletData);
            bulletData.travelSpeed = speed;
            return bulletData;
        }

        private Bullet CreateBullet(Vector3 position)
        {
            GameObject bulletObject = new GameObject("ProjectileTestBullet");
            cleanup.Add(bulletObject);
            bulletObject.transform.position = position;
            CircleCollider2D collider = bulletObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.1f;
            collider.isTrigger = true;
            return bulletObject.AddComponent<Bullet>();
        }

        private static IEnumerator WaitUntilReleased(Bullet bullet)
        {
            float timeout = Time.realtimeSinceStartup + 2f;
            while (bullet != null && bullet.gameObject.activeInHierarchy && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.That(bullet == null || !bullet.gameObject.activeInHierarchy, Is.True,
                "The projectile did not finish its flight.");
        }
    }
}
