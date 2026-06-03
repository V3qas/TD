using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
        Debug.Log($"[OccupantSpawner] Awake on '{gameObject.name}'. levelLoader={(levelLoader!=null)} gridManager={(gridManager!=null)} camera={(clickCamera!=null)} mouseAvailable={(Mouse.current!=null)}");
    }

    private void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
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
                destructible.ToggleMarked();
                if (verboseClickLogging) Debug.Log($"[OccupantSpawner] Toggled mark on {destructible.name}, marked={destructible.IsMarked}.");
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
                d.ToggleMarked();
                if (verboseClickLogging) Debug.Log($"[OccupantSpawner] Fallback toggled {d.name}, marked={d.IsMarked}.");
                return;
            }
        }
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

        int occupantCount = definition.occupants != null ? definition.occupants.Count : 0;
        int legacyBlockedCount = definition.blockedCells != null ? definition.blockedCells.Count : 0;
        Debug.Log($"[OccupantSpawner] HandleMapLoaded: occupants={occupantCount}, legacy blockedCells={legacyBlockedCount}.");

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

        // Legacy fallback: spawn rocks for any blockedCells that aren't already
        // covered by an occupants entry, so old maps keep working visually.
        if (definition.blockedCells != null)
        {
            for (int i = 0; i < definition.blockedCells.Count; i++)
            {
                Vector2Int cell = definition.blockedCells[i];
                bool alreadyCovered = false;
                if (definition.occupants != null)
                {
                    for (int j = 0; j < definition.occupants.Count; j++)
                    {
                        if (definition.occupants[j].cell == cell) { alreadyCovered = true; break; }
                    }
                }
                if (!alreadyCovered)
                    SpawnRock(new OccupantEntry { cell = cell, type = OccupantType.Rock });
            }
        }

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
        destructible.Initialize(entry.maxHp, entry.reward, renderer);

        spawned.Add(blockObject);
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
