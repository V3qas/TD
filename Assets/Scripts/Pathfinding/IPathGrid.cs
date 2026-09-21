using TD.Grid;
using UnityEngine;

namespace TD.Pathfinding
{
    public interface IPathGrid
    {
        GridCell GetCell(Vector2Int position);
        bool CanEnemyWalkOn(GridCell cell);
    }
}
