using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TD.Combat;
using TD.Core;
using TD.Grid;
using TD.Theming;
using TD.Towers;

/// <summary>
/// Spawns Rock and Destructible GameObjects from a level definition's
/// occupants list. Subscribes to <see cref="LevelLoader"/> events so it works
/// for both campaign loads and editor test runs.
///
/// Also routes left-clicks to Destructible.ToggleMarked using the new Input
/// System (Unity's built-in OnMouseDown is unreliable when the new Input
/// System is the active backend, so we do the raycast ourselves).
///
/// Visuals: runtime-generated 1x1 white sprites tinted per occupant type. The
/// theme system in Phase 7 may swap these for proper sprites.
/// </summary>

namespace TD.Level
{
    [DisallowMultipleComponent]
    public class OccupantSpawner : MonoBehaviour
    {
        [SerializeField] private LevelLoader levelLoader;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Camera clickCamera;
        [SerializeField] private BuildManager buildManager;
        [SerializeField] private bool verboseClickLogging = false;

        private static Sprite cachedQuadSprite;
        private readonly List<GameObject> spawned = new List<GameObject>();

        private void Awake()
        {
            if (levelLoader == null) levelLoader = FindAnyObjectByType<LevelLoader>();
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();
            if (clickCamera == null) clickCamera = Camera.main;
            if (buildManager == null) buildManager = FindAnyObjectByType<BuildManager>();
            if (verboseClickLogging)
                Debug.Log($"[OccupantSpawner] Awake on '{gameObject.name}'. levelLoader={(levelLoader != null)} gridManager={(gridManager != null)} camera={(clickCamera != null)} mouseAvailable={(Mouse.current != null)}");
        }

        private void Update()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (verboseClickLogging)
                Debug.Log("[OccupantSpawner] LMB detected.");

            if (buildManager != null && buildManager.IsPlacingTower)
            {
                if (verboseClickLogging) Debug.Log("[OccupantSpawner] Click ignored: tower placement active.");
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (verboseClickLogging) Debug.Log("[OccupantSpawner] Click ignored: pointer over UI.");
                return;
            }

            Camera cam = clickCamera != null ? clickCamera : Camera.main;
            if (cam == null)
            {
                if (verboseClickLogging) Debug.LogWarning("[OccupantSpawner] No camera found.");
                return;
            }

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPosition = cam.ScreenToWorldPoint(screenPos);
            Vector2 point = new Vector2(worldPosition.x, worldPosition.y);

            Collider2D[] hits = Physics2D.OverlapPointAll(point);
            if (verboseClickLogging) Debug.Log($"[OccupantSpawner] Click at {point} hit {hits.Length} colliders. Active destructibles: {Destructible.ActiveTargets.Count}");

            for (int i = 0; i < hits.Length; i++)
            {
                Destructible destructible = hits[i].GetComponentInParent<Destructible>();
                if (destructible != null)
                {
                    destructible.Mark();
                    if (verboseClickLogging) Debug.Log($"[OccupantSpawner] Focused {destructible.name}.");
                    return;
                }
            }

            // Fallback: distance-based hit test against the registry, in case the
            // physics raycast misses (collider disabled, layer mismatch, etc.).
            IReadOnlyList<Destructible> active = Destructible.ActiveTargets;
            float halfCell = gridManager != null ? gridManager.CellSize * 0.5f : 0.5f;
            for (int i = 0; i < active.Count; i++)
            {
                Destructible d = active[i];
                if (d == null) continue;
                Vector2 dp = d.transform.position;
                if (Mathf.Abs(dp.x - point.x) <= halfCell && Mathf.Abs(dp.y - point.y) <= halfCell)
                {
                    d.Mark();
                    if (verboseClickLogging) Debug.Log($"[OccupantSpawner] Fallback focused {d.name}.");
                    return;
                }
            }

            Destructible.ClearMarkedTargets();
            if (verboseClickLogging) Debug.Log("[OccupantSpawner] Cleared destructible focus.");
        }

