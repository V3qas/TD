using UnityEngine;
using TD.Combat;
using TD.Core;
using TD.Enemies;

namespace TD.Bullets
{
    public class Bullet : MonoBehaviour
    {
        private BulletData data;
        private float damage;
        private IDamageable target;
        private bool hasHit;
        private Vector3 destination;

        public Vector3 Destination => destination;

        public void Initialize(BulletData bulletData, float damage, IDamageable target)
        {
            this.data = bulletData;
            this.damage = damage;
            this.target = target;
            hasHit = false;
            destination = CalculateDestination(target, bulletData != null ? bulletData.travelSpeed : 0f);
        }

        private void OnDisable()
        {
            // Reset pooled state so the next spawn behaves like a fresh instance.
            target = null;
            hasHit = false;
            damage = 0f;
            destination = Vector3.zero;
        }

        private void Update()
        {
            if (hasHit)
                return;

            if (target == null || target.IsDead)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            if (target is Destructible destructible && !destructible.IsMarked)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            float step = Mathf.Max(0.01f, data.travelSpeed) * Time.deltaTime;
            float distance = Vector3.Distance(transform.position, destination);

            if (distance <= step)
            {
                transform.position = destination;
                OnReachedTarget();
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, step);
            }
        }

        private Vector3 CalculateDestination(IDamageable damageable, float projectileSpeed)
        {
            if (damageable == null)
                return transform.position;

            Vector3 predictedPosition = damageable.WorldPosition;
            if (!(damageable is Enemy enemy) || projectileSpeed <= 0f)
                return predictedPosition;

            float travelTime = Vector3.Distance(transform.position, predictedPosition) / projectileSpeed;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                predictedPosition = enemy.PredictPosition(travelTime);
                travelTime = Vector3.Distance(transform.position, predictedPosition) / projectileSpeed;
            }

            return predictedPosition;
        }

        private void OnReachedTarget()
        {
            hasHit = true;

            if (data.splashRadius > 0f)
                HitSplash();
            else
                ApplyHit(target);

            PrefabPool.Release(gameObject);
        }

        private void HitSplash()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, data.splashRadius);
            foreach (Collider2D hit in hits)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                    ApplyHit(damageable);
            }
        }

        private void ApplyHit(IDamageable victim)
        {
            if (victim == null || victim.IsDead)
                return;

            if (victim is Destructible destructible && !destructible.IsMarked)
                return;

            float finalDamage = damage * data.damageMultiplier;
            victim.TakeDamage(finalDamage);

            if (data.slowDuration > 0f && data.slowFactor < 1f && victim is Enemy enemy)
                enemy.ApplySlow(data.slowFactor, data.slowDuration);
        }
    }
}
