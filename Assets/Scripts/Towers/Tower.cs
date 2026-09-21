using UnityEngine;
using TD.Bullets;
using TD.Combat;
using TD.Core;

namespace TD.Towers
{
    [DefaultExecutionOrder(100)]
    public class Tower : MonoBehaviour
    {
        private const float TargetSearchInterval = 0.1f;

        [Header("Aiming")]
        [SerializeField] private Transform turretPivot;
        [SerializeField] private Transform firePoint;
        [SerializeField, Min(0f)] private float rotationSpeed = 540f;
        [SerializeField, Range(0f, 45f)] private float firingAngleTolerance = 5f;

        private TowerData data;
        private TowerUpgradeData upgradeData;
        private int currentUpgradeLevel;
        private float attackTimer;
        private float targetSearchTimer;
        private int totalInvested;
        private float terrainRangeBonus;
        private ITargetProvider targetProvider;
        private TargetingMode targetingMode = TargetingMode.First;
        private IDamageable currentTarget;

        public TowerData Data => data;
        public int CurrentUpgradeLevel => currentUpgradeLevel;
        public float Damage
        {
            get
            {
                BulletData bulletData = EffectiveBulletData;
                return bulletData != null
                    ? bulletData.ModifyDamage(EffectiveDamage)
                    : EffectiveDamage;
            }
        }
        public float AttackSpeed => EffectiveAttackSpeed;
        public float Range => EffectiveRange;
        public TargetingMode TargetingMode => targetingMode;

        private float EffectiveDamage
        {
            get
            {
                float total = data.damage;
                int cap = Mathf.Min(currentUpgradeLevel, upgradeData != null && upgradeData.levels != null ? upgradeData.levels.Count : 0);
                for (int index = 0; index < cap; index++)
                    if (upgradeData.levels[index] != null)
                        total += upgradeData.levels[index].damageBonus;
                return total;
            }
        }

        private float EffectiveAttackSpeed
        {
            get
            {
                float total = data.attackSpeed;
                int cap = Mathf.Min(currentUpgradeLevel, upgradeData != null && upgradeData.levels != null ? upgradeData.levels.Count : 0);
                for (int index = 0; index < cap; index++)
                    if (upgradeData.levels[index] != null)
                        total += upgradeData.levels[index].attackSpeedBonus;
                BulletData bulletData = EffectiveBulletData;
                return bulletData != null
                    ? bulletData.ModifyAttackSpeed(total)
                    : Mathf.Max(0.01f, total);
            }
        }

        private float EffectiveRange
        {
            get
            {
                float total = data.range + terrainRangeBonus;
                int cap = Mathf.Min(currentUpgradeLevel, upgradeData != null && upgradeData.levels != null ? upgradeData.levels.Count : 0);
                for (int index = 0; index < cap; index++)
                    if (upgradeData.levels[index] != null)
                        total += upgradeData.levels[index].rangeBonus;
                BulletData bulletData = EffectiveBulletData;
                return bulletData != null
                    ? bulletData.ModifyRange(total)
                    : Mathf.Max(0f, total);
            }
        }

        /// <summary>
        /// Adds a flat range bonus from the terrain the tower stands on (e.g. +1
        /// when placed on Elevated ground). Set once after placement.
        /// </summary>
        public void SetTerrainRangeBonus(float bonus)
        {
            terrainRangeBonus = bonus;
        }

        /// <summary>
        /// Returns the currently active bullet data.
        /// Walks upgrade levels from highest to lowest and uses the first override.
        /// </summary>
        private BulletData EffectiveBulletData
        {
            get
            {
                if (upgradeData != null && upgradeData.levels != null)
                {
                    int cap = Mathf.Min(currentUpgradeLevel, upgradeData.levels.Count);
                    for (int index = cap - 1; index >= 0; index--)
                    {
                        if (upgradeData.levels[index] != null && upgradeData.levels[index].overrideBulletData != null)
                            return upgradeData.levels[index].overrideBulletData;
                    }
                }

                return data.bulletData;
            }
        }

        /// <summary>
        /// Initializes the tower. Must be called immediately after Instantiate.
        /// </summary>
        /// <param name="towerData">Required base tower stats.</param>
        /// <param name="towerUpgradeData">Optional upgrade path. Null means upgrades are unavailable.</param>
        public void Initialize(TowerData towerData, TowerUpgradeData towerUpgradeData = null)
        {
            data = towerData;
            upgradeData = towerUpgradeData;
            currentUpgradeLevel = 0;
            attackTimer = 0f;
            targetSearchTimer = 0f;
            totalInvested = towerData != null ? towerData.cost : 0;
            targetingMode = TargetingMode.First;
            currentTarget = null;
            if (targetProvider == null)
                targetProvider = DefaultTargetProvider.Instance;
        }

        /// <summary>
        /// Overrides the targeting strategy. Useful for tests or alternative
        /// targeting rules. Must be called before the first <see cref="Update"/>.
        /// </summary>
        public void SetTargetProvider(ITargetProvider provider)
        {
            targetProvider = provider;
        }

        public void SetTargetingMode(TargetingMode mode)
        {
            targetingMode = mode;
            currentTarget = null;
            targetSearchTimer = 0f;
        }

