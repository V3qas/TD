using UnityEngine;

namespace TD.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "TowerDefense/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public const float MinimumSpeed = 0.01f;

        [Header("Info")]
        public string enemyName;

        [Header("Stats")]
        [Tooltip("Total hit points.")]
        [Min(1f)]
        public float maxHealth = 100f;

        [Tooltip("Movement speed in units per second.")]
        [Min(MinimumSpeed)]
        public float speed = 2f;

        [Tooltip("Shield points. Shield absorbs damage before health and ignores armor.")]
        public float shield = 0f;

        [Tooltip("Flat damage reduction per hit after shield damage.")]
        [Min(0f)] public float armor = 0f;

        [Tooltip("Base lives removed when this enemy reaches the goal.")]
        [Min(1)] public int goalDamage = 1;

        [Header("Reward")]
        [Tooltip("Gold awarded when this enemy is killed.")]
        public int reward = 10;
    }
}
