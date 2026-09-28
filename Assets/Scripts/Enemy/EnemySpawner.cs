using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TD.Core;
using TD.Grid;

namespace TD.Enemies
{
    [Serializable]
    public class EnemySpawnEntry
    {
        [Header("Enemy")]
        public string label = "Enemy";
        public EnemyData enemyData;
        public GameObject enemyPrefab;

        [Header("Round Rules")]
        [Min(1)] public int firstRound = 1;
        [Min(0)] public int baseAmount = 5;
        [Min(0)] public int amountPerRound = 1;

        [Tooltip("Seconds until the next spawn of this enemy.")]
        [Min(0.05f)] public float spawnInterval = 2f;
    }

    public class EnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GameState gameState;

        [Header("Fallback Single Enemy")]
        [SerializeField] private EnemyData enemyData;
        [SerializeField] private GameObject enemyPrefab;

        [Tooltip("Seconds between spawns.")]
        [SerializeField] private float spawnInterval = 2f;

        [Tooltip("When enabled, spawning starts automatically once a grid exists.")]
        [SerializeField] private bool startAutomatically = true;

        [Tooltip("Pause between rounds.")]
        [SerializeField] private float timeBetweenRounds = 5f;

        [Header("Round Enemy Types")]
        [SerializeField] private List<EnemySpawnEntry> spawnEntries = new List<EnemySpawnEntry>();

        private List<EnemyPath> cachedPaths = new List<EnemyPath>();
        private readonly Dictionary<Enemy, int> enemyPathIndices = new Dictionary<Enemy, int>();
        private readonly Queue<EnemySpawnEntry> spawnQueue = new Queue<EnemySpawnEntry>();
        private readonly List<Enemy> spawnedEnemies = new List<Enemy>();
        private float spawnTimer;
        private float roundBreakTimer;
        private bool isSpawning;
        private Coroutine startRoutine;
        private int currentRound = 1;
        private int aliveEnemies;
        private int nextPathIndex;
        private bool subscribedToGridPathChanged;
        private bool subscribedToMatchEnded;

        private void Start()
        {
            if (gameState == null)
                gameState = GameState.GetOrCreate();

            SubscribeToGameState();

            if (startAutomatically && !GameSession.IsMapEditorSession)
                BeginSpawning();
        }

        private void Update()
        {
            if (!isSpawning || cachedPaths.Count == 0 || gameState == null || !gameState.IsPlaying)
                return;

            if (spawnQueue.Count > 0)
            {
                spawnTimer -= Time.deltaTime;

                if (spawnTimer <= 0f)
                {
                    EnemySpawnEntry nextSpawn = spawnQueue.Dequeue();
                    SpawnEnemy(nextSpawn);
                    spawnTimer = nextSpawn.spawnInterval;
                }

                return;
            }

            if (aliveEnemies <= 0)
            {
                if (currentRound >= gameState.MaxRounds)
                {
                    gameState.Win();
                    return;
                }

                roundBreakTimer -= Time.deltaTime;

                if (roundBreakTimer <= 0f)
                    StartRound(currentRound + 1);
            }
        }

        public void BeginSpawning()
        {
            if (gameState == null)
                gameState = GameState.GetOrCreate();

            SubscribeToGameState();

            if (!gameState.IsPlaying)
                return;

            if (startRoutine != null)
                StopCoroutine(startRoutine);

            startRoutine = StartCoroutine(StartSpawningWhenGridIsReady());
        }

        public void RestartSpawningFromRound(int round)
        {
            if (gameState == null)
                gameState = GameState.GetOrCreate();

            GameplayLifecycle.StopCombat();
            StopSpawning(true);
            gameState.ResetState(round);
            BeginSpawning();
        }

        public void StopSpawning(bool clearEnemies)
        {
            if (startRoutine != null)
            {
                StopCoroutine(startRoutine);
                startRoutine = null;
            }

            spawnQueue.Clear();
            cachedPaths.Clear();
            enemyPathIndices.Clear();
            nextPathIndex = 0;
            spawnTimer = 0f;
            roundBreakTimer = 0f;
            aliveEnemies = 0;
            isSpawning = false;

            if (clearEnemies)
                ClearSpawnedEnemies();
        }

