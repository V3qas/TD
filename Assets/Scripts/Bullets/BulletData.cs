using UnityEngine;
using UnityEngine.Serialization;

namespace TD.Bullets
{
    public enum BulletType
    {
        Projectile,
        Laser
    }

    [CreateAssetMenu(fileName = "BulletData", menuName = "TowerDefense/Bullet Data")]
    public class BulletData : ScriptableObject
    {
        [Header("Info")]
        public string bulletName;

        [Header("Movement")]
        [Tooltip("Flight speed in units per second.")]
        public float travelSpeed = 8f;

        [Tooltip("Controls whether this bullet flies to one target or fires as an instant beam.")]
        public BulletType bulletType = BulletType.Projectile;

        [Tooltip("Distance covered by a laser beam. Set this longer than the largest map diagonal.")]
        [Min(0.1f)]
        public float maxTravelDistance = 20f;

        [Tooltip("Visible width and hit radius of a laser beam.")]
        [Min(0.01f)]
        public float beamWidth = 0.12f;

        [Tooltip("How long a laser beam remains visible after dealing damage.")]
        [Min(0.01f)]
        public float beamDuration = 0.08f;

        [Header("Damage")]
        [Tooltip("Multiplier applied to the tower base damage.")]
        public float damageMultiplier = 1f;

        [Header("Tower Modifiers")]
        [Tooltip("Multiplier applied to the tower's attacks per second while this bullet type is active.")]
        [Min(0.01f)]
        public float attackSpeedMultiplier = 1f;

        [Tooltip("Multiplier applied to the tower's targeting range while this bullet type is active.")]
        [Min(0.01f)]
        public float rangeMultiplier = 1f;

        [Header("Special Effects")]
        [Tooltip("Hits all enemies in the impact radius. Zero disables splash.")]
        public float splashRadius = 0f;

        [FormerlySerializedAs("isPiercing")]
        [Tooltip("When enabled, a laser damages every valid target along its beam. Ignored for projectiles.")]
        public bool laserPiercing = false;

        [Header("Slow Effect")]
        [Tooltip("Speed factor after impact. One disables slow.")]
        [Range(0.1f, 1f)]
        public float slowFactor = 1f;

        [Tooltip("Slow duration in seconds. Zero disables slow.")]
        public float slowDuration = 0f;

        [Header("Visuals")]
        [Tooltip("Color applied to the tower's ammunition accent visuals while this bullet type is active.")]
        public Color towerAccentColor = Color.white;

        [Tooltip("Projectile prefab. Must contain a Bullet component.")]
        public GameObject bulletPrefab;

        public float ModifyDamage(float baseDamage)
        {
            return Mathf.Max(0f, baseDamage * damageMultiplier);
        }

        public float ModifyAttackSpeed(float baseAttackSpeed)
        {
            return Mathf.Max(0.01f, baseAttackSpeed * attackSpeedMultiplier);
        }

        public float ModifyRange(float baseRange)
        {
            return Mathf.Max(0f, baseRange * rangeMultiplier);
        }
    }
}
