using System.Collections.Generic;
using UnityEngine;
using TD.Combat;
using TD.Core;
using TD.Enemies;

namespace TD.Bullets
{
    [DefaultExecutionOrder(200)]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private LayerMask hitLayers = ~0;
        private static readonly List<Bullet> activeBullets = new List<Bullet>();
        private readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>(16);
        private readonly List<Collider2D> splashHits = new List<Collider2D>(16);
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private BulletData data;
        private float damage;
        private IDamageable target;
        private bool hasHit;
        private Vector3 destination;
        private bool isLaser;
        private float laserLifetime;
        private SpriteRenderer spriteRenderer;
        private Collider2D projectileCollider;
        private Vector3 defaultLocalScale;
        private bool componentsCached;

        public Vector3 Destination => destination;

        private void OnEnable()
        {
            activeBullets.Add(this);
        }

        public static void ReleaseAll()
        {
            for (int index = activeBullets.Count - 1; index >= 0; index--)
            {
                Bullet bullet = activeBullets[index];
                if (bullet != null)
                {
                    bullet.hasHit = true;
                    PrefabPool.Release(bullet.gameObject);
                }
            }
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            projectileCollider = GetComponent<Collider2D>();
            defaultLocalScale = transform.localScale;
            componentsCached = true;
        }

        public void Initialize(BulletData bulletData, float damage, IDamageable target)
        {
            if (!componentsCached)
                CacheComponents();

            if (!GameplayLifecycle.CanRunCombat)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            this.data = bulletData;
            this.damage = damage;
            this.target = target;
            hasHit = false;
            isLaser = bulletData != null && bulletData.bulletType == BulletType.Laser;

            if (isLaser)
            {
                FireLaser();
                return;
            }

            destination = CalculateDestination(target, bulletData != null ? bulletData.travelSpeed : 0f);
        }

        private void OnDisable()
        {
            activeBullets.Remove(this);
            castHits.Clear();
            splashHits.Clear();
            hitTargets.Clear();
            // Reset pooled state so the next spawn behaves like a fresh instance.
            target = null;
            hasHit = false;
            damage = 0f;
            destination = Vector3.zero;
            isLaser = false;
            laserLifetime = 0f;
            data = null;
            transform.localScale = defaultLocalScale;
        }

        private void Update()
        {
            if (!GameplayLifecycle.CanRunCombat)
            {
                PrefabPool.Release(gameObject);
                return;
            }
            if (isLaser)
            {
                laserLifetime -= Time.deltaTime;
                if (laserLifetime <= 0f)
                    PrefabPool.Release(gameObject);
                return;
            }

            if (hasHit)
                return;

            if (data == null)
            {
                PrefabPool.Release(gameObject);
                return;
            }

            float step = Mathf.Max(0.01f, data.travelSpeed) * Time.deltaTime;
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, destination, step);
            if (TryHitAlongPath(nextPosition))
                return;

