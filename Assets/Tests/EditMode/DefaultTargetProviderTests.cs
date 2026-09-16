using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Combat;
using TD.Enemies;
using TD.Towers;

namespace TD.Tests.EditMode
{
    public class DefaultTargetProviderTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<EnemyData> enemyDataAssets = new List<EnemyData>();
        private EnemyData enemyData;

        [SetUp]
        public void SetUp()
        {
            enemyData = CreateEnemyData(100f, 2f);
            Destructible.ClearMarkedTargets();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            for (int i = enemyDataAssets.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(enemyDataAssets[i]);
            enemyDataAssets.Clear();
            Destructible.ClearMarkedTargets();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Enemy SpawnEnemy(Vector3 position, EnemyData data = null)
        {
            GameObject go = new GameObject("TestEnemy");
            go.transform.position = position;
            created.Add(go);
            Enemy enemy = go.AddComponent<Enemy>();
            enemy.Initialize(data != null ? data : enemyData, new List<Vector3> { position, position + Vector3.right });
            return enemy;
        }

        private Enemy SpawnEnemyOnPath(float x, float maxHealth, float speed, float damage = 0f)
        {
            EnemyData data = CreateEnemyData(maxHealth, speed);
            List<Vector3> path = new List<Vector3> { Vector3.zero, Vector3.right * 10f };

            GameObject go = new GameObject("TestEnemy");
            go.transform.position = new Vector3(x, 0f, 0f);
            created.Add(go);

            Enemy enemy = go.AddComponent<Enemy>();
            enemy.Initialize(data, path);
            enemy.SetWaypoints(path);
            if (damage > 0f)
                enemy.TakeDamage(damage);
            return enemy;
        }

        private EnemyData CreateEnemyData(float maxHealth, float speed)
        {
            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            data.maxHealth = maxHealth;
            data.speed = speed;
            enemyDataAssets.Add(data);
            return data;
        }

        private Destructible SpawnMarkedDestructible(Vector3 position)
        {
            GameObject go = new GameObject("TestDestructible");
            go.transform.position = position;
            created.Add(go);
            Destructible d = go.AddComponent<Destructible>();
            // null SpriteRenderer is safe - UpdateMarkedVisual has a null guard.
            d.Initialize(100, 10, null);
            d.Mark();
            return d;
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void FindTarget_EmptyRegistries_ReturnsNull()
        {
            IDamageable result = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 10f);
            Assert.IsNull(result);
        }

        [Test]
        public void FindTarget_EnemyInRange_ReturnsEnemy()
        {
            Enemy enemy = SpawnEnemy(new Vector3(1f, 0f, 0f));

            IDamageable result = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 10f);

            Assert.AreSame(enemy, result);
        }

        [Test]
        public void FindTarget_EnemyOutOfRange_ReturnsNull()
        {
            SpawnEnemy(new Vector3(100f, 0f, 0f));

            IDamageable result = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 5f);

            Assert.IsNull(result);
        }

        [Test]
        public void FindTarget_MarkedDestructible_TakesPriorityOverEnemy()
        {
            // Both at the same distance within range.
            SpawnEnemy(new Vector3(1f, 0f, 0f));
            Destructible marked = SpawnMarkedDestructible(new Vector3(1f, 0f, 0f));

            IDamageable result = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 10f);

            Assert.AreSame(marked, result);
        }

        [Test]
        public void FindTarget_MarkedDestructibleOutOfRange_FallsBackToEnemy()
        {
            SpawnEnemy(new Vector3(2f, 0f, 0f));
            SpawnMarkedDestructible(new Vector3(100f, 0f, 0f));

            IDamageable result = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 5f);

            Assert.IsInstanceOf<Enemy>(result);
        }

        [Test]
        public void FindTarget_FirstAndLast_UsePathProgress()
        {
            Enemy early = SpawnEnemyOnPath(2f, 100f, 2f);
            Enemy advanced = SpawnEnemyOnPath(8f, 100f, 2f);

            IDamageable first = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 20f, TargetingMode.First);
            IDamageable last = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 20f, TargetingMode.Last);

            Assert.AreSame(advanced, first);
            Assert.AreSame(early, last);
        }

        [Test]
        public void FindTarget_HealthModes_UseCurrentHealth()
        {
            Enemy healthy = SpawnEnemyOnPath(2f, 100f, 2f);
            Enemy damaged = SpawnEnemyOnPath(3f, 100f, 2f, 75f);

            IDamageable highest = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 20f, TargetingMode.HighestHealth);
            IDamageable lowest = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 20f, TargetingMode.LowestHealth);

            Assert.AreSame(healthy, highest);
            Assert.AreSame(damaged, lowest);
        }

        [Test]
        public void FindTarget_Fastest_UsesCurrentEffectiveSpeed()
        {
            SpawnEnemyOnPath(2f, 100f, 2f);
            Enemy runner = SpawnEnemyOnPath(3f, 100f, 8f);

            IDamageable fastest = DefaultTargetProvider.Instance.FindTarget(Vector3.zero, 20f, TargetingMode.Fastest);

            Assert.AreSame(runner, fastest);
        }
    }
}
