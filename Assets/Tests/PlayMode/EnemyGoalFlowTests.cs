using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TD.Core;
using TD.Enemies;
using TD.Grid;
using TD.Level;

namespace TD.Tests.PlayMode
{
    public class EnemyGoalFlowTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PrefabPool.Clear();

            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.Destroy(cleanup[index]);
            }

            cleanup.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnedEnemy_ReachingGoal_RemovesOneLifeOnce()
        {
            GameState gameState = CreateComponent<GameState>("GameState");

            GridManager gridManager = CreateComponent<GridManager>("GridManager");
            gridManager.BuildGrid(new LevelMapDefinition
            {
                width = 2,
                height = 1,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(1, 0)
            });

            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            cleanup.Add(enemyData);
            enemyData.maxHealth = 1f;
            enemyData.speed = 100f;
            enemyData.goalDamage = 1;

            GameObject enemyPrefab = new GameObject("EnemyPrefab");
            cleanup.Add(enemyPrefab);
            enemyPrefab.SetActive(false);
            enemyPrefab.AddComponent<Enemy>();

            GameObject spawnerObject = new GameObject("EnemySpawner");
            cleanup.Add(spawnerObject);
            spawnerObject.SetActive(false);
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
            SetField(spawner, "gridManager", gridManager);
            SetField(spawner, "gameState", gameState);
            SetField(spawner, "startAutomatically", false);
            SetField(spawner, "timeBetweenRounds", 999f);
            SetField(spawner, "spawnEntries", new List<EnemySpawnEntry>
            {
                new EnemySpawnEntry
                {
                    label = "Goal test enemy",
                    enemyData = enemyData,
                    enemyPrefab = enemyPrefab,
                    firstRound = 1,
                    baseAmount = 1,
                    amountPerRound = 0,
                    spawnInterval = 0.05f
                }
            });

            int livesEventCount = 0;
            gameState.OnLivesChanged += _ => livesEventCount++;

            spawnerObject.SetActive(true);
            yield return null;
            spawner.BeginSpawning();

            float timeout = Time.realtimeSinceStartup + 2f;
            while (gameState.Lives == gameState.StartingLives && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.That(gameState.Lives, Is.EqualTo(gameState.StartingLives - 1));
            Assert.That(livesEventCount, Is.EqualTo(1));
            Assert.That(gameState.State, Is.EqualTo(MatchState.Playing));
        }

        private T CreateComponent<T>(string objectName) where T : Component
        {
            GameObject gameObject = new GameObject(objectName);
            cleanup.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private static void SetField<T>(EnemySpawner target, string fieldName, T value)
        {
            FieldInfo field = typeof(EnemySpawner).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"EnemySpawner field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}
