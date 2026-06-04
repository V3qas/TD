using System;
using System.Collections.Generic;
using UnityEngine;
using TD.Combat;
using TD.Enemies;
using TD.Level;
using TD.Pathfinding;

namespace TD.Grid
{
    public class GridManager : MonoBehaviour
    {
        [SerializeField] private float cellSize = 1f;

        [Header("Preview Visuals (editor/runtime map editor)")]
        [SerializeField] private GameObject previewCellPrefab;

        private GridCell[,] grid;
        private readonly HashSet<Vector2Int> reservedPathCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, GroundType> runtimeGroundOverrides = new Dictionary<Vector2Int, GroundType>();

        private readonly GridPreviewRenderer previewRenderer = new GridPreviewRenderer();

        // Cached Start-to-goal path (BFS result). Recomputed on grid build and occupancy changes.
        private List<GridCell> cachedEnemyPath;
        private HashSet<Vector2Int> cachedEnemyPathLookup = new HashSet<Vector2Int>();
        private bool usesExplicitPath;

        public event Action OnPathChanged;

        public Vector2Int StartCell { get; private set; }
        public Vector2Int GoalCell { get; private set; }
        public float CellSize => cellSize;
        public bool HasGrid => grid != null;
        public bool UsesExplicitPath => usesExplicitPath;
        public int Width => grid != null ? grid.GetLength(0) : 0;
        public int Height => grid != null ? grid.GetLength(1) : 0;

        public List<Vector3> GetCachedEnemyPathWorld()
        {
            if (cachedEnemyPath == null || cachedEnemyPath.Count == 0)
                return null;

            List<Vector3> world = new List<Vector3>(cachedEnemyPath.Count);
            for (int i = 0; i < cachedEnemyPath.Count; i++)
                world.Add(CellToWorld(cachedEnemyPath[i].Position));
            return world;
        }

        public void BuildGrid(LevelData levelData)
        {
            if (levelData == null)
            {
                Debug.LogError("GridManager: LevelData is missing.");
                return;
            }

            if (!levelData.TryGetMapDefinition(out LevelMapDefinition definition, out string validationError))
            {
                Debug.LogError($"GridManager: LevelData is invalid ({validationError}).");
                return;
            }

            BuildGrid(definition);
        }

        public void BuildGrid(LevelMapDefinition definition)
        {
            BuildGridInternal(definition, true);
        }

        public void BuildGridPreview(LevelMapDefinition definition)
        {
            BuildGridInternal(definition, false);
            previewRenderer.Build(this, previewCellPrefab, cellSize, transform, definition);
        }

        public void ClearPreviewVisuals()
        {
            previewRenderer.Clear();
        }

        private void BuildGridInternal(LevelMapDefinition definition, bool validateMap)
        {
            ClearGrid();

            if (definition == null)
            {
                Debug.LogError("GridManager: Map data is missing.");
                return;
            }

            if (validateMap && !LevelMapValidator.Validate(definition, false, out string validationError))
            {
                Debug.LogError($"GridManager: Map data is invalid ({validationError}).");
                return;
            }

            LevelMapDefinition normalizedDefinition = definition.CloneNormalized();

            StartCell = normalizedDefinition.startCell;
            GoalCell = normalizedDefinition.goalCell;
            usesExplicitPath = normalizedDefinition.HasExplicitPath;

            HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
            HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>(normalizedDefinition.pathCells);

            // Occupants (Rock, Destructible) act as blockers in pathfinding/build checks.
            // After Normalize, legacy v1 blockedCells have already been migrated into occupants,
            // so this is the single source of truth for blocked tiles.
            if (normalizedDefinition.occupants != null)
            {
                for (int i = 0; i < normalizedDefinition.occupants.Count; i++)
                {
                    OccupantEntry occupant = normalizedDefinition.occupants[i];
                    if (occupant.type != OccupantType.None)
                        blockedCells.Add(occupant.cell);
                }
            }

            runtimeGroundOverrides.Clear();
            if (normalizedDefinition.groundOverrides != null)
            {
                for (int i = 0; i < normalizedDefinition.groundOverrides.Count; i++)
                {
                    GroundOverrideEntry entry = normalizedDefinition.groundOverrides[i];
                    runtimeGroundOverrides[entry.cell] = entry.type;
                }
            }

            grid = new GridCell[normalizedDefinition.width, normalizedDefinition.height];

            for (int row = 0; row < normalizedDefinition.height; row++)
            {
                for (int column = 0; column < normalizedDefinition.width; column++)
                {
                    Vector2Int cellPosition = new Vector2Int(column, row);
                    bool isBlocked = blockedCells.Contains(cellPosition);
                    bool isPath = pathCells.Contains(cellPosition);
                    grid[column, row] = new GridCell(column, row, isBlocked, isPath);
                }
            }

            BuildReservedPathCells(validateMap);
        }

