using UnityEngine;

public class Bullet : MonoBehaviour
{
    private BulletData data;
    private float damage;
    private Enemy target;
    private bool hasHit;

    /// <summary>
    /// Initialisiert das Projektil. Muss direkt nach Instantiate aufgerufen werden.
    /// </summary>
    public void Initialize(BulletData bulletData, float damage, Enemy target)
    {
        this.data = bulletData;
        this.damage = damage;
        this.target = target;
        hasHit = false;
    }

    private void OnDisable()
    {
        // Beim Zurueckgeben in den Pool Zustand zuruecksetzen, damit naechste
        // Wiederverwendung wie eine frische Instanz wirkt.
        target = null;
        hasHit = false;
        damage = 0f;
    }

    private void Update()
    {
        if (hasHit)
            return;

        // Ziel vernichtet bevor Bullet ankam
        if (target == null)
        {
            PrefabPool.Release(gameObject);
            return;
        }

        Vector3 direction = target.transform.position - transform.position;
        float step = data.travelSpeed * Time.deltaTime;

        if (direction.magnitude <= step)
        {
            transform.position = target.transform.position;
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
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null)
                ApplyHit(enemy);
        }
    }

    private void ApplyHit(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return;

        float finalDamage = damage * data.damageMultiplier;
        enemy.TakeDamage(finalDamage);

        if (data.slowDuration > 0f && data.slowFactor < 1f)
            enemy.ApplySlow(data.slowFactor, data.slowDuration);
    }
}
