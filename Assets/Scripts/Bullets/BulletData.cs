using UnityEngine;
using TD.Towers;

namespace TD.Bullets
{
    [CreateAssetMenu(fileName = "BulletData", menuName = "TowerDefense/Bullet Data")]
    public class BulletData : ScriptableObject
    {
        [Header("Info")]
        public string bulletName;

        [Header("Movement")]
        [Tooltip("Flight speed in units per second.")]
        public float travelSpeed = 8f;

        [Header("Damage")]
        [Tooltip("Multiplier applied to the tower base damage.")]
        public float damageMultiplier = 1f;

        [Header("Special Effects")]
        [Tooltip("Hits all enemies in the impact radius. Zero disables splash.")]
        public float splashRadius = 0f;

        [Tooltip("Passes through enemies and continues until its range ends.")]
        public bool isPiercing = false;

        [Header("Slow Effect")]
        [Tooltip("Speed factor after impact. One disables slow.")]
        [Range(0.1f, 1f)]
        public float slowFactor = 1f;

        [Tooltip("Slow duration in seconds. Zero disables slow.")]
        public float slowDuration = 0f;

        [Header("Visuals")]
        [Tooltip("Projectile prefab. Must contain a Bullet component.")]
        public GameObject bulletPrefab;

        [Tooltip("Optional animator for impact animations.")]
        public RuntimeAnimatorController hitAnimator;
    }
}
