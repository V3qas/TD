using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    /// <summary>Raised when this enemy dies.</summary>
    public event Action<Enemy> OnDied;
    public event Action<Enemy> OnReachedGoal;

    private static readonly List<Enemy> activeEnemies = new List<Enemy>();
    /// <summary>All active, living enemies. Read-only.</summary>
    public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;

    private EnemyData data;
    private float currentHealth;
    private float maxHealth;
    private float currentShield;
    private float scaledSpeed;
    private int scaledReward;
    private List<Vector3> waypoints;
    private int waypointIndex;
    private HealthBar healthBar;
    private float slowFactor = 1f;
    private Coroutine slowCoroutine;

    public bool IsDead => currentHealth <= 0f;
    public EnemyData Data => data;
    public int Reward => scaledReward;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public Vector3 WorldPosition => transform.position;

    private void OnEnable()
    {
        activeEnemies.Add(this);
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    /// <summary>
    /// Initializes this enemy with static data and world-space waypoints.
    /// </summary>
    public void Initialize(EnemyData enemyData, List<Vector3> path)
    {
        data = enemyData;
        DifficultySettings difficulty = GameSession.SelectedDifficulty;
        maxHealth = enemyData.maxHealth * difficulty.healthMultiplier;
        currentHealth = maxHealth;
        currentShield = enemyData.shield;
        scaledSpeed = enemyData.speed * difficulty.speedMultiplier;
        scaledReward = Mathf.RoundToInt(enemyData.reward * difficulty.rewardMultiplier);
        waypoints = path;
        waypointIndex = 0;
        slowFactor = 1f;

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }

        EnsureHealthBar();
        healthBar.Bind(this);
    }

    private void EnsureHealthBar()
    {
        if (healthBar == null)
            healthBar = HealthBar.AttachTo(transform);
    }

    /// <summary>
    /// Replaces waypoints at runtime and keeps the enemy moving forward on the
    /// new path from the nearest waypoint.
    /// </summary>
    public void SetWaypoints(List<Vector3> newWaypoints)
    {
        if (newWaypoints == null || newWaypoints.Count == 0)
            return;

        Vector3 position = transform.position;
        int nearestIndex = 0;
        float nearestSqr = float.MaxValue;

        for (int index = 0; index < newWaypoints.Count; index++)
        {
            float sqr = (newWaypoints[index] - position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearestIndex = index;
            }
        }

        waypoints = newWaypoints;
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
    /// Applies incoming damage. Shields absorb first, armor then reduces
    /// remaining damage, and health receives the final amount.
    /// </summary>
    public void TakeDamage(float rawDamage)
    {
        if (IsDead)
            return;

        if (data == null)
        {
            currentHealth = Mathf.Max(0f, currentHealth - rawDamage);
            if (currentHealth <= 0f)
                Die();
            return;
        }

        float damage = rawDamage;

        if (currentShield > 0f)
        {
            float absorbed = Mathf.Min(currentShield, damage);
            currentShield -= absorbed;
            damage -= absorbed;
        }

        damage = Mathf.Max(0f, damage - data.armor);
        currentHealth -= damage;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    /// <summary>
    /// Applies a slow effect. Weaker slows do not override a stronger one.
    /// </summary>
    public void ApplySlow(float factor, float duration)
    {
        if (factor >= slowFactor)
            return;

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
        OnDied = null;
        OnReachedGoal = null;
        waypoints = null;

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }

        slowFactor = 1f;

        if (healthBar != null)
            healthBar.Unbind();

        PrefabPool.Release(gameObject);
    }
}
