using UnityEngine;
using TD.Bullets;
using TD.Towers;
using TD.UI;

/// <summary>
/// Anything that can take damage from a tower's bullet (enemies, destructible blocks).
/// HealthBar binds to this contract so it works for any future damageable type.
/// </summary>

namespace TD.Combat
{
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }
        Vector3 WorldPosition { get; }

        void TakeDamage(float damage);
    }
}
