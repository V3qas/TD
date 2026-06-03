using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LevelMapEditorWindow : EditorWindow
{
    private enum PaintTool
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

    private static readonly string[] ToolLabels =
    {
        "Pfad",
        "Loeschen",
        "Stein",
        "Zerst.-Block",
        "Erhöhung",
        "Wasser",
        "Lava",
        "Start",
        "Stop"
    };

    private const int DefaultDestructibleHp = 100;
    private const int DefaultDestructibleReward = 15;

    private const float MinCellSize = 6f;
    private const float MaxCellSize = 32f;

    private LevelData targetLevel;
    private LevelMapDefinition mapDefinition;
    private Dictionary<Vector2Int, OccupantEntry> occupants = new Dictionary<Vector2Int, OccupantEntry>();
    private Dictionary<Vector2Int, GroundType> groundOverrides = new Dictionary<Vector2Int, GroundType>();
    private HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
    private Vector2 scrollPosition;
    private PaintTool selectedTool = PaintTool.Path;
    private string seedInput = string.Empty;
    private string validationMessage = string.Empty;
    private bool isValid;
    private bool validationDirty = true;
    private int newWidth = 44;
    private int newHeight = 32;
    private int destructibleHp = DefaultDestructibleHp;
    private int destructibleReward = DefaultDestructibleReward;
    private float cellSize = 18f;

    [MenuItem("Tools/Tower Defense/Map Editor")]
    public static void OpenWindow()
    {
        LevelMapEditorWindow window = GetWindow<LevelMapEditorWindow>("TD Map Editor");
        window.minSize = new Vector2(560f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        if (mapDefinition == null)
            CreateNewMap(newWidth, newHeight);
    }

    private void OnGUI()
    {
        DrawLevelControls();
        EditorGUILayout.Space(6f);
        DrawMapControls();
        EditorGUILayout.Space(6f);
        DrawSeedControls();
        EditorGUILayout.Space(6f);
        DrawValidation();
        EditorGUILayout.Space(6f);
        DrawGrid();
    }

    private void DrawLevelControls()
    {
        EditorGUILayout.LabelField("Level Asset", EditorStyles.boldLabel);
        targetLevel = (LevelData)EditorGUILayout.ObjectField("LevelData", targetLevel, typeof(LevelData), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(targetLevel == null))
            {
                if (GUILayout.Button("Aus Level laden"))
                    LoadFromLevel();

                ValidateIfNeeded();
                using (new EditorGUI.DisabledScope(!isValid))
                {
                    if (GUILayout.Button("In Level speichern"))
                        SaveToLevel();
                }
            }
        }
    }

    private void DrawMapControls()
    {
        EditorGUILayout.LabelField("Map", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            newWidth = Mathf.Clamp(EditorGUILayout.IntField("Breite", newWidth), 1, LevelMapDefinition.MaxSize);
            newHeight = Mathf.Clamp(EditorGUILayout.IntField("Hoehe", newHeight), 1, LevelMapDefinition.MaxSize);

            if (GUILayout.Button("Neue Map", GUILayout.Width(120f)))
                CreateNewMap(newWidth, newHeight);
        }

        selectedTool = (PaintTool)GUILayout.Toolbar((int)selectedTool, ToolLabels);
        cellSize = EditorGUILayout.Slider("Zoom", cellSize, MinCellSize, MaxCellSize);

        using (new EditorGUILayout.HorizontalScope())
        {
            destructibleHp = Mathf.Max(1, EditorGUILayout.IntField("Zerst. HP", destructibleHp));
            destructibleReward = Mathf.Max(0, EditorGUILayout.IntField("Belohnung", destructibleReward));
        }
    }

    private void DrawSeedControls()
    {
        EditorGUILayout.LabelField("Seed / JSON", EditorStyles.boldLabel);
        seedInput = EditorGUILayout.TextArea(seedInput, GUILayout.MinHeight(48f));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Seed laden"))
                LoadSeed();

            ValidateIfNeeded();
            using (new EditorGUI.DisabledScope(!isValid))
            {
                if (GUILayout.Button("Seed generieren"))
                    seedInput = LevelMapSeedUtility.Encode(BuildDefinition());

                if (GUILayout.Button("JSON generieren"))
                    seedInput = LevelMapSeedUtility.ToJson(BuildDefinition(), true);

                if (GUILayout.Button("Kopieren", GUILayout.Width(90f)))
                    EditorGUIUtility.systemCopyBuffer = LevelMapSeedUtility.Encode(BuildDefinition());
            }
        }
    }

    private void DrawValidation()
    {
        ValidateIfNeeded();
        MessageType messageType = isValid ? MessageType.Info : MessageType.Error;
        EditorGUILayout.HelpBox(validationMessage, messageType);
    }

    private void DrawGrid()
    {
        if (mapDefinition == null)
            return;

        float gridWidth = mapDefinition.width * cellSize;
        float gridHeight = mapDefinition.height * cellSize;
        float viewportHeight = Mathf.Max(220f, position.height - 390f);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(viewportHeight));
        Rect gridRect = GUILayoutUtility.GetRect(gridWidth, gridHeight);

        HandleGridInput(gridRect);
        DrawVisibleCells(gridRect, viewportHeight);

        EditorGUILayout.EndScrollView();
    }

    private void DrawVisibleCells(Rect gridRect, float viewportHeight)
    {
        float visibleLeft = scrollPosition.x;
        float visibleRight = visibleLeft + position.width;
        float visibleTop = scrollPosition.y;
        float visibleBottom = visibleTop + viewportHeight;

        int minColumn = Mathf.Max(0, Mathf.FloorToInt(visibleLeft / cellSize));
        int maxColumn = Mathf.Min(mapDefinition.width - 1, Mathf.CeilToInt(visibleRight / cellSize));
        int minVisualRow = Mathf.Max(0, Mathf.FloorToInt(visibleTop / cellSize));
        int maxVisualRow = Mathf.Min(mapDefinition.height - 1, Mathf.CeilToInt(visibleBottom / cellSize));

        for (int visualRow = minVisualRow; visualRow <= maxVisualRow; visualRow++)
        {
            int mapRow = mapDefinition.height - 1 - visualRow;

            for (int column = minColumn; column <= maxColumn; column++)
            {
                Vector2Int cell = new Vector2Int(column, mapRow);
                Rect cellRect = new Rect(
                    gridRect.x + column * cellSize,
                    gridRect.y + visualRow * cellSize,
                    Mathf.Max(1f, cellSize - 1f),
                    Mathf.Max(1f, cellSize - 1f)
                );

                EditorGUI.DrawRect(cellRect, GetCellColor(cell));
            }
        }
    }

    private void HandleGridInput(Rect gridRect)
    {
        Event currentEvent = Event.current;
        if (currentEvent.button != 0)
            return;

        if (currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseDrag)
            return;

        if (!gridRect.Contains(currentEvent.mousePosition))
            return;

        Vector2Int cell = MouseToCell(currentEvent.mousePosition, gridRect);
        PaintCell(cell);
        currentEvent.Use();
    }

    private Vector2Int MouseToCell(Vector2 mousePosition, Rect gridRect)
    {
        int column = Mathf.FloorToInt((mousePosition.x - gridRect.x) / cellSize);
        int visualRow = Mathf.FloorToInt((mousePosition.y - gridRect.y) / cellSize);
        int mapRow = mapDefinition.height - 1 - visualRow;

        return new Vector2Int(
            Mathf.Clamp(column, 0, mapDefinition.width - 1),
            Mathf.Clamp(mapRow, 0, mapDefinition.height - 1)
        );
    }

    private void PaintCell(Vector2Int cell)
    {
        switch (selectedTool)
        {
            case PaintTool.Path:
                occupants.Remove(cell);
                groundOverrides.Remove(cell);
                pathCells.Add(cell);
                break;
            case PaintTool.Erase:
                occupants.Remove(cell);
                groundOverrides.Remove(cell);
                if (cell != mapDefinition.startCell && cell != mapDefinition.goalCell)
                    pathCells.Remove(cell);
                break;
            case PaintTool.Rock:
                if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell) return;
                pathCells.Remove(cell);
                groundOverrides.Remove(cell);
                occupants[cell] = new OccupantEntry { cell = cell, type = OccupantType.Rock, maxHp = 0, reward = 0 };
                break;
            case PaintTool.Destructible:
                if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell) return;
                pathCells.Remove(cell);
                groundOverrides.Remove(cell);
                occupants[cell] = new OccupantEntry { cell = cell, type = OccupantType.Destructible, maxHp = destructibleHp, reward = destructibleReward };
                break;
            case PaintTool.Elevated:
                if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell) return;
                pathCells.Remove(cell);
                occupants.Remove(cell);
                groundOverrides[cell] = GroundType.Elevated;
                break;
            case PaintTool.Water:
                if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell) return;
                pathCells.Remove(cell);
                occupants.Remove(cell);
                groundOverrides[cell] = GroundType.Water;
                break;
            case PaintTool.Lava:
                if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell) return;
                pathCells.Remove(cell);
                occupants.Remove(cell);
                groundOverrides[cell] = GroundType.Lava;
                break;
            case PaintTool.Start:
                occupants.Remove(cell);
                groundOverrides.Remove(cell);
                pathCells.Remove(mapDefinition.startCell);
                mapDefinition.startCell = cell;
                pathCells.Add(cell);
                break;
            case PaintTool.Goal:
                occupants.Remove(cell);
                groundOverrides.Remove(cell);
                pathCells.Remove(mapDefinition.goalCell);
                mapDefinition.goalCell = cell;
                pathCells.Add(cell);
                break;
        }

        MarkDirty();
    }

    private Color GetCellColor(Vector2Int cell)
    {
        if (cell == mapDefinition.startCell)
            return new Color(0.2f, 0.85f, 0.35f);

        if (cell == mapDefinition.goalCell)
            return new Color(0.95f, 0.25f, 0.2f);

        if (occupants.TryGetValue(cell, out OccupantEntry occupant))
        {
            return occupant.type == OccupantType.Rock
                ? new Color(0.32f, 0.32f, 0.34f)
                : new Color(0.55f, 0.42f, 0.28f);
        }

        if (groundOverrides.TryGetValue(cell, out GroundType ground))
        {
            switch (ground)
            {
                case GroundType.Elevated: return new Color(0.7f, 0.66f, 0.55f);
                case GroundType.Water:    return new Color(0.25f, 0.55f, 0.85f);
                case GroundType.Lava:     return new Color(0.95f, 0.32f, 0.12f);
            }
        }

        if (pathCells.Contains(cell))
            return new Color(1f, 0.78f, 0.2f);

        return new Color(0.88f, 0.9f, 0.92f);
    }

    private void CreateNewMap(int width, int height)
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

        for (int column = 0; column < clampedWidth; column++)
            pathCells.Add(new Vector2Int(column, pathRow));

        newWidth = clampedWidth;
        newHeight = clampedHeight;
        seedInput = string.Empty;
        scrollPosition = Vector2.zero;
        MarkDirty();
    }

    private void LoadFromLevel()
    {
        if (targetLevel == null)
            return;

        LoadDefinition(targetLevel.GetMapDefinition());
        seedInput = targetLevel.mapSeed;

        if (string.IsNullOrWhiteSpace(seedInput))
            seedInput = LevelMapSeedUtility.Encode(BuildDefinition());
    }

    private void SaveToLevel()
    {
        if (targetLevel == null)
            return;

        ValidateIfNeeded();
        if (!isValid)
        {
            EditorUtility.DisplayDialog("Map ungueltig", validationMessage, "OK");
            return;
        }

        LevelMapDefinition definition = BuildDefinition();
        targetLevel.ApplyDefinition(definition);
        seedInput = targetLevel.mapSeed;

        EditorUtility.SetDirty(targetLevel);
        AssetDatabase.SaveAssets();
    }

    private void LoadSeed()
    {
        if (!LevelMapSeedUtility.TryDecode(seedInput, out LevelMapDefinition definition, out string error))
        {
            EditorUtility.DisplayDialog("Seed ungueltig", error, "OK");
            return;
        }

        LoadDefinition(definition);
    }

    private void LoadDefinition(LevelMapDefinition definition)
    {
        mapDefinition = definition.CloneNormalized();
        occupants = new Dictionary<Vector2Int, OccupantEntry>();
        groundOverrides = new Dictionary<Vector2Int, GroundType>();

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
                occupants[mapDefinition.occupants[i].cell] = mapDefinition.occupants[i];
        }

        if (mapDefinition.groundOverrides != null)
        {
            for (int i = 0; i < mapDefinition.groundOverrides.Count; i++)
                groundOverrides[mapDefinition.groundOverrides[i].cell] = mapDefinition.groundOverrides[i].type;
        }

        pathCells = new HashSet<Vector2Int>(mapDefinition.pathCells)
        {
            mapDefinition.startCell,
            mapDefinition.goalCell
        };

        newWidth = mapDefinition.width;
        newHeight = mapDefinition.height;
        scrollPosition = Vector2.zero;
        MarkDirty();
    }

    private LevelMapDefinition BuildDefinition()
    {
        List<OccupantEntry> occupantList = new List<OccupantEntry>(occupants.Count);
        foreach (KeyValuePair<Vector2Int, OccupantEntry> pair in occupants)
            occupantList.Add(pair.Value);

        List<GroundOverrideEntry> groundList = new List<GroundOverrideEntry>(groundOverrides.Count);
        foreach (KeyValuePair<Vector2Int, GroundType> pair in groundOverrides)
            groundList.Add(new GroundOverrideEntry { cell = pair.Key, type = pair.Value });

        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = mapDefinition.width,
            height = mapDefinition.height,
            startCell = mapDefinition.startCell,
            goalCell = mapDefinition.goalCell,
            blockedCells = new List<Vector2Int>(),
            pathCells = new List<Vector2Int>(pathCells),
            occupants = occupantList,
            groundOverrides = groundList
        };

        definition.Normalize();
        return definition;
    }

    private void ValidateIfNeeded()
    {
        if (!validationDirty)
            return;

        isValid = LevelMapValidator.Validate(BuildDefinition(), true, out validationMessage);
        validationDirty = false;
    }

    private void MarkDirty()
    {
        validationDirty = true;
        Repaint();
    }
}