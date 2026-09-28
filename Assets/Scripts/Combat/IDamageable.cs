using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>
        /// Predicts whether the listed raw damage packets would destroy this target
        /// when applied in order. Implementations account for shields, armor and
        /// other target-specific mitigation.
        /// </summary>
        bool WouldBeDestroyedBy(IReadOnlyList<float> incomingDamages);

        void TakeDamage(float damage);
    }
}
