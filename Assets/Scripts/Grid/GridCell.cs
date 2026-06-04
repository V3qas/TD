using UnityEngine;

namespace TD.Grid
{
    public class GridCell
    {
        public int X { get; }
        public int Y { get; }

        public bool IsBlocked { get; private set; }
        public bool IsOccupied { get; private set; }
        public bool IsPath { get; private set; }

        public Vector2Int Position => new Vector2Int(X, Y);

        public GridCell(int column, int row, bool isBlocked, bool isPath = false)
        {
            X = column;
            Y = row;
            IsBlocked = isBlocked;
            IsPath = isPath;
            IsOccupied = false;
        }

        public bool IsWalkable()
        {
            return !IsBlocked && !IsOccupied;
        }

        public void SetBlocked(bool blocked)
        {
            IsBlocked = blocked;
        }

        public void SetPath(bool path)
        {
            IsPath = path;
        }

        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
        }
    }
}
