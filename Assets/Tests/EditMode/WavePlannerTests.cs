using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Core;
using TD.Enemies;

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
    }
}
