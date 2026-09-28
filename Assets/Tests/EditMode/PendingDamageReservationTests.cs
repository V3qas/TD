using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Bullets;
using TD.Combat;
using TD.Core;
using TD.Enemies;
using TD.Towers;

namespace TD.Tests.EditMode
{
    public class PendingDamageReservationTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameSession.EndMapEditorMode();
            Destructible.ClearMarkedTargets();
            PendingDamageReservations.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            }

            cleanup.Clear();
            Destructible.ClearMarkedTargets();
            PendingDamageReservations.Clear();
            GameSession.EndMapEditorMode();
        }

        [Test]
        public void CombinedProjectileDamage_MakesCoveredTargetUnavailable()
        {
            Enemy enemy = CreateEnemy(100f);
            PendingDamageReservations.Reservation first =
                PendingDamageReservations.Reserve(enemy, 60f, 1f);
            Assert.That(FindTarget(), Is.SameAs(enemy));

            PendingDamageReservations.Reservation second =
                PendingDamageReservations.Reserve(enemy, 60f, 2f);
            Assert.That(FindTarget(), Is.Null);

            PendingDamageReservations.Release(second);
            Assert.That(FindTarget(), Is.SameAs(enemy));

            PendingDamageReservations.Release(first);
        }

        [Test]
        public void ProjectileDamagePrediction_AccountsForShieldAndArmorPerHit()
        {
            Enemy enemy = CreateEnemy(100f, 50f, 10f);
            BulletData bulletData = CreateBulletData();

            CreateBullet().Initialize(bulletData, 100f, enemy);
            Assert.That(FindTarget(), Is.SameAs(enemy));

            CreateBullet().Initialize(bulletData, 100f, enemy);
            Assert.That(FindTarget(), Is.Null);
        }

        [Test]
        public void StaleReservation_DoesNotReleaseReservationFromReusedTarget()
        {
            Enemy enemy = CreateEnemy(100f);
            PendingDamageReservations.Reservation stale =
                PendingDamageReservations.Reserve(enemy, 100f, 2f);

            PendingDamageReservations.ReleaseAll(enemy);
            PendingDamageReservations.Reservation current =
                PendingDamageReservations.Reserve(enemy, 100f, 1f);

            PendingDamageReservations.Release(stale);
            Assert.That(PendingDamageReservations.IsLethallyCovered(enemy), Is.True);

            PendingDamageReservations.Release(current);
            Assert.That(PendingDamageReservations.IsLethallyCovered(enemy), Is.False);
        }

        [Test]
        public void ProjectileDamagePrediction_UsesEstimatedImpactOrder()
        {
            Enemy enemy = CreateEnemy(5f, 5f, 10f);

            PendingDamageReservations.Reserve(enemy, 5f, 2f);
            PendingDamageReservations.Reserve(enemy, 15f, 1f);

            Assert.That(PendingDamageReservations.IsLethallyCovered(enemy), Is.False,
                "The earlier 15-damage hit is fully mitigated before the later 5-damage hit arrives.");
        }

        [Test]
        public void DamageArrivingAfterGoal_DoesNotCoverTarget()
        {
            Enemy enemy = CreateEnemy(100f, speed: 10f, pathLength: 1f);

            PendingDamageReservations.Reserve(enemy, 60f, 0.05f);
            PendingDamageReservations.Reserve(enemy, 60f, 0.2f);

            Assert.That(PendingDamageReservations.IsLethallyCovered(enemy), Is.False,
                "Damage scheduled after the enemy reaches the goal must not suppress a timely shot.");
            Assert.That(FindTarget(), Is.SameAs(enemy));
        }

        [Test]
        public void NegativeArmor_IsClampedConsistentlyForPredictionAndDamage()
        {
            Enemy enemy = CreateEnemy(100f, armor: -10f);
            float[] incomingDamages = { 90f };

            Assert.That(enemy.WouldBeDestroyedBy(incomingDamages), Is.False);
            enemy.TakeDamage(90f);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(10f).Within(0.001f));
        }

        private IDamageable FindTarget()
        {
            return DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 10f);
        }

        private Enemy CreateEnemy(
            float health,
            float shield = 0f,
            float armor = 0f,
            float speed = 0f,
            float pathLength = 1f)
        {
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            cleanup.Add(enemyData);
            enemyData.maxHealth = health;
            enemyData.shield = shield;
            enemyData.armor = armor;
            enemyData.speed = speed;

            GameObject enemyObject = new GameObject("ReservationTarget");
            cleanup.Add(enemyObject);
            enemyObject.transform.position = Vector3.right;
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemy.Initialize(enemyData, new List<Vector3>
            {
                enemyObject.transform.position,
                enemyObject.transform.position + Vector3.right * pathLength
            });
            return enemy;
        }

        private BulletData CreateBulletData()
        {
            BulletData bulletData = ScriptableObject.CreateInstance<BulletData>();
            cleanup.Add(bulletData);
            bulletData.bulletType = BulletType.Projectile;
            bulletData.damageMultiplier = 1f;
            return bulletData;
        }

        private Bullet CreateBullet()
        {
            GameObject bulletObject = new GameObject("ReservedProjectile");
            cleanup.Add(bulletObject);
            return bulletObject.AddComponent<Bullet>();
        }
    }
}
