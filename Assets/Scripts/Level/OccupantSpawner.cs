using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns Rock and Destructible GameObjects from a level definition's
/// occupants list. Subscribes to <see cref="LevelLoader"/> events so it works
/// for both campaign loads and editor test runs.
///
/// Visuals: runtime-generated 1x1 white sprites tinted per occupant type. The
/// theme system in Phase 7 may swap these for proper sprites.
/// </summary>
[DisallowMultipleComponent]
public class OccupantSpawner : MonoBehaviour
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
        if (definition == null || definition.occupants == null) return;
        if (gridManager == null) return;

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
