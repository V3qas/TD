using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TD.Combat;
using TD.Core;
using TD.UI;

namespace TD.Enemies
{
    [DefaultExecutionOrder(-100)]
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
        private EnemyPath waypoints;
        private int waypointIndex;
        private HealthBar healthBar;
        private float slowFactor = 1f;
        private Coroutine slowCoroutine;

        public bool IsDead => currentHealth <= 0f;
        public EnemyData Data => data;
        public int Reward => scaledReward;
        public int GoalDamage => data != null ? Mathf.Max(1, data.goalDamage) : 1;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentSpeed => Mathf.Max(0f, scaledSpeed * slowFactor);
        public float PathProgress => waypoints != null ? waypoints.GetProgress(transform.position, waypointIndex) : 0f;
        public Vector3 WorldPosition => transform.position;

        private void OnEnable()
        {
            RegisterActiveEnemy();
        }

        private void OnDisable()
        {
            activeEnemies.Remove(this);
            CombatPhysics.Invalidate();
            OnDied = null;
            OnReachedGoal = null;
            waypoints = null;
            if (slowCoroutine != null)
                StopCoroutine(slowCoroutine);
            slowCoroutine = null;
            slowFactor = 1f;
            if (healthBar != null)
                healthBar.Unbind();
        }

        /// <summary>
        /// Initializes this enemy with static data and world-space waypoints.
        /// </summary>
        public void Initialize(EnemyData enemyData, List<Vector3> path)
        {
            Initialize(enemyData, new EnemyPath(path));
        }

        public void Initialize(EnemyData enemyData, EnemyPath path)
        {
            RegisterActiveEnemy();
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
            CombatPhysics.Invalidate();

            if (slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
                slowCoroutine = null;
            }

            EnsureHealthBar();
            healthBar.Bind(this);
        }

        private void RegisterActiveEnemy()
        {
            if (isActiveAndEnabled && !activeEnemies.Contains(this))
                activeEnemies.Add(this);
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
            SetWaypoints(new EnemyPath(newWaypoints));
        }

        public void SetWaypoints(EnemyPath newWaypoints)
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

        public Vector3 PredictPosition(float seconds)
        {
            if (waypoints == null || waypoints.Count == 0 || seconds <= 0f || CurrentSpeed <= 0f)
                return transform.position;

            int targetIndex = waypointIndex;
            return waypoints.Advance(transform.position, ref targetIndex, CurrentSpeed * seconds);
        }

        private void Update()
        {
            if (!GameplayLifecycle.CanRunCombat || IsDead || waypoints == null || waypointIndex >= waypoints.Count)
                return;

            MoveAlongPath();
        }

        private void MoveAlongPath()
        {
            transform.position = waypoints.Advance(transform.position, ref waypointIndex, CurrentSpeed * Time.deltaTime);
            CombatPhysics.Invalidate();
            if (waypointIndex >= waypoints.Count)
                ReachGoal();
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
}
