using System;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] private float cellSize = 1f;

    [Header("Preview Visuals (editor/runtime map editor)")]
    [SerializeField] private GameObject previewCellPrefab;

    private GridCell[,] grid;
    private readonly HashSet<Vector2Int> reservedPathCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, GroundType> runtimeGroundOverrides = new Dictionary<Vector2Int, GroundType>();

    // Runtime preview objects created by BuildGridPreview
    private readonly List<GameObject> previewCells = new List<GameObject>();
    private Sprite previewSprite;
    private LevelMapDefinition previewDefinition;
    private readonly Dictionary<Vector2Int, OccupantType> previewOccupants = new Dictionary<Vector2Int, OccupantType>();
    private readonly Dictionary<Vector2Int, GroundType> previewGroundOverrides = new Dictionary<Vector2Int, GroundType>();

    // Cached Start→Goal path (BFS result). Recomputed on grid build and occupancy changes.
    private List<GridCell> cachedEnemyPath;
    private HashSet<Vector2Int> cachedEnemyPathLookup = new HashSet<Vector2Int>();
    private bool usesExplicitPath;

    public event Action OnPathChanged;

    public Vector2Int StartCell { get; private set; }
    public Vector2Int GoalCell { get; private set; }
    public float CellSize => cellSize;
    public bool HasGrid => grid != null;
    public bool UsesExplicitPath => usesExplicitPath;

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
        CachePreviewLookups(definition);
        CreatePreviewVisuals();
    }

    private void CachePreviewLookups(LevelMapDefinition definition)
    {
        previewDefinition = definition;
        previewOccupants.Clear();
        previewGroundOverrides.Clear();
        if (definition == null) return;
        if (definition.occupants != null)
        {
            for (int i = 0; i < definition.occupants.Count; i++)
            {
                OccupantEntry entry = definition.occupants[i];
                previewOccupants[entry.cell] = entry.type;
            }
        }
        if (definition.groundOverrides != null)
        {
            for (int i = 0; i < definition.groundOverrides.Count; i++)
            {
                GroundOverrideEntry entry = definition.groundOverrides[i];
                previewGroundOverrides[entry.cell] = entry.type;
            }
        }
    }

    public void ClearPreviewVisuals()
    {
        DestroyPreviewVisuals();
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

        HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>(normalizedDefinition.blockedCells);
        HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>(normalizedDefinition.pathCells);

        // New occupants (Rock, Destructible) act as blockers in pathfinding/build checks until
        // Phase 5 introduces dedicated runtime objects.
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
        DestroyPreviewVisuals();

        reservedPathCells.Clear();
        cachedEnemyPath = null;
        cachedEnemyPathLookup.Clear();
        usesExplicitPath = false;
        grid = null;
    }

    private void CreatePreviewVisuals()
    {
        DestroyPreviewVisuals();

        if (grid == null)
            return;

        for (int y = 0; y < grid.GetLength(1); y++)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                Vector3 worldPos = CellToWorld(pos);
                GameObject cellObj = null;

                if (previewCellPrefab != null)
                {
                    cellObj = Instantiate(previewCellPrefab, worldPos, Quaternion.identity, transform);
                }
                else
                {
                    cellObj = new GameObject($"PreviewCell_{x}_{y}");
                    cellObj.transform.SetParent(transform);
                    cellObj.transform.position = worldPos;

                    SpriteRenderer sr = cellObj.AddComponent<SpriteRenderer>();
                    sr.sprite = GetOrCreatePreviewSprite();
                    sr.sortingOrder = -1;
                }

                // ensure tile covers the configured cell size
                cellObj.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                SpriteRenderer renderer = cellObj.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = GetPreviewCellColor(pos);
                    if (renderer.sortingOrder == 0)
                        renderer.sortingOrder = -1;
                }

                previewCells.Add(cellObj);
            }
        }
    }

    private Color GetPreviewCellColor(Vector2Int cellPosition)
    {
        if (cellPosition == StartCell)
            return Color.green;

        if (cellPosition == GoalCell)
            return Color.red;

        if (previewOccupants.TryGetValue(cellPosition, out OccupantType occupant))
        {
            return occupant == OccupantType.Rock
                ? new Color(0.32f, 0.32f, 0.34f)
                : new Color(0.55f, 0.42f, 0.28f);
        }

        if (previewGroundOverrides.TryGetValue(cellPosition, out GroundType ground))
        {
            switch (ground)
            {
                case GroundType.Elevated: return new Color(0.7f, 0.66f, 0.55f);
                case GroundType.Water:    return new Color(0.25f, 0.55f, 0.85f);
                case GroundType.Lava:     return new Color(0.95f, 0.32f, 0.12f);
            }
        }

        GridCell cell = GetCell(cellPosition);
        if (cell == null)
            return Color.magenta;

        if (cell.IsBlocked)
            return new Color(0.28f, 0.3f, 0.33f);

        if (cell.IsPath)
            return new Color(1f, 0.78f, 0.2f);

        if (IsReservedPathCell(cellPosition))
            return new Color(1f, 0.78f, 0.2f);

        if (cell.IsOccupied)
            return Color.blue;

        return Color.white;
    }

    private Sprite GetOrCreatePreviewSprite()
    {
        if (previewSprite != null)
            return previewSprite;

        Texture2D texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        previewSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        previewSprite.hideFlags = HideFlags.HideAndDontSave;
        return previewSprite;
    }

    private void DestroyPreviewVisuals()
    {
        for (int i = 0; i < previewCells.Count; i++)
        {
            if (previewCells[i] != null)
                DestroyUnityObject(previewCells[i]);
        }
        previewCells.Clear();
    }

    private void OnDestroy()
    {
        if (previewSprite != null)
        {
            Texture2D texture = previewSprite.texture;
            DestroyUnityObject(previewSprite);
            if (texture != null)
                DestroyUnityObject(texture);
            previewSprite = null;
        }
    }

    private static void DestroyUnityObject(UnityEngine.Object objectToDestroy)
    {
        if (objectToDestroy == null)
            return;

        if (Application.isPlaying)
            Destroy(objectToDestroy);
        else
            DestroyImmediate(objectToDestroy);
    }
}
