using UnityEngine;
using TD.Bullets;
using TD.Combat;
using TD.Core;

namespace TD.Towers
{
    public class Tower : MonoBehaviour
    {
        private TowerData data;
        private TowerUpgradeData upgradeData;
        private int currentUpgradeLevel;
        private float attackTimer;
        private int totalInvested;
        private float terrainRangeBonus;
        private ITargetProvider targetProvider;

        public TowerData Data => data;
        public int CurrentUpgradeLevel => currentUpgradeLevel;
        public float Damage => EffectiveDamage;
        public float AttackSpeed => EffectiveAttackSpeed;
        public float Range => EffectiveRange;

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
                return Mathf.Max(0.01f, total);
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
                return total;
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
            totalInvested = towerData != null ? towerData.cost : 0;
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
            if (data == null)
                return;

            attackTimer -= Time.deltaTime;

            if (attackTimer <= 0f)
            {
                IDamageable target = FindNearestTarget();
                if (target != null)
                {
                    Shoot(target);
                    attackTimer = 1f / EffectiveAttackSpeed;
                }
            }
        }

        /// <summary>
        /// Delegates target selection to the configured <see cref="ITargetProvider"/>.
        /// </summary>
        private IDamageable FindNearestTarget()
        {
            return targetProvider != null
                ? targetProvider.FindTarget(transform.position, EffectiveRange)
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

            GameObject bulletObject = PrefabPool.Spawn(bulletData.bulletPrefab, transform.position, Quaternion.identity);
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
