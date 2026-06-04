using UnityEngine;

namespace TD.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "TowerDefense/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Info")]
        public string enemyName;

        [Header("Stats")]
        [Tooltip("Total hit points.")]
        public float maxHealth = 100f;

        [Tooltip("Movement speed in units per second.")]
        public float speed = 2f;

        [Tooltip("Shield points. Shield absorbs damage before health and ignores armor.")]
        public float shield = 0f;

        [Tooltip("Flat damage reduction per hit after shield damage.")]
        public float armor = 0f;

        [Header("Reward")]
        [Tooltip("Gold awarded when this enemy is killed.")]
        public int reward = 10;
    }
}
