using System.Collections.Generic;
using UnityEngine;
using TD.Grid;

namespace TD.Pathfinding
{
    public class Pathfinder
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };
        private readonly IPathGrid grid;

        public Pathfinder(IPathGrid grid)
        {
            this.grid = grid;
        }

        public List<GridCell> FindPath(Vector2Int start, Vector2Int goal)
        {
            GridCell startCell = grid.GetCell(start);
            GridCell goalCell = grid.GetCell(goal);

            if (startCell == null || goalCell == null)
                return null;

            if (!grid.CanEnemyWalkOn(startCell) || !grid.CanEnemyWalkOn(goalCell))
                return null;

            Queue<GridCell> frontier = new Queue<GridCell>();
            Dictionary<GridCell, GridCell> cameFrom = new Dictionary<GridCell, GridCell>();

            frontier.Enqueue(startCell);
            cameFrom[startCell] = null;

            while (frontier.Count > 0)
            {
                GridCell current = frontier.Dequeue();

                if (current == goalCell)
                {
                    return ReconstructPath(cameFrom, goalCell);
                }

                foreach (Vector2Int direction in Directions)
                {
                    GridCell neighbor = grid.GetCell(current.Position + direction);

                    if (neighbor == null || !grid.CanEnemyWalkOn(neighbor))
                        continue;

                    if (cameFrom.ContainsKey(neighbor))
                        continue;

                    frontier.Enqueue(neighbor);
                    cameFrom[neighbor] = current;
                }
            }

            return null;
        }

        public bool HasPath(Vector2Int start, Vector2Int goal)
        {
            List<GridCell> path = FindPath(start, goal);
            return path != null && path.Count > 0;
        }

        private List<GridCell> ReconstructPath(Dictionary<GridCell, GridCell> cameFrom, GridCell goalCell)
        {
            List<GridCell> path = new List<GridCell>();
            GridCell current = goalCell;

            while (current != null)
            {
                path.Add(current);
                current = cameFrom[current];
            }

            path.Reverse();
            return path;
        }
    }
}
