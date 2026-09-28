using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TD.Core;
using TD.Enemies;
using TD.Grid;
using TD.Level;

namespace TD.Tests.EditMode
{
    public class WavePlannerTests
    {
        private EnemyData enemyData;
        private GameObject dummyPrefab;

        [SetUp]
        public void SetUp()
        {
            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            dummyPrefab = new GameObject("DummyPrefab");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(dummyPrefab);
            Object.DestroyImmediate(enemyData);
        }

        // ── IsValid ───────────────────────────────────────────────────────────

        [Test]
        public void IsValid_NullEntry_ReturnsFalse()
        {
            Assert.IsFalse(WavePlanner.IsValid(null));
        }

        [Test]
        public void IsValid_MissingEnemyData_ReturnsFalse()
        {
            var entry = new EnemySpawnEntry { enemyPrefab = dummyPrefab };
            Assert.IsFalse(WavePlanner.IsValid(entry));
        }

        [Test]
        public void IsValid_MissingPrefab_ReturnsFalse()
        {
            var entry = new EnemySpawnEntry { enemyData = enemyData };
            Assert.IsFalse(WavePlanner.IsValid(entry));
        }

        [Test]
        public void IsValid_CompleteEntry_ReturnsTrue()
        {
            var entry = new EnemySpawnEntry { enemyData = enemyData, enemyPrefab = dummyPrefab };
            Assert.IsTrue(WavePlanner.IsValid(entry));
        }

        // ── BuildRound ────────────────────────────────────────────────────────

        [Test]
        public void BuildRound_ValidEntry_PopulatesQueue()
        {
            var output = new Queue<EnemySpawnEntry>();
            var entry = new EnemySpawnEntry
            {
                enemyData = enemyData, enemyPrefab = dummyPrefab,
                firstRound = 1, baseAmount = 5, amountPerRound = 0
            };
            DifficultySettings normal = DifficultySettings.ForLevel(DifficultyLevel.Normal);

            WavePlanner.BuildRound(output, new List<EnemySpawnEntry> { entry }, 1, normal, null);

            Assert.AreEqual(5, output.Count);
        }

