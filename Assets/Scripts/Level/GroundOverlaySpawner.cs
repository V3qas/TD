using System.Collections.Generic;
using UnityEngine;
using TD.Core;
using TD.Grid;
using TD.Theming;

/// <summary>
/// Renders the complete ground grid during gameplay. The headless
/// GridManager.BuildGrid path does not create cell visuals, so this component
/// makes buildable ground, paths, start/goal cells and special terrain visible
/// outside the editor preview.
///
/// Sprites are runtime-generated 1x1 quads tinted per ground type. Phase 7
/// will swap these for proper themed sprites.
/// </summary>

namespace TD.Level
{
    [DisallowMultipleComponent]
    public class GroundOverlaySpawner : MonoBehaviour
    {
        [SerializeField] private LevelLoader levelLoader;
        [SerializeField] private GridManager gridManager;

        private static Sprite cachedQuadSprite;
        private readonly List<GameObject> spawned = new List<GameObject>();

        private void Awake()
        {
            if (levelLoader == null) levelLoader = FindAnyObjectByType<LevelLoader>();
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();
        }

        private void OnEnable()
        {
            if (levelLoader == null) return;
            levelLoader.OnMapLoaded += HandleMapLoaded;
            levelLoader.OnLevelLoaded += HandleLevelLoaded;
            levelLoader.OnLevelCleared += ClearSpawned;
        }

        private void OnDisable()
        {
            if (levelLoader == null) return;
            levelLoader.OnMapLoaded -= HandleMapLoaded;
            levelLoader.OnLevelLoaded -= HandleLevelLoaded;
            levelLoader.OnLevelCleared -= ClearSpawned;
        }

        private void HandleLevelLoaded(LevelData level)
        {
            if (level == null) return;
            if (level.TryGetMapDefinition(out LevelMapDefinition definition, out _))
                HandleMapLoaded(definition);
        }

        private void HandleMapLoaded(LevelMapDefinition definition)
        {
            ClearSpawned();
            if (definition == null) return;
            if (gridManager == null) return;

            // The map editor owns its own preview visuals. A test run switches
            // this renderer back on so it matches regular gameplay.
            if (GameSession.IsMapEditorSession && !GameSession.IsEditorTestRun)
                return;

            for (int y = 0; y < definition.height; y++)
            {
                for (int x = 0; x < definition.width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    SpawnTile(
                        cell,
                        definition.GetGround(cell),
                        cell == definition.startCell,
                        cell == definition.goalCell);
                }
            }
        }

        private void SpawnTile(Vector2Int cell, GroundType type, bool isStart, bool isGoal)
        {
            Vector3 worldPosition = gridManager.CellToWorld(cell);
            GameObject tileObject = new GameObject($"Ground_{type}_{cell.x}_{cell.y}", typeof(SpriteRenderer));
            tileObject.transform.SetParent(transform, false);
            tileObject.transform.position = worldPosition;
            float tileSize = gridManager.CellSize * 0.96f;
            tileObject.transform.localScale = new Vector3(tileSize, tileSize, 1f);

            SpriteRenderer renderer = tileObject.GetComponent<SpriteRenderer>();

            if (isStart)
            {
                renderer.sprite = GetOrCreateQuadSprite();
                renderer.color = new Color(0.2f, 0.72f, 0.34f);
            }
            else if (isGoal)
            {
                renderer.sprite = GetOrCreateQuadSprite();
                renderer.color = new Color(0.82f, 0.22f, 0.2f);
            }
            else
            {
                ApplyGroundVisual(renderer, type);
            }
            renderer.sortingOrder = -10; // below towers/enemies but above background

            spawned.Add(tileObject);
        }

        private static void ApplyGroundVisual(SpriteRenderer renderer, GroundType type)
        {
            MapThemeDefinition theme = MapThemeApplier.Active != null ? MapThemeApplier.Active.ActiveTheme : null;
            if (theme != null && theme.TryGetGroundVisual(type, out MapThemeDefinition.GroundVisual visual))
            {
                renderer.sprite = visual.sprite != null ? visual.sprite : GetOrCreateQuadSprite();
                renderer.color = visual.tint.a > 0f ? visual.tint : ColorFor(type);
                return;
            }

            renderer.sprite = GetOrCreateQuadSprite();
            renderer.color = ColorFor(type);
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null)
                {
                    spawned[i].SetActive(false);
                    Destroy(spawned[i]);
                }
            spawned.Clear();
        }

        private static Color ColorFor(GroundType type)
        {
            switch (type)
            {
                case GroundType.Ground:   return new Color(0.76f, 0.79f, 0.72f);
                case GroundType.Path:     return new Color(0.72f, 0.57f, 0.3f);
                case GroundType.Elevated: return new Color(0.7f, 0.66f, 0.55f);
                case GroundType.Water:    return new Color(0.25f, 0.55f, 0.85f);
                case GroundType.Lava:     return new Color(0.95f, 0.32f, 0.12f);
                default:                  return Color.magenta;
            }
        }

        private static Sprite GetOrCreateQuadSprite()
        {
            if (cachedQuadSprite != null) return cachedQuadSprite;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            cachedQuadSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return cachedQuadSprite;
        }
    }
}