        public TargetingMode CycleTargetingMode()
        {
            int modeCount = System.Enum.GetValues(typeof(TargetingMode)).Length;
            SetTargetingMode((TargetingMode)(((int)targetingMode + 1) % modeCount));
            return targetingMode;
        }

        public bool CanUpgrade()
        {
            return upgradeData != null
                && upgradeData.levels != null
                && currentUpgradeLevel < upgradeData.levels.Count
                && upgradeData.levels[currentUpgradeLevel] != null;
        }

        /// <summary>Returns the next upgrade cost, or -1 when no upgrade is available.</summary>
        public int GetNextUpgradeCost()
        {
            if (!CanUpgrade())
                return -1;

            return upgradeData.levels[currentUpgradeLevel].cost;
        }

        /// <summary>Applies the next upgrade. Returns false when no upgrade is available.</summary>
        public bool TryUpgrade()
        {
            if (!CanUpgrade())
                return false;

            totalInvested += upgradeData.levels[currentUpgradeLevel].cost;
            currentUpgradeLevel++;
            return true;
        }

        /// <summary>Returns the sell value (50% of invested gold).</summary>
        public int GetSellValue()
        {
            return Mathf.RoundToInt(totalInvested * 0.5f);
        }

        private void Update()
        {
            if (data == null || !GameplayLifecycle.CanRunCombat)
            {
                currentTarget = null;
                return;
            }

            attackTimer -= Time.deltaTime;
            targetSearchTimer -= Time.deltaTime;

            if (!IsValidTarget(currentTarget))
                currentTarget = null;

            if (targetSearchTimer <= 0f && (currentTarget == null || attackTimer <= 0f))
            {
                currentTarget = FindNearestTarget();
                targetSearchTimer = TargetSearchInterval;
            }

            if (currentTarget == null)
                return;

            RotateTowardsTarget(currentTarget);

            if (attackTimer <= 0f && IsAimedAt(currentTarget))
            {
                Shoot(currentTarget);
                attackTimer = 1f / EffectiveAttackSpeed;
            }
        }

        private bool IsValidTarget(IDamageable target)
        {
            if (target == null)
                return false;

            if (target is Component component && (component == null || !component.gameObject.activeInHierarchy))
                return false;

            if (target.IsDead)
                return false;

            float range = EffectiveRange;
            return (target.WorldPosition - transform.position).sqrMagnitude <= range * range;
        }

        private void RotateTowardsTarget(IDamageable target)
        {
            if (turretPivot == null
                || !TryGetTargetRotation(target, out Quaternion targetRotation))
                return;

            turretPivot.rotation = Quaternion.RotateTowards(
                turretPivot.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        private bool IsAimedAt(IDamageable target)
        {
            if (turretPivot == null
                || !TryGetTargetRotation(target, out Quaternion targetRotation))
                return true;

            return Quaternion.Angle(turretPivot.rotation, targetRotation) <= firingAngleTolerance;
        }

        private bool TryGetTargetRotation(IDamageable target, out Quaternion rotation)
        {
            Vector3 aimPosition = GetAimPositionForTarget(target);
            return TryGetAimRotation(turretPivot.position, aimPosition, out rotation);
        }

        internal Vector3 GetAimPositionForTarget(IDamageable target)
        {
            Vector3 origin = firePoint != null
                ? firePoint.position
                : turretPivot != null
                    ? turretPivot.position
                    : transform.position;
            BulletData bulletData = EffectiveBulletData;
            float projectileSpeed = bulletData != null && bulletData.bulletType == BulletType.Projectile
                ? bulletData.travelSpeed
                : 0f;

            return Bullet.PredictDestination(origin, target, projectileSpeed);
        }

        internal static bool TryGetAimRotation(Vector3 origin, Vector3 targetPosition, out Quaternion rotation)
        {
            Vector2 direction = targetPosition - origin;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                rotation = Quaternion.identity;
                return false;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(0f, 0f, angle);
            return true;
        }

        /// <summary>
        /// Delegates target selection to the configured <see cref="ITargetProvider"/>.
        /// </summary>
        private IDamageable FindNearestTarget()
        {
            return targetProvider != null
                ? targetProvider.FindTarget(transform.position, EffectiveRange, targetingMode)
                : null;
        }

        private void Shoot(IDamageable target)
        {
            BulletData bulletData = EffectiveBulletData;

            if (bulletData == null)
            {
                Debug.LogWarning($"Tower '{data.towerName}': No BulletData assigned.");
                return;
            }

            if (bulletData.bulletPrefab == null)
            {
                Debug.LogWarning($"Tower '{data.towerName}': BulletData '{bulletData.bulletName}' has no prefab.");
                return;
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            Quaternion spawnRotation = firePoint != null ? firePoint.rotation : Quaternion.identity;
            GameObject bulletObject = PrefabPool.Spawn(bulletData.bulletPrefab, spawnPosition, spawnRotation);
            Bullet bullet = bulletObject.GetComponent<Bullet>();

            if (bullet != null)
                bullet.Initialize(bulletData, EffectiveDamage, target);
            else
            {
                Debug.LogWarning($"Tower '{data.towerName}': Bullet prefab has no Bullet component.");
                PrefabPool.Release(bulletObject);
            }
        }
    }
}