        public GridCell GetCell(int x, int y)
        {
            if (grid == null)
                return null;

            if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1))
                return null;

            return grid[x, y];
        }

        public GridCell GetCell(Vector2Int position)
        {
            return GetCell(position.x, position.y);
        }

        public List<GridCell> GetNeighbors(GridCell cell)
        {
            List<GridCell> neighbors = new List<GridCell>(4);

            if (cell == null)
                return neighbors;

            TryAddNeighbor(neighbors, cell.X + 1, cell.Y);
            TryAddNeighbor(neighbors, cell.X - 1, cell.Y);
            TryAddNeighbor(neighbors, cell.X, cell.Y + 1);
            TryAddNeighbor(neighbors, cell.X, cell.Y - 1);

            return neighbors;
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x / cellSize);
            int y = Mathf.FloorToInt(worldPosition.y / cellSize);

            return new Vector2Int(x, y);
        }

        public Vector3 CellToWorld(Vector2Int cellPosition)
        {
            return new Vector3((cellPosition.x + 0.5f) * cellSize, (cellPosition.y + 0.5f) * cellSize, 0f);
        }

        public bool IsReservedPathCell(Vector2Int cellPosition)
        {
            return reservedPathCells.Contains(cellPosition);
        }

        public bool IsPathCell(Vector2Int cellPosition)
        {
            GridCell cell = GetCell(cellPosition);
            return cell != null && cell.IsPath;
        }

        public bool CanEnemyWalkOn(GridCell cell)
        {
            if (cell == null || cell.IsBlocked || cell.IsOccupied)
                return false;

            if (!usesExplicitPath)
                return true;

            return cell.IsPath || cell.Position == StartCell || cell.Position == GoalCell;
        }

        public bool CanBuildAt(Vector2Int cellPosition)
        {
            GridCell cell = GetCell(cellPosition);

            if (cell == null)
                return false;

            if (cellPosition == StartCell || cellPosition == GoalCell)
                return false;

            if (IsPathCell(cellPosition))
                return false;

            if (IsReservedPathCell(cellPosition))
                return false;

            if (cell.IsBlocked || cell.IsOccupied)
                return false;

            // Water and Lava ground overrides occupy the cell visually and block placement.
            GroundType ground = GetGroundType(cellPosition);
            if (ground == GroundType.Water || ground == GroundType.Lava)
                return false;

            return !WouldOccupyingCellBlockPath(cellPosition);
        }

        /// <summary>
        /// Returns the ground type for a cell, defaulting to <see cref="GroundType.Ground"/>
        /// when no override is set. Path cells always read as <see cref="GroundType.Path"/>.
        /// </summary>
        public GroundType GetGroundType(Vector2Int cellPosition)
        {
            if (runtimeGroundOverrides.TryGetValue(cellPosition, out GroundType type))
                return type;
            GridCell cell = GetCell(cellPosition);
            if (cell != null && cell.IsPath)
                return GroundType.Path;
            return GroundType.Ground;
        }

        public bool TryOccupyCell(Vector2Int cellPosition)
        {
            if (!CanBuildAt(cellPosition))
                return false;

            GridCell cell = GetCell(cellPosition);
            if (cell == null)
                return false;

            cell.SetOccupied(true);
            RecomputeCachedPath();

            return true;
        }

        public void ClearOccupiedCell(Vector2Int cellPosition)
        {
            GridCell cell = GetCell(cellPosition);
            if (cell == null)
                return;

            cell.SetOccupied(false);
            RecomputeCachedPath();
        }

        public void ClearBlockedCell(Vector2Int cellPosition)
        {
            GridCell cell = GetCell(cellPosition);
            if (cell == null || !cell.IsBlocked)
                return;

            cell.SetBlocked(false);
            RecomputeCachedPath();
        }

        public bool WouldOccupyingCellBlockPath(Vector2Int cellPosition)
        {
            GridCell cell = GetCell(cellPosition);

            if (cell == null)
                return true;

            // Fast path: cells outside the current enemy path cannot block it.
            if (cachedEnemyPath != null && cachedEnemyPath.Count > 0
                && !cachedEnemyPathLookup.Contains(cellPosition))
            {
                return false;
            }

            bool originalOccupied = cell.IsOccupied;
            cell.SetOccupied(true);

            Pathfinder pathfinder = new Pathfinder(this);
            bool hasPath = pathfinder.HasPath(StartCell, GoalCell);

            cell.SetOccupied(originalOccupied);

            return !hasPath;
        }

        private void TryAddNeighbor(List<GridCell> neighbors, int x, int y)
        {
            GridCell neighbor = GetCell(x, y);
            if (neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }

        private void BuildReservedPathCells(bool warnIfNoPath)
        {
            reservedPathCells.Clear();

            if (usesExplicitPath)
            {
                for (int row = 0; row < grid.GetLength(1); row++)
                {
                    for (int column = 0; column < grid.GetLength(0); column++)
                    {
                        GridCell cell = grid[column, row];
                        if (cell != null && cell.IsPath)
                            reservedPathCells.Add(cell.Position);
                    }
                }
            }
            else if (!warnIfNoPath)
            {
                cachedEnemyPath = null;
                cachedEnemyPathLookup.Clear();
                return;
            }

            Pathfinder pathfinder = new Pathfinder(this);
            List<GridCell> path = pathfinder.FindPath(StartCell, GoalCell);

            if (path == null || path.Count == 0)
            {
                if (warnIfNoPath)
                    Debug.LogWarning("GridManager: No reserved enemy path found.");

                cachedEnemyPath = null;
                cachedEnemyPathLookup.Clear();
                return;
            }

            foreach (GridCell pathCell in path)
                reservedPathCells.Add(pathCell.Position);

            cachedEnemyPath = path;
            RebuildCachedPathLookup();
        }

        private void RecomputeCachedPath()
        {
            if (grid == null)
                return;

            Pathfinder pathfinder = new Pathfinder(this);
            List<GridCell> path = pathfinder.FindPath(StartCell, GoalCell);

            bool changed = !PathsAreEqual(cachedEnemyPath, path);

            cachedEnemyPath = (path != null && path.Count > 0) ? path : null;
            RebuildCachedPathLookup();

            if (changed)
                OnPathChanged?.Invoke();
        }

        private void RebuildCachedPathLookup()
        {
            cachedEnemyPathLookup.Clear();
            if (cachedEnemyPath == null)
                return;
            for (int i = 0; i < cachedEnemyPath.Count; i++)
                cachedEnemyPathLookup.Add(cachedEnemyPath[i].Position);
        }

        private static bool PathsAreEqual(List<GridCell> a, List<GridCell> b)
        {
            int countA = a?.Count ?? 0;
            int countB = b?.Count ?? 0;
            if (countA != countB)
                return false;
            for (int i = 0; i < countA; i++)
            {
                if (a[i].Position != b[i].Position)
                    return false;
            }
            return true;
        }

        private void ClearGrid()
        {
            previewRenderer.Clear();

            reservedPathCells.Clear();
            cachedEnemyPath = null;
            cachedEnemyPathLookup.Clear();
            usesExplicitPath = false;
            grid = null;
        }

        private void OnDestroy()
        {
            previewRenderer.DisposeSprite();
        }
    }
}
