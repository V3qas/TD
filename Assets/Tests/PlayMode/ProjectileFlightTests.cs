using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TD.Bullets;
using TD.Combat;
using TD.Core;
using TD.Enemies;
using TD.Towers;

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
            Enemy enemy = CreateEnemy(new Vector3(1005f, 1000f, 0f), 2f);
            BulletData bulletData = CreateBulletData(40f);
            Bullet bullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            bullet.Initialize(bulletData, 10f, enemy);
            Vector3 launchDestination = bullet.Destination;

            enemy.transform.position = new Vector3(1005f, 1004f, 0f);
            Assert.That(bullet.Destination, Is.EqualTo(launchDestination),
                "A projectile must keep the destination calculated at launch instead of homing toward its target.");
            yield return WaitUntilReleased(bullet);

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

        [UnityTest]
        public IEnumerator Splash_DamagesMultiColliderEnemyOnlyOnce()
        {
            Enemy enemy = CreateEnemy(new Vector3(1003f, 1000f, 0f), 0f);
            enemy.gameObject.AddComponent<BoxCollider2D>();
            GameObject child = new GameObject("ExtraHitbox");
            child.transform.SetParent(enemy.transform, false);
            child.AddComponent<CircleCollider2D>().radius = 0.2f;
            BulletData bulletData = CreateBulletData(40f);
            bulletData.splashRadius = 2f;
            Bullet bullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            bullet.Initialize(bulletData, 10f, enemy);

            yield return WaitUntilReleased(bullet);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator MatchEnd_ClearsInFlightProjectileBeforeItCanHit()
        {
            GameState state = new GameObject("ProjectileMatchState").AddComponent<GameState>();
            cleanup.Add(state.gameObject);
            Enemy enemy = CreateEnemy(new Vector3(1003f, 1000f, 0f), 0f);
            Bullet bullet = CreateBullet(new Vector3(1000f, 1000f, 0f));
            bullet.Initialize(CreateBulletData(1f), 10f, enemy);
            state.Lose();

            Assert.That(bullet.gameObject.activeSelf, Is.False);
            yield return null;
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator DisabledProjectile_ReleasesReservedDamageForRetargeting()
        {
            Vector3 towerPosition = new Vector3(1000f, 1000f, 0f);
            Enemy enemy = CreateEnemy(towerPosition + Vector3.right * 3f, 0f);
            BulletData bulletData = CreateBulletData(1f);
            Bullet first = CreateBullet(towerPosition);
            first.Initialize(bulletData, 60f, enemy);
            Bullet second = CreateBullet(towerPosition);
            second.Initialize(bulletData, 60f, enemy);

            Assert.That(DefaultTargetProvider.Instance.FindTarget(towerPosition, 10f), Is.Null);

            second.gameObject.SetActive(false);
            yield return null;

            Assert.That(DefaultTargetProvider.Instance.FindTarget(towerPosition, 10f), Is.SameAs(enemy));
        }

        [UnityTest]
        public IEnumerator ProjectileObstructedByEnemy_DoesNotCoverIntendedTarget()
        {
            Vector3 origin = new Vector3(1000f, 1000f, 0f);
            CreateEnemy(origin + Vector3.right, 0f);
            Enemy intendedTarget = CreateEnemy(origin + Vector3.right * 3f, 0f);
            Bullet bullet = CreateBullet(origin);
            bullet.Initialize(CreateBulletData(10f), 100f, intendedTarget);

            Assert.That(PendingDamageReservations.IsLethallyCovered(intendedTarget), Is.False,
                "Damage intercepted by a nearer enemy must not suppress fire at the intended target.");

            yield return WaitUntilReleased(bullet);
            Assert.That(intendedTarget.CurrentHealth, Is.EqualTo(100f));
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
