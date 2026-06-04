using System.Collections.Generic;
using UnityEngine;
using TD.Grid;
using TD.Theming;

/// <summary>
/// Renders ground overlay sprites for Elevated / Water / Lava cells during
/// gameplay. The headless GridManager.BuildGrid path does not create cell
/// visuals, so this component handles them so themed terrain is visible at
/// runtime - not only inside the editor preview.
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
        }

        private void OnDisable()
        {
            if (levelLoader == null) return;
            levelLoader.OnMapLoaded -= HandleMapLoaded;
            levelLoader.OnLevelLoaded -= HandleLevelLoaded;
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
            if (definition == null || definition.groundOverrides == null) return;
            if (gridManager == null) return;

            for (int i = 0; i < definition.groundOverrides.Count; i++)
            {
                GroundOverrideEntry entry = definition.groundOverrides[i];
                SpawnTile(entry.cell, entry.type);
            }
        }

        private void SpawnTile(Vector2Int cell, GroundType type)
        {
            Vector3 worldPosition = gridManager.CellToWorld(cell);
            GameObject tileObject = new GameObject($"Ground_{type}_{cell.x}_{cell.y}", typeof(SpriteRenderer));
            tileObject.transform.SetParent(transform, false);
            tileObject.transform.position = worldPosition;
            tileObject.transform.localScale = new Vector3(gridManager.CellSize, gridManager.CellSize, 1f);

            SpriteRenderer renderer = tileObject.GetComponent<SpriteRenderer>();

            // Prefer themed sprite/tint if a MapThemeApplier is present.
            MapThemeDefinition theme = MapThemeApplier.Active != null ? MapThemeApplier.Active.ActiveTheme : null;
            if (theme != null && theme.TryGetGroundVisual(type, out MapThemeDefinition.GroundVisual visual))
            {
                renderer.sprite = visual.sprite != null ? visual.sprite : GetOrCreateQuadSprite();
                renderer.color = visual.tint.a > 0f ? visual.tint : ColorFor(type);
            }
            else
            {
                renderer.sprite = GetOrCreateQuadSprite();
                renderer.color = ColorFor(type);
            }
            renderer.sortingOrder = -10; // below towers/enemies but above background

            spawned.Add(tileObject);
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Destroy(spawned[i]);
            spawned.Clear();
        }

        private static Color ColorFor(GroundType type)
        {
            switch (type)
            {
                case GroundType.Elevated: return new Color(0.7f, 0.66f, 0.55f);
                case GroundType.Water:    return new Color(0.25f, 0.55f, 0.85f);
                case GroundType.Lava:     return new Color(0.95f, 0.32f, 0.12f);
                default:                  return Color.white;
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
