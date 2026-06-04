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
    /// 2. Nearest enemy in range.
    /// </summary>
    public sealed class DefaultTargetProvider : ITargetProvider
    {
        public static DefaultTargetProvider Instance { get; } = new DefaultTargetProvider();

        public IDamageable FindTarget(Vector3 origin, float range)
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
            Enemy nearest = null;
            float nearestSqrDist = rangeSqr;
            for (int index = 0; index < enemies.Count; index++)
            {
                Enemy enemy = enemies[index];
                if (enemy == null || enemy.IsDead)
                    continue;

                float sqrDist = (origin - enemy.transform.position).sqrMagnitude;
                if (sqrDist <= nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = enemy;
                }
            }

            return nearest;
        }
    }
}
