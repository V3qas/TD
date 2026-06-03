using System;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private LevelData levelData;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private MapCameraController mapCameraController;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private float rightHudWidth = InGameHudController.PanelWidth;
    [SerializeField] private float cameraPaddingCells = 0.25f;
    [SerializeField, Range(1f, 1.25f)] private float cameraOverzoom = 1.12f;

    private LevelMapDefinition loadedMapDefinition;
    private int lastFramedScreenWidth = -1;
    private int lastFramedScreenHeight = -1;
    private float lastFramedCanvasScale = -1f;

    public event Action<LevelData> OnLevelLoaded;
    public event Action<LevelMapDefinition> OnMapLoaded;

    public LevelData DefaultLevelData => levelData;
    public LevelMapDefinition LoadedMapDefinition => loadedMapDefinition != null ? loadedMapDefinition.CloneNormalized() : null;
    public bool HasLoadedLevel { get; private set; }

    private void Start()
    {
        EnsureRuntimeHelpers();
        if (!GameSession.IsMapEditorSession)
            LoadSelectedOrDefaultLevel();
    }

    /// <summary>
    /// Adds OccupantSpawner / GroundOverlaySpawner / MapThemeApplier to the
    /// LevelLoader GameObject at runtime if the scene author hasn't placed
    /// them yet. This way a fresh scene "just works" — the player can click
    /// destructibles and see Water/Lava/Elevated visuals out of the box.
    /// </summary>
    private void EnsureRuntimeHelpers()
    {
        if (FindAnyObjectByType<OccupantSpawner>() == null)
        {
            Debug.Log("[LevelLoader] No OccupantSpawner in scene — adding one to LevelLoader.");
            gameObject.AddComponent<OccupantSpawner>();
        }
        if (FindAnyObjectByType<GroundOverlaySpawner>() == null)
        {
            Debug.Log("[LevelLoader] No GroundOverlaySpawner in scene — adding one to LevelLoader.");
            gameObject.AddComponent<GroundOverlaySpawner>();
        }
        if (FindAnyObjectByType<MapThemeApplier>() == null)
        {
            Debug.Log("[LevelLoader] No MapThemeApplier in scene — adding one to LevelLoader.");
            gameObject.AddComponent<MapThemeApplier>();
        }
    }

    private void LateUpdate()
    {
        RefreshCameraFrameForScreenSize();
    }

    public void LoadSelectedOrDefaultLevel()
    {
        if (GameSession.SelectedMapDefinition != null)
        {
            LoadMap(GameSession.SelectedMapDefinition);
            return;
        }

        LevelData selectedLevel = GameSession.SelectedLevelData != null ? GameSession.SelectedLevelData : levelData;
        LoadLevel(selectedLevel);
    }

    public void LoadLevel(LevelData selectedLevel)
    {
        if (selectedLevel == null)
        {
            Debug.LogError("LevelLoader: No LevelData assigned.");
            return;
        }

        if (gridManager == null)
        {
            Debug.LogError("LevelLoader: No GridManager assigned.");
            return;
        }

        levelData = selectedLevel;
        if (!levelData.TryGetMapDefinition(out LevelMapDefinition definition, out string validationError))
        {
            Debug.LogError($"LevelLoader: LevelData is invalid ({validationError}).");
            return;
        }

        BuildGridForCurrentSession(definition);
        FrameCameraOnMap(definition);
        HasLoadedLevel = true;
        OnLevelLoaded?.Invoke(levelData);
    }

    public bool LoadMapSeed(string mapSeed)
    {
        if (!LevelMapSeedUtility.TryDecode(mapSeed, out LevelMapDefinition definition, out string error))
        {
            Debug.LogError($"LevelLoader: Map seed is invalid ({error}).");
            return false;
        }

        LoadMap(definition);
        return true;
    }

    public void LoadMap(LevelMapDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogError("LevelLoader: Map data is missing.");
            return;
        }

        if (gridManager == null)
        {
            Debug.LogError("LevelLoader: No GridManager assigned.");
            return;
        }

        levelData = null;
        LevelMapDefinition normalizedDefinition = definition.CloneNormalized();
        BuildGridForCurrentSession(normalizedDefinition);
        FrameCameraOnMap(normalizedDefinition);
        HasLoadedLevel = true;
        OnMapLoaded?.Invoke(normalizedDefinition);
    }

    private void BuildGridForCurrentSession(LevelMapDefinition definition)
    {
        // Editor test runs render the authored map directly via the preview visuals,
        // since gameplay does not yet spawn its own tile sprites. Campaign play keeps
        // the headless build to honour the no-debug-visuals-in-gameplay rule.
        if (GameSession.IsEditorTestRun)
            gridManager.BuildGridPreview(definition);
        else
            gridManager.BuildGrid(definition);
    }

    private void FrameCameraOnMap(LevelMapDefinition definition)
    {
        loadedMapDefinition = definition != null ? definition.CloneNormalized() : null;
        ApplyCameraFrame();
    }

    private void RefreshCameraFrameForScreenSize()
    {
        if (loadedMapDefinition == null)
            return;

        ResolveCameraReferences();

        float canvasScale = targetCanvas != null ? targetCanvas.scaleFactor : 1f;
        if (Screen.width == lastFramedScreenWidth
            && Screen.height == lastFramedScreenHeight
            && Mathf.Approximately(canvasScale, lastFramedCanvasScale))
        {
            return;
        }

        ApplyCameraFrame();
    }

    private void ApplyCameraFrame()
    {
        if (loadedMapDefinition == null)
            return;

        ResolveCameraReferences();

        float cellSize = gridManager != null ? gridManager.CellSize : 1f;
        MapCameraFrame frame = MapCameraFramer.Frame(mainCamera, loadedMapDefinition, cellSize, rightHudWidth, targetCanvas, cameraPaddingCells, cameraOverzoom);
        mapCameraController?.Configure(mainCamera, frame);

        lastFramedScreenWidth = Screen.width;
        lastFramedScreenHeight = Screen.height;
        lastFramedCanvasScale = targetCanvas != null ? targetCanvas.scaleFactor : 1f;
    }

    private void ResolveCameraReferences()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mapCameraController == null && mainCamera != null)
            mapCameraController = mainCamera.GetComponent<MapCameraController>();

        if (mapCameraController == null && mainCamera != null)
            mapCameraController = mainCamera.gameObject.AddComponent<MapCameraController>();

        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();
    }
}