        private void OnEnable()
        {
            if (levelLoader == null) return;
            levelLoader.OnMapLoaded += HandleMapLoaded;
            levelLoader.OnLevelLoaded += HandleLevelLoaded;

            // If this component is enabled after LevelLoader has already fired its
            // load event, catch up from the loader's current map snapshot.
            if (levelLoader.HasLoadedLevel && levelLoader.LoadedMapDefinition != null)
                HandleMapLoaded(levelLoader.LoadedMapDefinition);
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
            if (definition == null)
            {
                Debug.LogWarning("[OccupantSpawner] HandleMapLoaded called with null definition.");
                return;
            }
            if (gridManager == null)
            {
                Debug.LogWarning("[OccupantSpawner] HandleMapLoaded called but gridManager is null.");
                return;
            }

            if (verboseClickLogging)
            {
                int occupantCount = definition.occupants != null ? definition.occupants.Count : 0;
                Debug.Log($"[OccupantSpawner] HandleMapLoaded: occupants={occupantCount}.");
            }

            if (definition.occupants != null)
            {
                for (int i = 0; i < definition.occupants.Count; i++)
                {
                    OccupantEntry entry = definition.occupants[i];
                    switch (entry.type)
                    {
                        case OccupantType.Rock:
                            SpawnRock(entry);
                            break;
                        case OccupantType.Destructible:
                            SpawnDestructible(entry);
                            break;
                    }
                }
            }

            if (verboseClickLogging)
                Debug.Log($"[OccupantSpawner] After spawn: {spawned.Count} objects, {Destructible.ActiveTargets.Count} active destructibles.");
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                    Destroy(spawned[i]);
            }
            spawned.Clear();
        }

        private void SpawnRock(OccupantEntry entry)
        {
            Vector3 worldPosition = gridManager.CellToWorld(entry.cell);
            GameObject rockObject = new GameObject($"Rock_{entry.cell.x}_{entry.cell.y}", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rock));
            rockObject.transform.SetParent(transform, false);
            rockObject.transform.position = worldPosition;
            rockObject.transform.localScale = new Vector3(gridManager.CellSize * 0.85f, gridManager.CellSize * 0.85f, 1f);

            SpriteRenderer renderer = rockObject.GetComponent<SpriteRenderer>();
            renderer.sprite = GetOrCreateQuadSprite();
            renderer.color = new Color(0.32f, 0.32f, 0.34f);
            renderer.sortingOrder = 5;

            spawned.Add(rockObject);
        }

        private void SpawnDestructible(OccupantEntry entry)
        {
            Vector3 worldPosition = gridManager.CellToWorld(entry.cell);
            GameObject blockObject = new GameObject($"Destructible_{entry.cell.x}_{entry.cell.y}", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Destructible));
            blockObject.transform.SetParent(transform, false);
            blockObject.transform.position = worldPosition;
            blockObject.transform.localScale = new Vector3(gridManager.CellSize * 0.85f, gridManager.CellSize * 0.85f, 1f);

            SpriteRenderer renderer = blockObject.GetComponent<SpriteRenderer>();
            renderer.sprite = GetOrCreateQuadSprite();
            renderer.sortingOrder = 5;

            Destructible destructible = blockObject.GetComponent<Destructible>();
            destructible.Initialize(entry.maxHp, entry.reward, renderer, gridManager, entry.cell, SpawnDefaultGround);

            spawned.Add(blockObject);
        }

        private void SpawnDefaultGround(Vector2Int cell, Vector3 worldPosition)
        {
            GameObject groundObject = new GameObject($"Ground_Default_{cell.x}_{cell.y}", typeof(SpriteRenderer));
            groundObject.transform.SetParent(transform, false);
            groundObject.transform.position = worldPosition;
            groundObject.transform.localScale = new Vector3(gridManager != null ? gridManager.CellSize : 1f, gridManager != null ? gridManager.CellSize : 1f, 1f);

            SpriteRenderer renderer = groundObject.GetComponent<SpriteRenderer>();
            ApplyGroundVisual(renderer, GroundType.Ground);
            renderer.sortingOrder = -10;

            spawned.Add(groundObject);
        }

        private static void ApplyGroundVisual(SpriteRenderer renderer, GroundType type)
        {
            if (renderer == null)
                return;

            MapThemeDefinition theme = MapThemeApplier.Active != null ? MapThemeApplier.Active.ActiveTheme : null;
            if (theme != null && theme.TryGetGroundVisual(type, out MapThemeDefinition.GroundVisual visual))
            {
                renderer.sprite = visual.sprite != null ? visual.sprite : GetOrCreateQuadSprite();
                renderer.color = visual.tint.a > 0f ? visual.tint : Color.white;
                return;
            }

            renderer.sprite = GetOrCreateQuadSprite();
            renderer.color = Color.white;
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
