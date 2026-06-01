using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    private List<Vector3> cachedPath;
    private readonly Queue<EnemySpawnEntry> spawnQueue = new Queue<EnemySpawnEntry>();
    private readonly List<Enemy> spawnedEnemies = new List<Enemy>();
    private float spawnTimer;
    private float roundBreakTimer;
    private bool isSpawning;
    private Coroutine startRoutine;
    private int currentRound = 1;
    private int aliveEnemies;
    private bool subscribedToGridPathChanged;

    private void Start()
    {
        if (gameState == null)
            gameState = GameState.GetOrCreate();

        if (startAutomatically)
            BeginSpawning();
    }

    private void Update()
    {
        if (!isSpawning || cachedPath == null)
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
            roundBreakTimer -= Time.deltaTime;

            if (roundBreakTimer <= 0f)
                StartRound(currentRound + 1);
        }
    }

    public void BeginSpawning()
    {
        if (gameState == null)
            gameState = GameState.GetOrCreate();

        if (startRoutine != null)
            StopCoroutine(startRoutine);

        startRoutine = StartCoroutine(StartSpawningWhenGridIsReady());
    }

    public void RestartSpawning()
    {
        if (gameState == null)
            gameState = GameState.GetOrCreate();

        gameState.ResetState();
        StopSpawning(true);
        BeginSpawning();
    }

    public void RestartSpawningFromRound(int round)
    {
        if (gameState == null)
            gameState = GameState.GetOrCreate();

        gameState.ResetState(round);
        StopSpawning(true);
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
        cachedPath = null;
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
            yield break;
        }

        while (!gridManager.HasGrid)
            yield return null;

        BuildPath();
        currentRound = gameState != null ? gameState.CurrentRound : 1;

        if (cachedPath != null)
            StartRound(currentRound);

        startRoutine = null;
    }

    private void StartRound(int round)
    {
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
            return;
        }

        // Use the path cached by GridManager during grid build and occupancy changes.
        cachedPath = gridManager.GetCachedEnemyPathWorld();

        if (cachedPath == null || cachedPath.Count == 0)
        {
            Debug.LogError("EnemySpawner: No path from start to goal found.");
            cachedPath = null;
            return;
        }

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

        List<Vector3> newPath = gridManager.GetCachedEnemyPathWorld();
        if (newPath == null || newPath.Count == 0)
            return;

        cachedPath = newPath;

        // Move active enemies to the updated path.
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = spawnedEnemies[i];
            if (enemy == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }
            enemy.SetWaypoints(new List<Vector3>(newPath));
        }
    }

    private void OnDestroy()
    {
        if (subscribedToGridPathChanged && gridManager != null)
        {
            gridManager.OnPathChanged -= HandleGridPathChanged;
            subscribedToGridPathChanged = false;
        }
    }

    private void BuildSpawnQueueForRound()
    {
        spawnQueue.Clear();
        DifficultySettings difficulty = GameSession.SelectedDifficulty;

        if (spawnEntries != null)
        {
            foreach (EnemySpawnEntry entry in spawnEntries)
            {
                if (!IsValidSpawnEntry(entry) || currentRound < entry.firstRound)
                    continue;

                int scaledBase     = Mathf.Max(1, Mathf.RoundToInt(entry.baseAmount * difficulty.amountMultiplier));
                int scaledPerRound = Mathf.RoundToInt(entry.amountPerRound * difficulty.amountScaleMultiplier);
                int amount = scaledBase + ((currentRound - entry.firstRound) * scaledPerRound);
                for (int i = 0; i < amount; i++)
                    spawnQueue.Enqueue(entry);
            }
        }

        if (spawnQueue.Count == 0 && enemyData != null && enemyPrefab != null)
        {
            EnemySpawnEntry fallbackEntry = new EnemySpawnEntry
            {
                label = "Fallback Enemy",
                enemyData = enemyData,
                enemyPrefab = enemyPrefab,
                firstRound = 1,
                baseAmount = 5,
                amountPerRound = 1,
                spawnInterval = spawnInterval
            };

            int scaledBase     = Mathf.Max(1, Mathf.RoundToInt(fallbackEntry.baseAmount * difficulty.amountMultiplier));
            int scaledPerRound = Mathf.RoundToInt(fallbackEntry.amountPerRound * difficulty.amountScaleMultiplier);
            int fallbackAmount = scaledBase + ((currentRound - 1) * scaledPerRound);
            for (int i = 0; i < fallbackAmount; i++)
                spawnQueue.Enqueue(fallbackEntry);
        }
    }

    private bool IsValidSpawnEntry(EnemySpawnEntry entry)
    {
        return entry != null && entry.enemyData != null && entry.enemyPrefab != null;
    }

    private void SpawnEnemy(EnemySpawnEntry spawnEntry)
    {
        if (!IsValidSpawnEntry(spawnEntry))
        {
            Debug.LogError("EnemySpawner: Invalid spawn entry.");
            return;
        }

        GameObject enemyObject = PrefabPool.Spawn(spawnEntry.enemyPrefab, cachedPath[0], Quaternion.identity);
        Enemy enemy = enemyObject.GetComponent<Enemy>();

        if (enemy == null)
        {
            Debug.LogWarning("EnemySpawner: Enemy prefab has no Enemy component.");
            PrefabPool.Release(enemyObject);
            return;
        }

        enemy.Initialize(spawnEntry.enemyData, new List<Vector3>(cachedPath));
        enemy.OnDied += HandleEnemyDied;
        enemy.OnReachedGoal += HandleEnemyReachedGoal;
        spawnedEnemies.Add(enemy);
        aliveEnemies++;
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        UnregisterEnemy(enemy);
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        gameState?.AddMoney(enemy.Reward);
    }

    private void HandleEnemyReachedGoal(Enemy enemy)
    {
        UnregisterEnemy(enemy);
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
    }

    private void UnregisterEnemy(Enemy enemy)
    {
        if (enemy == null)
            return;

        enemy.OnDied -= HandleEnemyDied;
        enemy.OnReachedGoal -= HandleEnemyReachedGoal;
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

        // Also remove stray enemies from earlier runs that are no longer tracked.
        Enemy[] strays = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        for (int i = 0; i < strays.Length; i++)
        {
            if (strays[i] != null)
                PrefabPool.Release(strays[i].gameObject);
        }
    }
}
