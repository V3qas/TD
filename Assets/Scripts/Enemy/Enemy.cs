using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    /// <summary>Wird gefeuert, wenn der Gegner stirbt. Parameter: dieser Enemy.</summary>
    public event Action<Enemy> OnDied;
    public event Action<Enemy> OnReachedGoal;

    // ── Aktiver-Gegner-Registry (vermeidet FindObjectsByType in Towers) ─────
    private static readonly List<Enemy> activeEnemies = new List<Enemy>();
    /// <summary>Alle aktuell aktiven (lebenden, nicht despawnten) Gegner. Read-only.</summary>
    public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;

    private void OnEnable()
    {
        activeEnemies.Add(this);
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    private EnemyData data;
    private float currentHealth;
    private float currentShield;
    private float scaledSpeed;
    private int scaledReward;
    private List<Vector3> waypoints;
    private int waypointIndex;

    // Slow-Effekt
    private float slowFactor = 1f;
    private Coroutine slowCoroutine;

    public bool IsDead => currentHealth <= 0f;
    public EnemyData Data => data;
    public int Reward => scaledReward;

    /// <summary>
    /// Initialisiert den Gegner mit seinen Daten und dem Wegpunkt-Pfad.
    /// </summary>
    /// <param name="enemyData">Statische Daten (Werte aus dem ScriptableObject).</param>
    /// <param name="path">Weltkoordinaten-Pfad vom Spawn zum Ziel.</param>
    public void Initialize(EnemyData enemyData, List<Vector3> path)
    {
        data = enemyData;
        DifficultySettings difficulty = GameSession.SelectedDifficulty;
        currentHealth = enemyData.maxHealth * difficulty.healthMultiplier;
        currentShield = enemyData.shield;
        scaledSpeed = enemyData.speed * difficulty.speedMultiplier;
        scaledReward = Mathf.RoundToInt(enemyData.reward * difficulty.rewardMultiplier);
        waypoints = path;
        waypointIndex = 0;
        slowFactor = 1f;

        // Falls aus dem Pool reaktiviert: laufende Slow-Coroutine aus dem
        // vorigen Leben stoppen, sonst koennte sie den frischen Slow löschen.
        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }
    }

    /// <summary>
    /// Ersetzt die Wegpunkte zur Laufzeit (z.B. wenn sich der Pfad nach einer
    /// Tower-Platzierung aendert). Sucht den naehesten Wegpunkt im neuen Pfad
    /// und setzt den Index entsprechend, damit der Gegner nicht zurueck laeuft.
    /// </summary>
    public void SetWaypoints(List<Vector3> newWaypoints)
    {
        if (newWaypoints == null || newWaypoints.Count == 0)
            return;

        Vector3 position = transform.position;
        int nearestIndex = 0;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < newWaypoints.Count; i++)
        {
            float sqr = (newWaypoints[i] - position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearestIndex = i;
            }
        }

        waypoints = newWaypoints;
        // Naechster Zielpunkt ist der naechste Wegpunkt nach dem naehesten,
        // damit wir vorwaerts gehen (sofern vorhanden).
        waypointIndex = Mathf.Min(nearestIndex + 1, newWaypoints.Count - 1);
    }

    private void Update()
    {
        if (IsDead || waypoints == null || waypointIndex >= waypoints.Count)
            return;

        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        Vector3 target = waypoints[waypointIndex];
        Vector3 direction = target - transform.position;
        float effectiveSpeed = scaledSpeed * slowFactor;
        float step = effectiveSpeed * Time.deltaTime;

        if (direction.magnitude <= step)
        {
            transform.position = target;
            waypointIndex++;

            if (waypointIndex >= waypoints.Count)
                ReachGoal();
        }
        else
        {
            transform.position += direction.normalized * step;
        }
    }

    /// <summary>
    /// Verarbeitet eingehenden Schaden.
    /// Reihenfolge: Schild absorbiert zuerst (ohne Rüstung), dann Rüstung reduziert, dann HP.
    /// </summary>
    public void TakeDamage(float rawDamage)
    {
        if (IsDead)
            return;

        float damage = rawDamage;

        // Schild absorbiert Schaden (keine Rüstungsreduzierung auf Schild)
        if (currentShield > 0f)
        {
            float absorbed = Mathf.Min(currentShield, damage);
            currentShield -= absorbed;
            damage -= absorbed;
        }

        // Rüstung reduziert verbleibenden Schaden
        damage = Mathf.Max(0f, damage - data.armor);

        currentHealth -= damage;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    /// <summary>
    /// Wendet einen Slow-Effekt an. Überschreibt einen laufenden Slow,
    /// wenn der neue Faktor kleiner (stärker) ist.
    /// </summary>
    public void ApplySlow(float factor, float duration)
    {
        if (factor >= slowFactor)
            return; // schwächerer Slow wird ignoriert

        if (slowCoroutine != null)
            StopCoroutine(slowCoroutine);

        slowFactor = factor;
        slowCoroutine = StartCoroutine(RemoveSlowAfter(duration));
    }

    private IEnumerator RemoveSlowAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        slowFactor = 1f;
        slowCoroutine = null;
    }

    private void Die()
    {
        OnDied?.Invoke(this);
        ReleaseToPool();
    }

    private void ReachGoal()
    {
        OnReachedGoal?.Invoke(this);
        ReleaseToPool();
    }

    private void ReleaseToPool()
    {
        // Alle Listener loesen, damit eine spaetere Wiederverwendung nicht
        // alte Subscriber re-feuert.
        OnDied = null;
        OnReachedGoal = null;
        waypoints = null;
        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }
        slowFactor = 1f;
        PrefabPool.Release(gameObject);
    }
}