        [Test]
        public void BuildRound_EntryFirstRoundNotYetReached_IsExcluded()
        {
            var output = new Queue<EnemySpawnEntry>();
            var entry = new EnemySpawnEntry
            {
                enemyData = enemyData, enemyPrefab = dummyPrefab,
                firstRound = 3, baseAmount = 5, amountPerRound = 0
            };
            DifficultySettings normal = DifficultySettings.ForLevel(DifficultyLevel.Normal);

            WavePlanner.BuildRound(output, new List<EnemySpawnEntry> { entry }, 1, normal, null);

            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void BuildRound_AmountMultiplier_ScalesBaseAmount()
        {
            var output = new Queue<EnemySpawnEntry>();
            var entry = new EnemySpawnEntry
            {
                enemyData = enemyData, enemyPrefab = dummyPrefab,
                firstRound = 1, baseAmount = 5, amountPerRound = 0
            };
            var doubled = new DifficultySettings { amountMultiplier = 2f, amountScaleMultiplier = 1f };

            WavePlanner.BuildRound(output, new List<EnemySpawnEntry> { entry }, 1, doubled, null);

            // Max(1, RoundToInt(5 * 2)) = 10
            Assert.AreEqual(10, output.Count);
        }

        [Test]
        public void BuildRound_AmountPerRound_IncreasesEachRound()
        {
            var entry = new EnemySpawnEntry
            {
                enemyData = enemyData, enemyPrefab = dummyPrefab,
                firstRound = 1, baseAmount = 3, amountPerRound = 2
            };
            DifficultySettings normal = DifficultySettings.ForLevel(DifficultyLevel.Normal);

            var round1 = new Queue<EnemySpawnEntry>();
            WavePlanner.BuildRound(round1, new List<EnemySpawnEntry> { entry }, 1, normal, null);

            var round3 = new Queue<EnemySpawnEntry>();
            WavePlanner.BuildRound(round3, new List<EnemySpawnEntry> { entry }, 3, normal, null);

            // Round 1: 3 + (1-1)*2 = 3 ; Round 3: 3 + (3-1)*2 = 7
            Assert.AreEqual(3, round1.Count);
            Assert.AreEqual(7, round3.Count);
        }

        [Test]
        public void BuildRound_InvalidEntry_IsSkipped()
        {
            var output = new Queue<EnemySpawnEntry>();
            var invalid = new EnemySpawnEntry { enemyData = null, enemyPrefab = null, firstRound = 1, baseAmount = 5 };
            DifficultySettings normal = DifficultySettings.ForLevel(DifficultyLevel.Normal);

            WavePlanner.BuildRound(output, new List<EnemySpawnEntry> { invalid }, 1, normal, null);

            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void BuildRound_NoMatchingEntries_WithValidFallback_UsesFallback()
        {
            var output = new Queue<EnemySpawnEntry>();
            var futureEntry = new EnemySpawnEntry
            {
                enemyData = enemyData, enemyPrefab = dummyPrefab,
                firstRound = 10, baseAmount = 5, amountPerRound = 0
            };
            var fallback = new EnemySpawnEntry
            {
                enemyData = enemyData, enemyPrefab = dummyPrefab,
                firstRound = 1, baseAmount = 3, amountPerRound = 0
            };
            DifficultySettings normal = DifficultySettings.ForLevel(DifficultyLevel.Normal);

            WavePlanner.BuildRound(output, new List<EnemySpawnEntry> { futureEntry }, 1, normal, fallback);

            Assert.AreEqual(3, output.Count);
        }

        [Test]
        public void BuildRound_NoMatchingEntries_NoFallback_QueueIsEmpty()
        {
            var output = new Queue<EnemySpawnEntry>();
            DifficultySettings normal = DifficultySettings.ForLevel(DifficultyLevel.Normal);

            WavePlanner.BuildRound(output, new List<EnemySpawnEntry>(), 1, normal, null);

            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void SpawnEnemy_WithoutCachedPath_StopsCleanly()
        {
            EnemySpawner spawner = dummyPrefab.AddComponent<EnemySpawner>();
            MethodInfo spawnEnemy = typeof(EnemySpawner).GetMethod(
                "SpawnEnemy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(spawnEnemy, Is.Not.Null);

            var entry = new EnemySpawnEntry
            {
                enemyData = enemyData,
                enemyPrefab = dummyPrefab
            };

            LogAssert.Expect(LogType.Error, "EnemySpawner: Cannot spawn an enemy without a valid path.");
            Assert.DoesNotThrow(() => spawnEnemy.Invoke(spawner, new object[] { entry }));
        }

        [Test]
        public void SpawnerCyclesAcrossAllAuthoredPaths()
        {
            GameObject gridObject = new GameObject("GridManager");
            try
            {
                GridManager gridManager = gridObject.AddComponent<GridManager>();
                LevelMapDefinition definition = new LevelMapDefinition
                {
                    width = 3,
                    height = 3,
                    startCell = new Vector2Int(0, 1),
                    goalCell = new Vector2Int(2, 1),
                    pathSequences = new List<PathSequence>
                    {
                        new PathSequence(new List<Vector2Int>
                        {
                            new Vector2Int(0, 1), new Vector2Int(0, 2),
                            new Vector2Int(1, 2), new Vector2Int(2, 2),
                            new Vector2Int(2, 1)
                        }),
                        new PathSequence(new List<Vector2Int>
                        {
                            new Vector2Int(0, 1), new Vector2Int(0, 0),
                            new Vector2Int(1, 0), new Vector2Int(2, 0),
                            new Vector2Int(2, 1)
                        })
                    }
                };
                Assert.That(gridManager.BuildGrid(definition), Is.True);

                EnemySpawner spawner = dummyPrefab.AddComponent<EnemySpawner>();
                SetField(spawner, "gridManager", gridManager);
                MethodInfo buildPath = typeof(EnemySpawner).GetMethod(
                    "BuildPath",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(buildPath, Is.Not.Null);
                buildPath.Invoke(spawner, null);

                EnemyPath first = spawner.TakeNextSpawnPath(out int firstIndex);
                EnemyPath second = spawner.TakeNextSpawnPath(out int secondIndex);
                EnemyPath third = spawner.TakeNextSpawnPath(out int thirdIndex);

                Assert.That(new[] { firstIndex, secondIndex, thirdIndex }, Is.EqualTo(new[] { 0, 1, 0 }));
                Assert.That(first[1].y, Is.EqualTo(2.5f));
                Assert.That(second[1].y, Is.EqualTo(0.5f));
                Assert.That(third, Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(gridObject);
            }
        }

        private static void SetField<T>(EnemySpawner target, string fieldName, T value)
        {
            FieldInfo field = typeof(EnemySpawner).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
