using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TD.Combat;
using TD.Core;
using TD.Enemies;
using TD.Grid;
using TD.Level;
using TD.Menu;
using TD.Theming;

namespace TD.UI
{
    #if UNITY_EDITOR
    using UnityEditor;
    #endif

    public class RuntimeMapEditorController : MonoBehaviour
    {
        private const float EditorNavWidth = 300f;
        private const float CameraPaddingCells = 0.25f;
        private const float CameraOverzoom = 1.12f;

        private enum MapEditorTool
        {
            Path,
            Erase,
            Rock,
            Destructible,
            Elevated,
            Water,
            Lava,
            Start,
            Goal
        }

        private const int DefaultDestructibleHp = 100;
        private const int DefaultDestructibleReward = 15;

        [SerializeField] private LevelLoader levelLoader;
        [SerializeField] private MainMenuConfig menuConfig;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private BuildManager buildManager;
        [SerializeField] private InGameHudController hudController;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private MapCameraController mapCameraController;
        [SerializeField] private Font font;
        [SerializeField] private string menuSceneName = "Menu";
        private const int defaultWidth = 44;
        private const int defaultHeight = 32;

        private readonly Dictionary<MapEditorTool, Button> toolButtons = new Dictionary<MapEditorTool, Button>();
        private readonly Dictionary<DifficultyLevel, Button> difficultyButtons = new Dictionary<DifficultyLevel, Button>();
        private readonly Dictionary<Vector2Int, OccupantEntry> occupants = new Dictionary<Vector2Int, OccupantEntry>();
        private readonly Dictionary<Vector2Int, GroundType> groundOverrides = new Dictionary<Vector2Int, GroundType>();
        private readonly HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
        // Snapshot of authored multi-path sequences. See LevelMapEditorWindow for rationale:
        // we keep them so a v3 multi-path map survives load->save without being collapsed.
        private List<PathSequence> loadedPathSequences = new List<PathSequence>();

        private GameObject editorRoot;
        private InputField widthInput;
        private InputField heightInput;
        private InputField seedInput;
        private InputField destructibleHpInput;
        private InputField destructibleRewardInput;
        private Text validationText;
        private Button startTestButton;
        private Button saveCustomButton;
        private Button saveCampaignButton;
        private LevelMapDefinition mapDefinition;
        private LevelMapDefinition pendingTestDefinition;
        private MapEditorTool selectedTool = MapEditorTool.Path;
        private DifficultyLevel selectedDifficulty = DifficultyLevel.Normal;
        private Vector2Int lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
        private LevelMapDefinition activeCameraFrameDefinition;
        private float activeCameraReservedRightUiWidth = EditorNavWidth;
        private int lastFramedScreenWidth = -1;
        private int lastFramedScreenHeight = -1;
        private float lastFramedCanvasScale = -1f;
        private bool isOpen;
        private bool isValid;

