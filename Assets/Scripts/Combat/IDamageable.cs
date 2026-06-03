using UnityEngine;

/// <summary>
/// Anything that can take damage from a tower's bullet (enemies, destructible blocks).
/// HealthBar binds to this contract so it works for any future damageable type.
/// </summary>
public interface IDamageable
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDead { get; }
    Vector3 WorldPosition { get; }

    void TakeDamage(float damage);
}
