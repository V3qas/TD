using UnityEngine;

public class Bullet : MonoBehaviour
{
    private BulletData data;
    private float damage;
    private IDamageable target;
    private bool hasHit;

    public void Initialize(BulletData bulletData, float damage, IDamageable target)
    {
        this.data = bulletData;
        this.damage = damage;
        this.target = target;
        hasHit = false;
    }

    private void OnDisable()
    {
        // Reset pooled state so the next spawn behaves like a fresh instance.
        target = null;
        hasHit = false;
        damage = 0f;
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

        Vector3 targetPosition = target.WorldPosition;
        Vector3 direction = targetPosition - transform.position;
        float step = data.travelSpeed * Time.deltaTime;

        if (direction.magnitude <= step)
        {
            transform.position = targetPosition;
            OnReachedTarget();
        }
        else
        {
            transform.position += direction.normalized * step;
        }
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
