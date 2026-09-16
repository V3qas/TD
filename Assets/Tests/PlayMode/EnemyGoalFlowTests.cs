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
            GridManager gridManager = CreateTwoCellGrid();
            EnemyData enemyData = CreateEnemyData(100f);
            EnemySpawner spawner = CreateSpawner(gameState, gridManager, enemyData, CreateEnemyPrefab());

            int livesEventCount = 0;
            gameState.OnLivesChanged += _ => livesEventCount++;

            spawner.gameObject.SetActive(true);
            yield return null;
            spawner.BeginSpawning();

            float timeout = Time.realtimeSinceStartup + 2f;
            while (gameState.Lives == gameState.StartingLives && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.That(gameState.Lives, Is.EqualTo(gameState.StartingLives - 1));
            Assert.That(livesEventCount, Is.EqualTo(1));
            Assert.That(gameState.State, Is.EqualTo(MatchState.Playing));
        }

        [UnityTest]
        public IEnumerator FinalRound_WaitsForLastEnemy_ThenWinsOnceWithoutRoundSix()
        {
            GameState gameState = CreateComponent<GameState>("GameState");
            gameState.SetRound(gameState.MaxRounds);

            GridManager gridManager = CreateTwoCellGrid();
            EnemyData enemyData = CreateEnemyData(0f);
            EnemySpawner spawner = CreateSpawner(gameState, gridManager, enemyData, CreateEnemyPrefab());

            int matchEndCount = 0;
            gameState.OnMatchEnded += _ => matchEndCount++;

            spawner.gameObject.SetActive(true);
            yield return null;
            spawner.BeginSpawning();

            Enemy spawnedEnemy = null;
            float timeout = Time.realtimeSinceStartup + 2f;
            while (spawnedEnemy == null && Time.realtimeSinceStartup < timeout)
            {
                spawnedEnemy = FindActiveEnemy(enemyData);
                yield return null;
            }

            Assert.That(spawnedEnemy, Is.Not.Null, "The final-round enemy was not spawned.");
            Assert.That(gameState.CurrentRound, Is.EqualTo(gameState.MaxRounds));
            Assert.That(gameState.State, Is.EqualTo(MatchState.Playing),
                "Victory must wait until the last active enemy is gone.");

            spawnedEnemy.TakeDamage(spawnedEnemy.MaxHealth);

            timeout = Time.realtimeSinceStartup + 2f;
            while (gameState.State == MatchState.Playing && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.That(gameState.State, Is.EqualTo(MatchState.Won));
            Assert.That(matchEndCount, Is.EqualTo(1));
            Assert.That(gameState.CurrentRound, Is.EqualTo(gameState.MaxRounds));

            spawner.BeginSpawning();
            yield return null;

            Assert.That(FindActiveEnemy(enemyData), Is.Null, "No enemy may spawn after match end.");
            Assert.That(matchEndCount, Is.EqualTo(1));
        }

        private GridManager CreateTwoCellGrid()
        {
            GridManager gridManager = CreateComponent<GridManager>("GridManager");
            gridManager.BuildGrid(new LevelMapDefinition
            {
                width = 2,
                height = 1,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(1, 0)
            });
            return gridManager;
        }

        private EnemyData CreateEnemyData(float speed)
        {
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            cleanup.Add(enemyData);
            enemyData.maxHealth = 1f;
            enemyData.speed = speed;
            enemyData.goalDamage = 1;
            return enemyData;
        }

        private GameObject CreateEnemyPrefab()
        {
            GameObject enemyPrefab = new GameObject("EnemyPrefab");
            cleanup.Add(enemyPrefab);
            enemyPrefab.SetActive(false);
            enemyPrefab.AddComponent<Enemy>();
            return enemyPrefab;
        }

        private EnemySpawner CreateSpawner(
            GameState gameState,
            GridManager gridManager,
            EnemyData enemyData,
            GameObject enemyPrefab)
        {
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
                    label = "PlayMode test enemy",
                    enemyData = enemyData,
                    enemyPrefab = enemyPrefab,
                    firstRound = 1,
                    baseAmount = 1,
                    amountPerRound = 0,
                    spawnInterval = 0.05f
                }
            });
            return spawner;
        }

        private static Enemy FindActiveEnemy(EnemyData enemyData)
        {
            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            for (int index = 0; index < enemies.Count; index++)
            {
                Enemy enemy = enemies[index];
                if (enemy != null && enemy.Data == enemyData)
                    return enemy;
            }

            return null;
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