            transform.position = nextPosition;
            if ((destination - nextPosition).sqrMagnitude <= Mathf.Epsilon)
                OnReachedDestination();
        }

        private bool TryHitAlongPath(Vector3 nextPosition)
        {
            Vector2 movement = nextPosition - transform.position;
            float distance = movement.magnitude;
            if (distance <= Mathf.Epsilon)
                return false;

            CombatPhysics.Synchronize();
            ContactFilter2D filter = CreateHitFilter();
            Vector2 direction = movement / distance;
            float hitRadius = 0.05f;
            if (projectileCollider is CircleCollider2D circleCollider)
            {
                Vector3 scale = transform.lossyScale;
                hitRadius = circleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
            else if (projectileCollider != null)
            {
                Bounds bounds = projectileCollider.bounds;
                hitRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            }

            int hitCount = Physics2D.CircleCast(
                transform.position,
                Mathf.Max(0.01f, hitRadius),
                direction,
                filter,
                castHits,
                distance);
            IDamageable closestTarget = null;
            float closestDistance = float.PositiveInfinity;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit2D hit = castHits[index];
                if (hit.collider == null || hit.collider.gameObject == gameObject)
                    continue;

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (!CanHit(damageable) || hit.distance >= closestDistance)
                    continue;

                closestTarget = damageable;
                closestDistance = hit.distance;
            }

            if (closestTarget == null)
                return false;

            transform.position += (Vector3)(direction * closestDistance);
            hasHit = true;
            if (data.splashRadius > 0f)
                HitSplash();
            else
                ApplyHit(closestTarget);

            PrefabPool.Release(gameObject);
            return true;
        }

        private void FireLaser()
        {
            Vector3 origin = transform.position;
            Vector3 targetPosition = target != null ? target.WorldPosition : origin + Vector3.right;
            Vector2 direction = targetPosition - origin;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector2.right;
            else
                direction.Normalize();

            float beamLength = Mathf.Max(0.1f, data.maxTravelDistance);
            float beamWidth = Mathf.Max(0.01f, data.beamWidth);
            destination = origin + (Vector3)(direction * beamLength);
            laserLifetime = Mathf.Max(0.01f, data.beamDuration);

            HitLaserPath(origin, direction, beamLength, beamWidth);

            if (!gameObject.activeInHierarchy)
                return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(
                Vector3.Lerp(origin, destination, 0.5f),
                Quaternion.Euler(0f, 0f, angle));
            transform.localScale = new Vector3(beamLength, beamWidth, defaultLocalScale.z);

            if (spriteRenderer == null)
                Debug.LogWarning($"Laser bullet '{name}' has no SpriteRenderer for its beam visual.");
        }

        private void HitLaserPath(Vector2 origin, Vector2 direction, float beamLength, float beamWidth)
        {
            CombatPhysics.Synchronize();
            Physics2D.CircleCast(
                origin,
                beamWidth * 0.5f,
                direction,
                CreateHitFilter(),
                castHits,
                beamLength);

            hitTargets.Clear();
            IDamageable closestTarget = null;
            float closestDistance = float.PositiveInfinity;

            for (int index = 0; index < castHits.Count && data != null; index++)
            {
                RaycastHit2D hit = castHits[index];
                IDamageable damageable = hit.collider != null
                    ? hit.collider.GetComponentInParent<IDamageable>()
                    : null;
                if (!CanHit(damageable))
                    continue;

                if (!data.isPiercing)
                {
                    if (hit.distance < closestDistance)
                    {
                        closestTarget = damageable;
                        closestDistance = hit.distance;
                    }
                    continue;
                }

                ApplyUniqueHit(damageable, hitTargets);
            }

            if (closestTarget != null)
                ApplyHit(closestTarget);
        }

        private void ApplyUniqueHit(IDamageable victim, HashSet<IDamageable> hitTargets)
        {
            if (victim == null)
                return;

            if (hitTargets.Add(victim))
                ApplyHit(victim);
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

        private void OnReachedDestination()
        {
            hasHit = true;

            if (data.splashRadius > 0f)
                HitSplash();

            PrefabPool.Release(gameObject);
        }

        private void HitSplash()
        {
            CombatPhysics.Synchronize();
            Physics2D.OverlapCircle(transform.position, data.splashRadius, CreateHitFilter(), splashHits);
            hitTargets.Clear();
            for (int index = 0; index < splashHits.Count && data != null; index++)
            {
                Collider2D hit = splashHits[index];
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                ApplyUniqueHit(damageable, hitTargets);
            }
        }

        private ContactFilter2D CreateHitFilter()
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(hitLayers);
            return filter;
        }

        private static bool CanHit(IDamageable victim)
        {
            if (victim == null || victim.IsDead)
                return false;

            if (victim is Component component && (component == null || !component.gameObject.activeInHierarchy))
                return false;

            return !(victim is Destructible destructible) || destructible.IsMarked;
        }

        private void ApplyHit(IDamageable victim)
        {
            if (data == null || !GameplayLifecycle.CanRunCombat || !CanHit(victim))
                return;

            BulletData hitData = data;
            float finalDamage = hitData.ModifyDamage(damage);
            victim.TakeDamage(finalDamage);

            if (GameplayLifecycle.CanRunCombat && hitData.slowDuration > 0f && hitData.slowFactor < 1f
                && victim is Enemy enemy && !enemy.IsDead && enemy.isActiveAndEnabled)
                enemy.ApplySlow(hitData.slowFactor, hitData.slowDuration);
        }
    }
}