        private IEnumerator StartSpawningWhenGridIsReady()
        {
            if (gridManager == null)
            {
                Debug.LogError("EnemySpawner: GridManager is missing.");
                startRoutine = null;
                yield break;
            }

            while (!gridManager.HasGrid)
            {
                if (gameState == null || !gameState.IsPlaying)
                {
                    startRoutine = null;
                    yield break;
                }

                yield return null;
            }

            BuildPath();
            currentRound = gameState != null ? gameState.CurrentRound : 1;

            if (cachedPaths.Count > 0)
                StartRound(currentRound);

            startRoutine = null;
        }

        private void StartRound(int round)
        {
            if (gameState == null || !gameState.IsPlaying)
                return;

            if (round > gameState.MaxRounds)
            {
                gameState.Win();
                return;
            }

            currentRound = Mathf.Max(1, round);
            gameState?.SetRound(currentRound);

            BuildSpawnQueueForRound();

            if (spawnQueue.Count == 0)
            {
                Debug.LogError("EnemySpawner: No valid enemy spawn entries configured.");
                isSpawning = false;
                return;
            }

            spawnTimer = 0f;
            roundBreakTimer = timeBetweenRounds;
            isSpawning = true;
        }

        private void BuildPath()
        {
            if (gridManager == null)
            {
                Debug.LogError("EnemySpawner: GridManager is missing.");
                cachedPaths.Clear();
                return;
            }

            // Preserve all ordered paths cached by GridManager. SpawnEnemy distributes new
            // enemies across them in round-robin order.
            if (!TryCreatePaths(out List<EnemyPath> paths))
            {
                Debug.LogError("EnemySpawner: No path from start to goal found.");
                cachedPaths.Clear();
                return;
            }
            cachedPaths = paths;
            nextPathIndex = 0;

            if (!subscribedToGridPathChanged)
            {
                gridManager.OnPathChanged += HandleGridPathChanged;
                subscribedToGridPathChanged = true;
            }
        }

