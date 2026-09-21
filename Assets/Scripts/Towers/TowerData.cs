using UnityEngine;
using TD.Bullets;

namespace TD.Towers
{
    [CreateAssetMenu(fileName = "TowerData", menuName = "TowerDefense/Tower Data")]
    public class TowerData : ScriptableObject
    {
        [Header("Info")]
        public string towerName;

        [Tooltip("Icon shown in the in-game menu.")]
        public Sprite icon;

        [Tooltip("Cost to place this tower.")]
        public int cost = 50;

        [Header("Combat")]
        [Tooltip("Base damage per shot.")]
        public float damage = 20f;

        [Tooltip("Attacks per second.")]
        public float attackSpeed = 1f;

        [Tooltip("Range in units.")]
        public float range = 3f;

        [Header("Projectile")]
        [Tooltip("Default bullet type fired by this tower.")]
        public BulletData bulletData;

        [Header("Upgrades")]
        [Tooltip("Upgrade path used by this tower. Leave empty to use the BuildManager fallback.")]
        public TowerUpgradeData upgradeData;

        [Header("Prefabs")]
        [Tooltip("Tower prefab. Must contain a Tower component.")]
        public GameObject towerPrefab;

        public float DamagePerShot => bulletData != null
            ? bulletData.ModifyDamage(damage)
            : Mathf.Max(0f, damage);

        public float AttacksPerSecond => bulletData != null
            ? bulletData.ModifyAttackSpeed(attackSpeed)
            : Mathf.Max(0.01f, attackSpeed);

        public float TargetingRange => GetTargetingRange();

        public float GetTargetingRange(float flatRangeBonus = 0f)
        {
            float baseRange = range + flatRangeBonus;
            return bulletData != null
                ? bulletData.ModifyRange(baseRange)
                : Mathf.Max(0f, baseRange);
        }
    }
}
