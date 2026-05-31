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

        [Tooltip("Kosten dieser Upgrade-Stufe in Währungseinheiten")]
        public int cost;

        [Header("Stat-Boni (additiv auf den vorherigen Stand)")]
        public float damageBonus;
        public float attackSpeedBonus;
        public float rangeBonus;

        [Header("Projektil-Austausch")]
        [Tooltip("Leer lassen, um den aktuellen Bullet-Typ beizubehalten")]
        public BulletData overrideBulletData;
    }

    [Tooltip("Upgrade-Stufen in aufsteigender Reihenfolge (Index 0 = erste Verbesserung)")]
    public List<UpgradeLevel> levels = new List<UpgradeLevel>();
}
