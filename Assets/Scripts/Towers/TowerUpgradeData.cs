using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TowerUpgradeData", menuName = "TowerDefense/Tower Upgrade Data")]
public class TowerUpgradeData : ScriptableObject
{
    [Serializable]
    public class UpgradeLevel
    {
        [Header("Info")]
        public string upgradeName;

        [Tooltip("Cost of this upgrade level.")]
        public int cost;

        [Header("Stat Bonuses")]
        public float damageBonus;
        public float attackSpeedBonus;
        public float rangeBonus;

        [Header("Projectile Override")]
        [Tooltip("Leave empty to keep the current bullet type.")]
        public BulletData overrideBulletData;
    }

    [Tooltip("Upgrade levels in ascending order. Index zero is the first upgrade.")]
    public List<UpgradeLevel> levels = new List<UpgradeLevel>();
}
