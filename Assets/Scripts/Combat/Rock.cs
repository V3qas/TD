using UnityEngine;

/// <summary>
/// Indestructible obstacle. Pure marker component used for cell occupancy and
/// distinct visuals; gameplay logic that cares about pathfinding/build checks
/// is handled by the grid (rocks are baked into the blocked set on level load).
/// </summary>

namespace TD.Combat
{
    public class Rock : MonoBehaviour
    {
    }
}
