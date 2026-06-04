using System.Collections.Generic;
using UnityEngine;
using TD.Level;

namespace TD.Grid
{
    /// <summary>
    /// Renders the debug/preview tile overlay used by the map-editor flows.
    /// Owns all preview GameObjects, the shared preview sprite and the per-cell
    /// visual lookups, keeping <see cref="GridManager"/> focused on grid state
    /// and placement rules. Runtime gameplay must not create these visuals.
    /// </summary>
    public sealed class GridPreviewRenderer
    {
        private readonly List<GameObject> previewCells = new List<GameObject>();
        private readonly Dictionary<Vector2Int, OccupantType> previewOccupants = new Dictionary<Vector2Int, OccupantType>();
        private readonly Dictionary<Vector2Int, GroundType> previewGroundOverrides = new Dictionary<Vector2Int, GroundType>();
        private Sprite previewSprite;

        public void Build(GridManager grid, GameObject previewCellPrefab, float cellSize, Transform parent, LevelMapDefinition definition)
        {
            Clear();
            CacheLookups(definition);
            CreateVisuals(grid, previewCellPrefab, cellSize, parent);
        }

        public void Clear()
        {
            for (int i = 0; i < previewCells.Count; i++)
            {
                if (previewCells[i] != null)
                    DestroyUnityObject(previewCells[i]);
            }
            previewCells.Clear();
        }

        public void DisposeSprite()
        {
            if (previewSprite == null)
                return;

            Texture2D texture = previewSprite.texture;
            DestroyUnityObject(previewSprite);
            if (texture != null)
                DestroyUnityObject(texture);
            previewSprite = null;
        }

        private void CacheLookups(LevelMapDefinition definition)
        {
            previewOccupants.Clear();
            previewGroundOverrides.Clear();
            if (definition == null)
                return;

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

        private void CreateVisuals(GridManager grid, GameObject previewCellPrefab, float cellSize, Transform parent)
        {
            if (grid == null || !grid.HasGrid)
                return;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Vector3 worldPos = grid.CellToWorld(pos);
                    GameObject cellObj;

                    if (previewCellPrefab != null)
                    {
                        cellObj = Object.Instantiate(previewCellPrefab, worldPos, Quaternion.identity, parent);
                    }
                    else
                    {
                        cellObj = new GameObject($"PreviewCell_{x}_{y}");
                        cellObj.transform.SetParent(parent);
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
                        renderer.color = GetPreviewCellColor(grid, pos);
                        if (renderer.sortingOrder == 0)
                            renderer.sortingOrder = -1;
                    }

                    previewCells.Add(cellObj);
                }
            }
        }

        private Color GetPreviewCellColor(GridManager grid, Vector2Int cellPosition)
        {
            if (cellPosition == grid.StartCell)
                return Color.green;

            if (cellPosition == grid.GoalCell)
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

            GridCell cell = grid.GetCell(cellPosition);
            if (cell == null)
                return Color.magenta;

            if (cell.IsBlocked)
                return new Color(0.28f, 0.3f, 0.33f);

            if (cell.IsPath)
                return new Color(1f, 0.78f, 0.2f);

            if (grid.IsReservedPathCell(cellPosition))
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

        private static void DestroyUnityObject(Object objectToDestroy)
        {
            if (objectToDestroy == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(objectToDestroy);
            else
                Object.DestroyImmediate(objectToDestroy);
        }
    }
}