        private Font RuntimeFont
        {
            get
            {
                if (font != null)
                    return font;

                Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return legacyFont != null ? legacyFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        private void Start()
        {
            if (GameSession.IsMapEditorSession)
                Open();
        }

        private void Update()
        {
            RefreshCameraFrameForScreenSize();

            if (!isOpen || Mouse.current == null)
                return;

            if (!Mouse.current.leftButton.isPressed)
            {
                lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
                return;
            }

            if (IsPointerOverUi())
                return;

            if (!TryGetPointerCell(out Vector2Int cell) || cell == lastPaintedCell)
                return;

            if (PaintCell(cell))
            {
                lastPaintedCell = cell;
                RebuildPreview();
                RefreshValidation();
            }
        }

        public void Open()
        {
            ResolveReferences();
            EnsureCanvas();
            EnsureEventSystem();
            EnsureEditorUi();

            enemySpawner?.StopSpawning(true);
            buildManager?.ClearSelectedTowerToBuild();
            hudController?.Hide();

            if (mapDefinition == null)
                LoadInitialMap();

            editorRoot.SetActive(true);
            isOpen = true;
            RebuildPreview();
            FrameCameraOnMap(mapDefinition, EditorNavWidth);
            RefreshToolButtons();
            RefreshDifficultyButtons();
            RefreshValidation();
        }

        public void CloseToMenu()
        {
            GameSession.EndMapEditorMode();
            CloseEditor();
            SceneManager.LoadScene(menuSceneName);
        }

        private void CloseEditor()
        {
            isOpen = false;
            buildManager?.ClearSelectedTowerToBuild();
            gridManager?.ClearPreviewVisuals();

            if (editorRoot != null)
                editorRoot.SetActive(false);
        }

        private void StartTestRun()
        {
            if (gridManager == null)
            {
                SetValidation(false, "GridManager is missing from the scene.");
                return;
            }

            LevelMapDefinition definition = BuildDefinition();
            if (!LevelMapValidator.Validate(definition, true, out string validationMessage))
            {
                SetValidation(false, validationMessage);
                return;
            }

            pendingTestDefinition = definition;
            GameSession.BeginTestRun(selectedDifficulty);

            if (hudController != null)
            {
                hudController.OnBackToEditorRequested -= ReturnFromTest;
                hudController.OnBackToEditorRequested += ReturnFromTest;
            }

            CloseEditor();
            if (levelLoader != null)
            {
                // Routes through LevelLoader so OccupantSpawner / GroundOverlaySpawner
                // / MapThemeApplier all receive OnMapLoaded and can spawn their objects.
                levelLoader.LoadMap(definition);
            }
            else
            {
                gridManager.BuildGridPreview(definition);
                FrameCameraOnMap(definition, InGameHudController.PanelWidth);
            }
            hudController?.Show();
            enemySpawner?.RestartSpawningFromRound(1);
        }

        private void ReturnFromTest()
        {
            if (hudController != null)
                hudController.OnBackToEditorRequested -= ReturnFromTest;

            GameSession.EndTestRun();
            enemySpawner?.StopSpawning(true);
            buildManager?.ClearAllPlacedTowers();

            if (pendingTestDefinition != null)
                LoadDefinition(pendingTestDefinition);

            Open();
        }

        private void ResolveReferences()
        {
            if (gridManager == null)
                gridManager = FindAnyObjectByType<GridManager>();

            if (levelLoader == null)
                levelLoader = FindAnyObjectByType<LevelLoader>();

            if (enemySpawner == null)
                enemySpawner = FindAnyObjectByType<EnemySpawner>();

            if (buildManager == null)
                buildManager = FindAnyObjectByType<BuildManager>();

            if (hudController == null)
                hudController = FindAnyObjectByType<InGameHudController>();

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mapCameraController == null && mainCamera != null)
                mapCameraController = mainCamera.GetComponent<MapCameraController>();

            if (mapCameraController == null && mainCamera != null)
                mapCameraController = mainCamera.gameObject.AddComponent<MapCameraController>();
        }

        private void EnsureCanvas()
        {
            if (targetCanvas != null)
                return;

            targetCanvas = FindAnyObjectByType<Canvas>();
            if (targetCanvas != null)
                return;

            GameObject canvasObject = new GameObject("MapEditorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        private void EnsureEditorUi()
        {
            if (editorRoot != null)
                return;

            editorRoot = new GameObject("RuntimeMapEditor", typeof(RectTransform), typeof(Image));
            editorRoot.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = editorRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image overlay = editorRoot.GetComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.12f);
            overlay.raycastTarget = false;

            GameObject nav = new GameObject("MapEditorNav", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            nav.transform.SetParent(editorRoot.transform, false);

            RectTransform navRect = nav.GetComponent<RectTransform>();
            navRect.anchorMin = new Vector2(1f, 0f);
            navRect.anchorMax = new Vector2(1f, 1f);
            navRect.pivot = new Vector2(1f, 0.5f);
            navRect.sizeDelta = new Vector2(EditorNavWidth, 0f);
            navRect.anchoredPosition = Vector2.zero;

            Image navBackground = nav.GetComponent<Image>();
            navBackground.color = new Color(0.07f, 0.08f, 0.1f, 1f);

            VerticalLayoutGroup navLayout = nav.GetComponent<VerticalLayoutGroup>();
            navLayout.padding = new RectOffset(18, 18, 18, 18);
            navLayout.spacing = 8f;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = false;

            CreateText("Title", nav.transform, "Map Editor", 24, TextAnchor.MiddleLeft, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            BuildSizeControls(nav.transform);
            CreateDivider(nav.transform);
            BuildToolControls(nav.transform);
            CreateDivider(nav.transform);
            BuildSeedControls(nav.transform);
            CreateDivider(nav.transform);
            BuildDifficultyControls(nav.transform);
            CreateDivider(nav.transform);
            BuildActionControls(nav.transform);

            editorRoot.SetActive(false);
        }

        private void BuildSizeControls(Transform parent)
        {
            CreateText("SizeTitle", parent, "Size", 18, TextAnchor.MiddleLeft, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            widthInput = CreateInputField(parent, defaultWidth.ToString(), 16, 34f, false);
            heightInput = CreateInputField(parent, defaultHeight.ToString(), 16, 34f, false);
            CreateButton(parent, "New Map", ApplyNewMapSize, true);
        }

        private void BuildToolControls(Transform parent)
        {
            CreateText("ToolsTitle", parent, "Tools", 18, TextAnchor.MiddleLeft, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            CreateToolButton(parent, MapEditorTool.Path, "Path");
            CreateToolButton(parent, MapEditorTool.Erase, "Erase");
            CreateToolButton(parent, MapEditorTool.Rock, "Rock");
            CreateToolButton(parent, MapEditorTool.Destructible, "Destructible");
            CreateToolButton(parent, MapEditorTool.Elevated, "Elevated");
            CreateToolButton(parent, MapEditorTool.Water, "Wasser");
            CreateToolButton(parent, MapEditorTool.Lava, "Lava");
            CreateToolButton(parent, MapEditorTool.Start, "Start");
            CreateToolButton(parent, MapEditorTool.Goal, "Stop");

            CreateText("DestructibleTitle", parent, "Destructible: HP / Reward", 14, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f))
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            destructibleHpInput = CreateInputField(parent, DefaultDestructibleHp.ToString(), 14, 30f, false);
            destructibleRewardInput = CreateInputField(parent, DefaultDestructibleReward.ToString(), 14, 30f, false);
        }

        private void BuildSeedControls(Transform parent)
        {
            CreateText("SeedTitle", parent, "Seed", 18, TextAnchor.MiddleLeft, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            seedInput = CreateInputField(parent, string.Empty, 13, 88f, true);
            CreateButton(parent, "Load Seed", LoadSeedFromInput, true);
            CreateButton(parent, "Copy Seed", CopySeedToClipboard, true);

            CreateText("GenTitle", parent, "Generate", 14, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f))
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            CreateButton(parent, "Generate Path", GenerateRandomPath, true);
            CreateButton(parent, "Scatter Blocks", ScatterRandomBlocks, true);
            CreateButton(parent, "Full Map", GenerateFullRandomMap, true);
        }

        private int ResolveSeed()
        {
            if (seedInput != null && int.TryParse(seedInput.text, out int parsed) && parsed != 0)
                return parsed;
            return UnityEngine.Random.Range(1, int.MaxValue);
        }

        private void GenerateRandomPath()
        {
            if (mapDefinition == null) return;
            int seed = ResolveSeed();
            List<Vector2Int> path = MapGenerator.GeneratePath(
                mapDefinition.width, mapDefinition.height,
                mapDefinition.startCell, mapDefinition.goalCell, seed);

            if (path == null || path.Count == 0)
            {
                SetValidation(false, "Path generator found no route. Try another seed.");
                return;
            }

            pathCells.Clear();
            foreach (Vector2Int cell in path)
                pathCells.Add(cell);
            pathCells.Add(mapDefinition.startCell);
            pathCells.Add(mapDefinition.goalCell);

            // remove now-overlapping occupants/grounds
            foreach (Vector2Int cell in path)
            {
                occupants.Remove(cell);
                groundOverrides.Remove(cell);
            }

            RefreshSeedText();
            RebuildPreview();
            RefreshValidation();
        }

        private void ScatterRandomBlocks()
        {
            if (mapDefinition == null) return;
            LevelMapDefinition definition = BuildDefinition();
            int seed = ResolveSeed();
            MapGenerator.ScatterParams parameters = MapGenerator.ScatterParams.Default;
            parameters.destructibleHp = ParsePositiveInput(destructibleHpInput, DefaultDestructibleHp);
            parameters.destructibleReward = ParseNonNegativeInput(destructibleRewardInput, DefaultDestructibleReward);
            MapGenerator.ScatterBlocks(definition, seed, parameters);
            definition.Normalize();
            LoadDefinition(definition);
            RebuildPreview();
            RefreshValidation();
        }

        private void GenerateFullRandomMap()
        {
            if (mapDefinition == null) return;
            int seed = ResolveSeed();
            MapGenerator.ScatterParams parameters = MapGenerator.ScatterParams.Default;
            parameters.destructibleHp = ParsePositiveInput(destructibleHpInput, DefaultDestructibleHp);
            parameters.destructibleReward = ParseNonNegativeInput(destructibleRewardInput, DefaultDestructibleReward);
            LevelMapDefinition generated = MapGenerator.GenerateFullMap(mapDefinition.width, mapDefinition.height, seed, parameters);
            LoadDefinition(generated);
            RebuildPreview();
            FrameCameraOnMap(mapDefinition, EditorNavWidth);
            RefreshValidation();
        }

        private void BuildActionControls(Transform parent)
        {
            validationText = CreateText("Validation", parent, string.Empty, 15, TextAnchor.UpperLeft, new Color(0.82f, 0.84f, 0.88f));
            validationText.gameObject.AddComponent<LayoutElement>().preferredHeight = 78f;

            saveCustomButton = CreateButton(parent, "Save Custom Map", SaveCustomMap, false);
            saveCampaignButton = CreateButton(parent, "Save to Campaign", SaveCampaignMap, false);
            startTestButton = CreateButton(parent, "Test Round 1", StartTestRun, false);
            CreateButton(parent, "Back", CloseToMenu, true);
        }

        private void BuildDifficultyControls(Transform parent)
        {
            CreateText("DiffTitle", parent, "Difficulty", 18, TextAnchor.MiddleLeft, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            CreateDifficultyButton(parent, DifficultyLevel.Easy,      "Easy");
            CreateDifficultyButton(parent, DifficultyLevel.Normal,    "Normal");
            CreateDifficultyButton(parent, DifficultyLevel.Hard,      "Hard");
            CreateDifficultyButton(parent, DifficultyLevel.Nightmare, "Nightmare");
        }

        private void CreateDifficultyButton(Transform parent, DifficultyLevel level, string label)
        {
            Button button = CreateButton(parent, label, () => SetDifficulty(level), true);
            button.gameObject.GetComponent<LayoutElement>().preferredHeight = 36f;
            difficultyButtons[level] = button;
        }

        private void SetDifficulty(DifficultyLevel level)
        {
            selectedDifficulty = level;
            RefreshDifficultyButtons();
        }

        private void RefreshDifficultyButtons()
        {
            foreach (KeyValuePair<DifficultyLevel, Button> pair in difficultyButtons)
            {
                Image image = pair.Value.GetComponent<Image>();
                if (image != null)
                    image.color = pair.Key == selectedDifficulty
                        ? new Color(0.3f, 0.6f, 0.35f, 1f)
                        : new Color(0.18f, 0.32f, 0.52f, 1f);
            }
        }

        private void CreateToolButton(Transform parent, MapEditorTool tool, string label)
        {
            Button button = CreateButton(parent, label, () => SelectTool(tool), true);
            toolButtons[tool] = button;
        }

        private void SelectTool(MapEditorTool tool)
        {
            selectedTool = tool;
            RefreshToolButtons();
        }

        private void LoadInitialMap()
        {
            CreateEmptyMap(defaultWidth, defaultHeight);
        }

        private void ApplyNewMapSize()
        {
            int width = ParseSizeInput(widthInput, defaultWidth);
            int height = ParseSizeInput(heightInput, defaultHeight);
            CreateEmptyMap(width, height);
            RebuildPreview();
            FrameCameraOnMap(mapDefinition, EditorNavWidth);
            RefreshValidation();
        }

        private void CreateEmptyMap(int width, int height)
        {
            int clampedWidth = Mathf.Clamp(width, 1, LevelMapDefinition.MaxSize);
            int clampedHeight = Mathf.Clamp(height, 1, LevelMapDefinition.MaxSize);
            int pathRow = clampedHeight / 2;

            mapDefinition = new LevelMapDefinition
            {
                width = clampedWidth,
                height = clampedHeight,
                startCell = new Vector2Int(0, pathRow),
                goalCell = new Vector2Int(clampedWidth - 1, pathRow),
                blockedCells = new List<Vector2Int>(),
                pathCells = new List<Vector2Int>(),
                groundOverrides = new List<GroundOverrideEntry>(),
                occupants = new List<OccupantEntry>()
            };

            occupants.Clear();
            groundOverrides.Clear();
            pathCells.Clear();

            RefreshInputsFromMap();
            RefreshSeedText();
        }

        private void LoadDefinition(LevelMapDefinition definition)
        {
            mapDefinition = definition.CloneNormalized();
            occupants.Clear();
            groundOverrides.Clear();
            pathCells.Clear();

            // Legacy: any blockedCells from old saves count as indestructible Rocks
            // so old maps still display correctly.
            if (mapDefinition.blockedCells != null)
            {
                for (int i = 0; i < mapDefinition.blockedCells.Count; i++)
                {
                    Vector2Int legacyBlocker = mapDefinition.blockedCells[i];
                    if (!occupants.ContainsKey(legacyBlocker))
                        occupants[legacyBlocker] = new OccupantEntry { cell = legacyBlocker, type = OccupantType.Rock, maxHp = 0, reward = 0 };
                }
            }

            if (mapDefinition.occupants != null)
            {
                for (int i = 0; i < mapDefinition.occupants.Count; i++)
                {
                    OccupantEntry entry = mapDefinition.occupants[i];
                    occupants[entry.cell] = entry;
                }
            }

            if (mapDefinition.groundOverrides != null)
            {
                for (int i = 0; i < mapDefinition.groundOverrides.Count; i++)
                {
                    GroundOverrideEntry entry = mapDefinition.groundOverrides[i];
                    groundOverrides[entry.cell] = entry.type;
                }
            }

            foreach (Vector2Int pathCell in mapDefinition.pathCells)
                pathCells.Add(pathCell);

            pathCells.Add(mapDefinition.startCell);
            pathCells.Add(mapDefinition.goalCell);

            loadedPathSequences = ClonePathSequences(mapDefinition.pathSequences);

            RefreshInputsFromMap();
            RefreshSeedText();
        }

        private void LoadSeedFromInput()
        {
            if (!LevelMapSeedUtility.TryDecode(seedInput.text, out LevelMapDefinition definition, out string error))
            {
                SetValidation(false, error);
                return;
            }

            LoadDefinition(definition);
            RebuildPreview();
            FrameCameraOnMap(mapDefinition, EditorNavWidth);
            RefreshValidation();
        }

        private void CopySeedToClipboard()
        {
            string seed = LevelMapSeedUtility.Encode(BuildDefinition());
            seedInput.text = seed;
            GUIUtility.systemCopyBuffer = seed;
        }

        private bool PaintCell(Vector2Int cell)
        {
            bool changed = false;

            switch (selectedTool)
            {
                case MapEditorTool.Path:
                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    changed |= pathCells.Add(cell);
                    break;
                case MapEditorTool.Erase:
                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    if (cell != mapDefinition.startCell && cell != mapDefinition.goalCell)
                        changed |= pathCells.Remove(cell);
                    break;
                case MapEditorTool.Rock:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    changed |= pathCells.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    changed |= SetOccupant(cell, OccupantType.Rock, 0, 0);
                    break;
                case MapEditorTool.Destructible:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    int hp = ParsePositiveInput(destructibleHpInput, DefaultDestructibleHp);
                    int reward = ParseNonNegativeInput(destructibleRewardInput, DefaultDestructibleReward);
                    changed |= pathCells.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    changed |= SetOccupant(cell, OccupantType.Destructible, hp, reward);
                    break;
                case MapEditorTool.Elevated:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    changed |= pathCells.Remove(cell);
                    changed |= occupants.Remove(cell);
                    changed |= SetGroundOverride(cell, GroundType.Elevated);
                    break;
                case MapEditorTool.Water:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    changed |= pathCells.Remove(cell);
                    changed |= occupants.Remove(cell);
                    changed |= SetGroundOverride(cell, GroundType.Water);
                    break;
                case MapEditorTool.Lava:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    changed |= pathCells.Remove(cell);
                    changed |= occupants.Remove(cell);
                    changed |= SetGroundOverride(cell, GroundType.Lava);
                    break;
                case MapEditorTool.Start:
                    if (cell == mapDefinition.goalCell)
                        return false;

                    changed |= pathCells.Remove(mapDefinition.startCell);
                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    mapDefinition.startCell = cell;
                    changed |= pathCells.Add(cell);
                    changed = true;
                    break;
                case MapEditorTool.Goal:
                    if (cell == mapDefinition.startCell)
                        return false;

                    changed |= pathCells.Remove(mapDefinition.goalCell);
                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    mapDefinition.goalCell = cell;
                    changed |= pathCells.Add(cell);
                    changed = true;
                    break;
            }

            if (changed)
                RefreshSeedText();

            return changed;
        }

        private bool SetOccupant(Vector2Int cell, OccupantType type, int maxHp, int reward)
        {
            if (occupants.TryGetValue(cell, out OccupantEntry existing) && existing.type == type && existing.maxHp == maxHp && existing.reward == reward)
                return false;
            occupants[cell] = new OccupantEntry { cell = cell, type = type, maxHp = maxHp, reward = reward };
            return true;
        }

        private bool SetGroundOverride(Vector2Int cell, GroundType type)
        {
            if (groundOverrides.TryGetValue(cell, out GroundType existing) && existing == type)
                return false;
            groundOverrides[cell] = type;
            return true;
        }

        private static int ParsePositiveInput(InputField input, int fallback)
        {
            if (input == null || !int.TryParse(input.text, out int value) || value <= 0)
                return fallback;
            return value;
        }

        private static int ParseNonNegativeInput(InputField input, int fallback)
        {
            if (input == null || !int.TryParse(input.text, out int value) || value < 0)
                return fallback;
            return value;
        }

        private bool TryGetPointerCell(out Vector2Int cell)
        {
            cell = default;

            if (mapDefinition == null || mainCamera == null)
                return false;

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
            worldPosition.z = 0f;

            cell = gridManager != null
                ? gridManager.WorldToCell(worldPosition)
                : new Vector2Int(Mathf.FloorToInt(worldPosition.x), Mathf.FloorToInt(worldPosition.y));

            return mapDefinition.IsInBounds(cell);
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void RebuildPreview()
        {
            if (gridManager == null || mapDefinition == null)
                return;

            gridManager.BuildGridPreview(BuildDefinition());
        }

        private LevelMapDefinition BuildDefinition()
        {
            List<OccupantEntry> occupantList = new List<OccupantEntry>(occupants.Count);
            foreach (KeyValuePair<Vector2Int, OccupantEntry> pair in occupants)
                occupantList.Add(pair.Value);

            List<GroundOverrideEntry> groundList = new List<GroundOverrideEntry>(groundOverrides.Count);
            foreach (KeyValuePair<Vector2Int, GroundType> pair in groundOverrides)
                groundList.Add(new GroundOverrideEntry { cell = pair.Key, type = pair.Value });

            // Reuse loaded sequences only if pathCells still matches their union; otherwise let
            // Normalize() reconstruct a single sequence. Prevents silent multi-path -> single-path
            // collapse during a roundtrip when the user didn't touch path cells.
            List<PathSequence> sequences = PathSequencesStillMatch(pathCells, loadedPathSequences)
                ? ClonePathSequences(loadedPathSequences)
                : new List<PathSequence>();

            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = mapDefinition.width,
                height = mapDefinition.height,
                startCell = mapDefinition.startCell,
                goalCell = mapDefinition.goalCell,
                blockedCells = new List<Vector2Int>(),
                pathCells = new List<Vector2Int>(pathCells),
                occupants = occupantList,
                groundOverrides = groundList,
                pathSequences = sequences
            };

            definition.Normalize();
            return definition;
        }

        private static bool PathSequencesStillMatch(HashSet<Vector2Int> currentPathCells, List<PathSequence> sequences)
        {
            if (sequences == null || sequences.Count == 0)
                return false;
            HashSet<Vector2Int> union = new HashSet<Vector2Int>();
            foreach (PathSequence seq in sequences)
            {
                if (seq?.cells == null) continue;
                foreach (Vector2Int cell in seq.cells)
                    union.Add(cell);
            }
            return union.SetEquals(currentPathCells);
        }

        private static List<PathSequence> ClonePathSequences(List<PathSequence> source)
        {
            List<PathSequence> copy = new List<PathSequence>();
            if (source == null) return copy;
            foreach (PathSequence seq in source)
            {
                if (seq?.cells == null) continue;
                copy.Add(new PathSequence(seq.cells));
            }
            return copy;
        }

        private void RefreshValidation()
        {
            bool valid = LevelMapValidator.Validate(BuildDefinition(), true, out string message);
            SetValidation(valid, message);
        }

        private void SetValidation(bool valid, string message)
        {
            isValid = valid;

            if (validationText != null)
            {
                validationText.text = message;
                validationText.color = valid ? new Color(0.58f, 0.9f, 0.62f) : new Color(1f, 0.46f, 0.42f);
            }

            if (startTestButton != null)
                startTestButton.interactable = isValid;

            if (saveCustomButton != null)
                saveCustomButton.interactable = isValid;

            if (saveCampaignButton != null)
                saveCampaignButton.interactable = isValid;
        }

        private void SaveCustomMap()
        {
            string seed = LevelMapSeedUtility.Encode(BuildDefinition());
            if (!CustomMapStorage.Save(seed, out CustomMapEntry savedEntry, out string error))
            {
                SetValidation(false, error);
                return;
            }

            seedInput.text = savedEntry.seed;
            SetValidation(true, $"Custom map saved: {savedEntry.label}");
        }

        private void SaveCampaignMap()
        {
            LevelMapDefinition definition = BuildDefinition();
            if (!LevelMapValidator.Validate(definition, true, out string validationMessage))
            {
                SetValidation(false, validationMessage);
                return;
            }

    #if UNITY_EDITOR
            if (menuConfig == null)
            {
                SetValidation(false, "MainMenuConfig is missing. Campaign seed could not be saved.");
                return;
            }

            Undo.RecordObject(menuConfig, "Add Campaign Map Seed");

            if (menuConfig.campaignLevels == null)
                menuConfig.campaignLevels = new List<CampaignLevelConfig>();

            string seed = LevelMapSeedUtility.Encode(definition);
            CampaignLevelConfig campaignLevel = new CampaignLevelConfig($"Seed Map {menuConfig.campaignLevels.Count + 1}")
            {
                mapSeed = seed,
                levelData = null,
                isUnlocked = true
            };

            menuConfig.campaignLevels.Add(campaignLevel);
            EditorUtility.SetDirty(menuConfig);
            AssetDatabase.SaveAssets();
            seedInput.text = seed;
            SetValidation(true, $"Campaign seed saved: {campaignLevel.label}");
    #else
            SetValidation(false, "Campaign saving is only available in the Unity Editor. Custom maps are saved locally.");
    #endif
        }

        private void RefreshInputsFromMap()
        {
            if (mapDefinition == null)
                return;

            if (widthInput != null)
                widthInput.text = mapDefinition.width.ToString();

            if (heightInput != null)
                heightInput.text = mapDefinition.height.ToString();
        }

        private void RefreshSeedText()
        {
            if (seedInput != null && mapDefinition != null)
                seedInput.text = LevelMapSeedUtility.Encode(BuildDefinition());
        }

        private void RefreshToolButtons()
        {
            foreach (KeyValuePair<MapEditorTool, Button> pair in toolButtons)
            {
                Image image = pair.Value.GetComponent<Image>();
                if (image != null)
                    image.color = pair.Key == selectedTool ? new Color(0.95f, 0.64f, 0.2f, 1f) : new Color(0.18f, 0.32f, 0.52f, 1f);
            }
        }

        private void FrameCameraOnMap(LevelMapDefinition definition, float reservedRightUiWidth)
        {
            if (definition == null)
                return;

            activeCameraFrameDefinition = definition.CloneNormalized();
            activeCameraReservedRightUiWidth = reservedRightUiWidth;
            ApplyCameraFrame(activeCameraFrameDefinition, activeCameraReservedRightUiWidth);
        }

        private void RefreshCameraFrameForScreenSize()
        {
            if (activeCameraFrameDefinition == null)
                return;

            float canvasScale = targetCanvas != null ? targetCanvas.scaleFactor : 1f;
            if (Screen.width == lastFramedScreenWidth
                && Screen.height == lastFramedScreenHeight
                && Mathf.Approximately(canvasScale, lastFramedCanvasScale))
            {
                return;
            }

            ApplyCameraFrame(activeCameraFrameDefinition, activeCameraReservedRightUiWidth);
        }

        private void ApplyCameraFrame(LevelMapDefinition definition, float reservedRightUiWidth)
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            float cellSize = gridManager != null ? gridManager.CellSize : 1f;
            MapCameraFrame frame = MapCameraFramer.Frame(mainCamera, definition, cellSize, reservedRightUiWidth, targetCanvas, CameraPaddingCells, CameraOverzoom);
            mapCameraController?.Configure(mainCamera, frame);

            lastFramedScreenWidth = Screen.width;
            lastFramedScreenHeight = Screen.height;
            lastFramedCanvasScale = targetCanvas != null ? targetCanvas.scaleFactor : 1f;
        }

        private int ParseSizeInput(InputField inputField, int fallback)
        {
            if (inputField == null || !int.TryParse(inputField.text, out int value))
                return fallback;

            return Mathf.Clamp(value, 1, LevelMapDefinition.MaxSize);
        }

        private Text CreateText(string objectName, Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text textComponent = textObject.GetComponent<Text>();
            textComponent.text = text;
            textComponent.font = RuntimeFont;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = color;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;

            return textComponent;
        }

        private Button CreateButton(Transform parent, string label, UnityAction onClick, bool interactable)
        {
            GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 42f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = interactable ? new Color(0.18f, 0.32f, 0.52f, 1f) : new Color(0.18f, 0.18f, 0.2f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.24f, 0.44f, 0.68f, 1f);
            colors.pressedColor = new Color(0.12f, 0.24f, 0.38f, 1f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            button.colors = colors;

            Text labelText = CreateText("Label", buttonObject.transform, label, 16, TextAnchor.MiddleCenter, Color.white);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        private InputField CreateInputField(Transform parent, string value, int fontSize, float height, bool multiline)
        {
            GameObject inputObject = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            inputObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = inputObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = height;

            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.17f, 1f);

            Text text = CreateText("Text", inputObject.transform, value, fontSize, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, Color.white);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            Text placeholder = CreateText("Placeholder", inputObject.transform, string.Empty, fontSize, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Color(0.62f, 0.65f, 0.7f));
            RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 4f);
            placeholderRect.offsetMax = new Vector2(-8f, -4f);

            InputField inputField = inputObject.GetComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.text = value;
            inputField.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            inputField.contentType = multiline ? InputField.ContentType.Standard : InputField.ContentType.IntegerNumber;

            return inputField;
        }

        private void CreateDivider(Transform parent)
        {
            GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.transform.SetParent(parent, false);
            divider.GetComponent<LayoutElement>().preferredHeight = 1f;
            divider.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);
        }
    }
}
