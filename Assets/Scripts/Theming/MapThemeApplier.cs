using UnityEngine;

/// <summary>
/// Applies a <see cref="MapThemeDefinition"/> when a level loads. Spawns a
/// background sprite behind the grid and exposes the active theme to other
/// systems (e.g. <see cref="GroundOverlaySpawner"/>) through
/// <see cref="ActiveTheme"/>.
///
/// If <see cref="theme"/> is null nothing is rendered - gameplay falls back to
/// the GroundOverlaySpawner default tinted quads.
/// </summary>
[DisallowMultipleComponent]
public class MapThemeApplier : MonoBehaviour
{
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private MapThemeDefinition theme;
    [SerializeField] private int backgroundSortingOrder = -100;
    [SerializeField] private float backgroundPadding = 2f;

    private GameObject spawnedBackground;

    public static MapThemeApplier Active { get; private set; }
    public MapThemeDefinition ActiveTheme => theme;

    private void Awake()
    {
        if (levelLoader == null) levelLoader = FindAnyObjectByType<LevelLoader>();
        if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();
    }

    private void OnEnable()
    {
        Active = this;
        if (levelLoader == null) return;
        levelLoader.OnMapLoaded += HandleMapLoaded;
        levelLoader.OnLevelLoaded += HandleLevelLoaded;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        if (levelLoader == null) return;
        levelLoader.OnMapLoaded -= HandleMapLoaded;
        levelLoader.OnLevelLoaded -= HandleLevelLoaded;
    }

    public void SetTheme(MapThemeDefinition newTheme)
    {
        theme = newTheme;
        RebuildBackground();
    }

    private void HandleLevelLoaded(LevelData level)
    {
        if (level == null) return;
        if (level.TryGetMapDefinition(out LevelMapDefinition definition, out _))
            HandleMapLoaded(definition);
    }

    private void HandleMapLoaded(LevelMapDefinition definition)
    {
        RebuildBackground(definition);
    }

    private void RebuildBackground(LevelMapDefinition definition = null)
    {
        if (spawnedBackground != null) Destroy(spawnedBackground);
        if (theme == null || gridManager == null) return;

        int width = definition != null ? definition.width : gridManager.Width;
        int height = definition != null ? definition.height : gridManager.Height;
        if (width <= 0 || height <= 0) return;

        spawnedBackground = new GameObject("MapBackground", typeof(SpriteRenderer));
        spawnedBackground.transform.SetParent(transform, false);

        SpriteRenderer renderer = spawnedBackground.GetComponent<SpriteRenderer>();
        renderer.sprite = theme.backgroundSprite != null ? theme.backgroundSprite : GetOrCreateQuadSprite();
        renderer.color = theme.backgroundColor;
        renderer.sortingOrder = backgroundSortingOrder;

        Vector3 worldMin = gridManager.CellToWorld(new Vector2Int(0, 0));
        Vector3 worldMax = gridManager.CellToWorld(new Vector2Int(width - 1, height - 1));
        Vector3 center = (worldMin + worldMax) * 0.5f;
        spawnedBackground.transform.position = new Vector3(center.x, center.y, 1f); // slightly behind

        float worldWidth = (worldMax.x - worldMin.x) + gridManager.CellSize + backgroundPadding * 2f;
        float worldHeight = (worldMax.y - worldMin.y) + gridManager.CellSize + backgroundPadding * 2f;

        if (theme.backgroundSprite != null)
        {
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(worldWidth, worldHeight);
        }
        else
        {
            spawnedBackground.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
        }
    }

    private static Sprite cachedQuadSprite;
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
