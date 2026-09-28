using System;
using System.Collections.Generic;
using UnityEngine;
using TD.Combat;
using TD.Enemies;
using TD.Level;
using TD.Pathfinding;

namespace TD.Grid
{
    public class GridManager : MonoBehaviour, IPathGrid
    {
        [SerializeField] private float cellSize = 1f;

        [Header("Preview Visuals (editor/runtime map editor)")]
        [SerializeField] private GameObject previewCellPrefab;

        private GridCell[,] grid;
        private readonly HashSet<Vector2Int> reservedPathCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, GroundType> runtimeGroundOverrides = new Dictionary<Vector2Int, GroundType>();

        private readonly GridPreviewRenderer previewRenderer = new GridPreviewRenderer();

        // Explicit maps retain every authored sequence in its authored order. Open maps contain
        // one dynamic BFS route. The lookup is the union and keeps build checks inexpensive.
        private readonly List<List<GridCell>> cachedEnemyPaths = new List<List<GridCell>>();
        private readonly HashSet<Vector2Int> cachedEnemyPathLookup = new HashSet<Vector2Int>();
        private readonly List<List<Vector2Int>> authoredEnemyPaths = new List<List<Vector2Int>>();
        private bool usesExplicitPath;

        public event Action OnPathChanged;

        public Vector2Int StartCell { get; private set; }
        public Vector2Int GoalCell { get; private set; }
        public float CellSize => cellSize;
        public bool HasGrid => grid != null;
        public bool UsesExplicitPath => usesExplicitPath;
        public int Width => grid != null ? grid.GetLength(0) : 0;
        public int Height => grid != null ? grid.GetLength(1) : 0;
        public int CachedEnemyPathCount => cachedEnemyPaths.Count;

        public List<Vector3> GetCachedEnemyPathWorld()
        {
            return GetCachedEnemyPathWorld(0);
        }

        public List<Vector3> GetCachedEnemyPathWorld(int pathIndex)
        {
            if (pathIndex < 0 || pathIndex >= cachedEnemyPaths.Count)
                return null;

            List<GridCell> path = cachedEnemyPaths[pathIndex];
            List<Vector3> world = new List<Vector3>(path.Count);
            for (int i = 0; i < path.Count; i++)
                world.Add(CellToWorld(path[i].Position));
            return world;
        }

        public bool BuildGrid(LevelData levelData)
        {
            if (levelData == null)
            {
                Debug.LogError("GridManager: LevelData is missing.");
                return false;
            }

            if (!levelData.TryGetMapDefinition(out LevelMapDefinition definition, out string validationError))
            {
                Debug.LogError($"GridManager: LevelData is invalid ({validationError}).");
                return false;
            }

            return BuildGrid(definition);
        }

        public bool BuildGrid(LevelMapDefinition definition)
        {
            return BuildGridInternal(definition, true);
        }

        public void BuildGridPreview(LevelMapDefinition definition)
        {
            if (!BuildGridInternal(definition, false))
                return;

            previewRenderer.Build(this, previewCellPrefab, cellSize, transform, definition);
        }

        public void ClearPreviewVisuals()
        {
            previewRenderer.Clear();
        }

        public void UpdatePreviewCell(LevelMapAuthoringState authoringState, Vector2Int position)
        {
            GridCell cell = GetCell(position);
            if (cell == null || authoringState?.MapDefinition == null)
                return;

            StartCell = authoringState.MapDefinition.startCell;
            GoalCell = authoringState.MapDefinition.goalCell;
            usesExplicitPath = authoringState.HasExplicitPath;
            cell.SetPath(authoringState.IsPathCell(position));
            cell.SetBlocked(authoringState.TryGetOccupant(position, out OccupantEntry occupant)
                && occupant.type != OccupantType.None);
            cell.SetOccupied(false);
            if (authoringState.TryGetGroundOverride(position, out GroundType ground))
                runtimeGroundOverrides[position] = ground;
            else
                runtimeGroundOverrides.Remove(position);

            if (cell.IsPath)
                reservedPathCells.Add(position);
            else
                reservedPathCells.Remove(position);
            cachedEnemyPaths.Clear();
            cachedEnemyPathLookup.Clear();
            authoredEnemyPaths.Clear();
            previewRenderer.UpdateCell(this, authoringState, position);
        }

