using System.Collections.Generic;
using UnityEngine;
using TD.Combat;
using TD.Enemies;

namespace TD.Towers
{
    /// <summary>
    /// Default tower targeting strategy. Encapsulates the dependency on the
    /// global registries (<see cref="Destructible.MarkedTargets"/> and
    /// <see cref="Enemy.ActiveEnemies"/>) so towers themselves stay free of it.
    ///
    /// Priority:
    /// 1. Marked destructibles in range - the player explicitly targeted these.
    /// 2. Enemy preferred by the tower's targeting mode.
    /// </summary>
    public sealed class DefaultTargetProvider : ITargetProvider
    {
        public static DefaultTargetProvider Instance { get; } = new DefaultTargetProvider();

        public IDamageable FindTarget(Vector3 origin, float range, TargetingMode mode = TargetingMode.First)
        {
            float rangeSqr = range * range;

            IReadOnlyList<Destructible> markedTargets = Destructible.MarkedTargets;
            Destructible nearestMarked = null;
            float nearestMarkedSqr = rangeSqr;
            for (int index = 0; index < markedTargets.Count; index++)
            {
                Destructible marked = markedTargets[index];
                if (marked == null || marked.IsDead)
                    continue;

                float sqr = (origin - marked.WorldPosition).sqrMagnitude;
                if (sqr <= nearestMarkedSqr)
                {
                    nearestMarkedSqr = sqr;
                    nearestMarked = marked;
                }
            }

            if (nearestMarked != null)
                return nearestMarked;

            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            Enemy best = null;
            float bestSqrDistance = float.MaxValue;
            float bestMetric = 0f;
            for (int index = 0; index < enemies.Count; index++)
            {
                Enemy enemy = enemies[index];
                if (enemy == null || enemy.IsDead)
                    continue;

                float sqrDist = (origin - enemy.transform.position).sqrMagnitude;
                if (sqrDist > rangeSqr)
                    continue;

                float metric = GetMetric(enemy, mode);
                if (best == null || IsPreferred(metric, bestMetric, mode, sqrDist, bestSqrDistance))
                {
                    best = enemy;
                    bestSqrDistance = sqrDist;
                    bestMetric = metric;
                }
            }

            return best;
        }

        private static bool IsPreferred(
            float candidateMetric,
            float currentMetric,
            TargetingMode mode,
            float candidateSqrDistance,
            float currentSqrDistance)
        {
            if (!Mathf.Approximately(candidateMetric, currentMetric))
            {
                bool preferHigher = mode != TargetingMode.Last && mode != TargetingMode.LowestHealth;
                return preferHigher ? candidateMetric > currentMetric : candidateMetric < currentMetric;
            }

            return candidateSqrDistance < currentSqrDistance;
        }

        private static float GetMetric(Enemy enemy, TargetingMode mode)
        {
            switch (mode)
            {
                case TargetingMode.First:
                case TargetingMode.Last:
                    return enemy.PathProgress;
                case TargetingMode.HighestHealth:
                case TargetingMode.LowestHealth:
                    return enemy.CurrentHealth;
                case TargetingMode.Fastest:
                    return enemy.CurrentSpeed;
                default:
                    return enemy.PathProgress;
            }
        }
    }
}
