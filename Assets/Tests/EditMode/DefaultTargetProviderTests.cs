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
        private EnemyData enemyData;

        [SetUp]
        public void SetUp()
        {
            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            // enemyData.maxHealth defaults to 100f - no override needed.
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
            Object.DestroyImmediate(enemyData);
            Destructible.ClearMarkedTargets();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Enemy SpawnEnemy(Vector3 position)
        {
            GameObject go = new GameObject("TestEnemy");
            go.transform.position = position;
            created.Add(go);
            Enemy enemy = go.AddComponent<Enemy>();
            enemy.Initialize(enemyData, new List<Vector3> { position, position + Vector3.right });
            return enemy;
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
    }
}
