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

        [Header("Prefabs")]
        [Tooltip("Tower prefab. Must contain a Tower component.")]
        public GameObject towerPrefab;
    }
}
