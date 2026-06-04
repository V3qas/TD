using UnityEngine;

namespace TD.Combat
{
    /// <summary>
    /// Supplies a tower with a target to attack. Decouples towers from the
    /// concrete global registries (active enemies, marked destructibles) so the
    /// selection strategy can be swapped or faked in tests.
    /// </summary>
    public interface ITargetProvider
    {
        /// <summary>
        /// Returns the best target within <paramref name="range"/> of
        /// <paramref name="origin"/>, or null when nothing is in range.
        /// </summary>
        IDamageable FindTarget(Vector3 origin, float range);
    }
}