        private void HandleGridPathChanged()
        {
            if (gridManager == null)
                return;

            if (!TryCreatePaths(out List<EnemyPath> newPaths))
            {
                Debug.LogError("EnemySpawner: The updated grid has no valid enemy path. Spawning was stopped.");
                cachedPaths.Clear();
                spawnQueue.Clear();
                isSpawning = false;
                return;
            }

            cachedPaths = newPaths;

            // Keep each active enemy on its assigned route when paths are refreshed.
            for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = spawnedEnemies[i];
                if (enemy == null)
                {
                    spawnedEnemies.RemoveAt(i);
                    if (!ReferenceEquals(enemy, null))
                        enemyPathIndices.Remove(enemy);
                    continue;
                }

                if (!enemyPathIndices.TryGetValue(enemy, out int pathIndex))
                    pathIndex = i;
                pathIndex %= cachedPaths.Count;
                enemyPathIndices[enemy] = pathIndex;
                enemy.SetWaypoints(cachedPaths[pathIndex]);
            }
        }

        private bool TryCreatePaths(out List<EnemyPath> paths)
        {
            paths = new List<EnemyPath>();
            int pathCount = gridManager != null ? gridManager.CachedEnemyPathCount : 0;
            for (int pathIndex = 0; pathIndex < pathCount; pathIndex++)
            {
                IReadOnlyList<Vector3> worldPath = gridManager.GetCachedEnemyPathWorld(pathIndex);
                if (worldPath == null || worldPath.Count == 0)
                    return false;
                paths.Add(new EnemyPath(worldPath));
            }

            if (paths.Count == 0)
                return false;
            return true;
        }

        private void OnDestroy()
        {
            if (subscribedToGridPathChanged && gridManager != null)
            {
                gridManager.OnPathChanged -= HandleGridPathChanged;
                subscribedToGridPathChanged = false;
            }

            if (subscribedToMatchEnded && gameState != null)
            {
                gameState.OnMatchEnded -= HandleMatchEnded;
                subscribedToMatchEnded = false;
            }
        }

        private void SubscribeToGameState()
        {
            if (subscribedToMatchEnded || gameState == null)
                return;

            gameState.OnMatchEnded += HandleMatchEnded;
            subscribedToMatchEnded = true;
        }

        private void HandleMatchEnded(MatchState _)
        {
            StopSpawning(true);
        }

        private void BuildSpawnQueueForRound()
        {
            EnemySpawnEntry fallback = null;
            if (enemyData != null && enemyPrefab != null)
            {
                fallback = new EnemySpawnEntry
                {
                    label = "Fallback Enemy",
                    enemyData = enemyData,
                    enemyPrefab = enemyPrefab,
                    firstRound = 1,
                    baseAmount = 5,
                    amountPerRound = 1,
                    spawnInterval = spawnInterval
                };
            }

            WavePlanner.BuildRound(spawnQueue, spawnEntries, currentRound, GameSession.SelectedDifficulty, fallback);
        }

        private void SpawnEnemy(EnemySpawnEntry spawnEntry)
        {
            if (cachedPaths.Count == 0)
            {
                Debug.LogError("EnemySpawner: Cannot spawn an enemy without a valid path.");
                spawnQueue.Clear();
                isSpawning = false;
                return;
            }

            if (!WavePlanner.IsValid(spawnEntry))
            {
                Debug.LogError("EnemySpawner: Invalid spawn entry.");
                return;
            }

            EnemyPath path = TakeNextSpawnPath(out int pathIndex);

            GameObject enemyObject = PrefabPool.Spawn(spawnEntry.enemyPrefab, path[0], Quaternion.identity);
            Enemy enemy = enemyObject.GetComponent<Enemy>();

            if (enemy == null)
            {
                Debug.LogWarning("EnemySpawner: Enemy prefab has no Enemy component.");
                PrefabPool.Release(enemyObject);
                return;
            }

            enemy.Initialize(spawnEntry.enemyData, path);
            enemy.OnDied += HandleEnemyDied;
            enemy.OnReachedGoal += HandleEnemyReachedGoal;
            spawnedEnemies.Add(enemy);
            enemyPathIndices[enemy] = pathIndex;
            aliveEnemies++;
        }

        internal EnemyPath TakeNextSpawnPath(out int pathIndex)
        {
            if (cachedPaths.Count == 0)
            {
                pathIndex = -1;
                return null;
            }

            pathIndex = nextPathIndex % cachedPaths.Count;
            nextPathIndex = (pathIndex + 1) % cachedPaths.Count;
            return cachedPaths[pathIndex];
        }

        private void HandleEnemyDied(Enemy enemy)
        {
            UnregisterEnemy(enemy);
            aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
            gameState?.AddMoney(enemy.Reward);
        }

        private void HandleEnemyReachedGoal(Enemy enemy)
        {
            int goalDamage = enemy != null ? enemy.GoalDamage : 1;
            UnregisterEnemy(enemy);
            aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
            gameState?.DamageBase(goalDamage);
        }

        private void UnregisterEnemy(Enemy enemy)
        {
            if (enemy == null)
                return;

            enemy.OnDied -= HandleEnemyDied;
            enemy.OnReachedGoal -= HandleEnemyReachedGoal;
            enemyPathIndices.Remove(enemy);
            spawnedEnemies.Remove(enemy);
        }

        private void ClearSpawnedEnemies()
        {
            // Iterate backwards because UnregisterEnemy removes from spawnedEnemies.
            for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = spawnedEnemies[i];
                if (enemy == null)
                    continue;

                UnregisterEnemy(enemy);
                PrefabPool.Release(enemy.gameObject);
            }

            spawnedEnemies.Clear();
            enemyPathIndices.Clear();

            // Also remove stray enemies from earlier runs that are no longer tracked.
            Enemy[] strays = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
            for (int i = 0; i < strays.Length; i++)
            {
                if (strays[i] != null)
                    PrefabPool.Release(strays[i].gameObject);
            }
        }
    }
}