        private bool BuildGridInternal(LevelMapDefinition definition, bool validateMap)
        {
            if (definition == null)
            {
                Debug.LogError("GridManager: Map data is missing.");
                return false;
            }

            if (validateMap && !LevelMapValidator.Validate(definition, false, out string validationError))
            {
                Debug.LogError($"GridManager: Map data is invalid ({validationError}).");
                return false;
            }

            ClearGrid();
            LevelMapDefinition normalizedDefinition = definition.CloneNormalized();

            StartCell = normalizedDefinition.startCell;
            GoalCell = normalizedDefinition.goalCell;
            usesExplicitPath = normalizedDefinition.HasExplicitPath;
            CacheAuthoredEnemyPaths(normalizedDefinition.pathSequences);

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
            return true;
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
            if (cachedEnemyPaths.Count > 0
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

                List<List<GridCell>> explicitPaths = BuildAuthoredEnemyPaths();
                SetCachedEnemyPaths(explicitPaths);
                if (cachedEnemyPaths.Count == 0 && warnIfNoPath)
                    Debug.LogWarning("GridManager: No valid authored enemy paths found.");
                return;
            }

            if (!warnIfNoPath)
            {
                SetCachedEnemyPaths(null);
                return;
            }

            Pathfinder pathfinder = new Pathfinder(this);
            List<GridCell> path = pathfinder.FindPath(StartCell, GoalCell);

            if (path == null || path.Count == 0)
            {
                if (warnIfNoPath)
                    Debug.LogWarning("GridManager: No reserved enemy path found.");

                SetCachedEnemyPaths(null);
                return;
            }

            foreach (GridCell pathCell in path)
                reservedPathCells.Add(pathCell.Position);

            SetCachedEnemyPaths(new List<List<GridCell>> { path });
        }

        private void RecomputeCachedPath()
        {
            if (grid == null)
                return;

            List<List<GridCell>> paths;
            if (usesExplicitPath)
            {
                paths = BuildAuthoredEnemyPaths();
            }
            else
            {
                Pathfinder pathfinder = new Pathfinder(this);
                List<GridCell> path = pathfinder.FindPath(StartCell, GoalCell);
                paths = path != null && path.Count > 0
                    ? new List<List<GridCell>> { path }
                    : null;
            }

            bool changed = !PathCollectionsAreEqual(cachedEnemyPaths, paths);
            SetCachedEnemyPaths(paths);

            if (changed)
                OnPathChanged?.Invoke();
        }

        private void CacheAuthoredEnemyPaths(List<PathSequence> sequences)
        {
            authoredEnemyPaths.Clear();
            if (sequences == null)
                return;

            for (int i = 0; i < sequences.Count; i++)
            {
                PathSequence sequence = sequences[i];
                if (sequence?.cells == null || sequence.cells.Count == 0)
                    continue;
                authoredEnemyPaths.Add(new List<Vector2Int>(sequence.cells));
            }
        }

        private List<List<GridCell>> BuildAuthoredEnemyPaths()
        {
            if (authoredEnemyPaths.Count == 0)
                return null;

            List<List<GridCell>> paths = new List<List<GridCell>>(authoredEnemyPaths.Count);
            for (int pathIndex = 0; pathIndex < authoredEnemyPaths.Count; pathIndex++)
            {
                List<Vector2Int> authoredPath = authoredEnemyPaths[pathIndex];
                List<GridCell> path = new List<GridCell>(authoredPath.Count);
                for (int cellIndex = 0; cellIndex < authoredPath.Count; cellIndex++)
                {
                    GridCell cell = GetCell(authoredPath[cellIndex]);
                    if (!CanEnemyWalkOn(cell))
                        return null;
                    path.Add(cell);
                }
                paths.Add(path);
            }
            return paths;
        }

        private void SetCachedEnemyPaths(List<List<GridCell>> paths)
        {
            cachedEnemyPaths.Clear();
            cachedEnemyPathLookup.Clear();
            if (paths == null)
                return;

            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                List<GridCell> path = paths[pathIndex];
                if (path == null || path.Count == 0)
                    continue;
                cachedEnemyPaths.Add(path);
                for (int cellIndex = 0; cellIndex < path.Count; cellIndex++)
                    cachedEnemyPathLookup.Add(path[cellIndex].Position);
            }
        }

        private static bool PathCollectionsAreEqual(List<List<GridCell>> current, List<List<GridCell>> next)
        {
            int currentCount = current?.Count ?? 0;
            int nextCount = next?.Count ?? 0;
            if (currentCount != nextCount)
                return false;
            for (int i = 0; i < currentCount; i++)
                if (!PathsAreEqual(current[i], next[i]))
                    return false;
            return true;
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
            cachedEnemyPaths.Clear();
            cachedEnemyPathLookup.Clear();
            authoredEnemyPaths.Clear();
            usesExplicitPath = false;
            grid = null;
        }

    }
}
