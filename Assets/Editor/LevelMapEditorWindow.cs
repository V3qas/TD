using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LevelMapEditorWindow : EditorWindow
{
    private enum PaintTool
    {
        Path,
        Blocked,
        Erase,
        Start,
        Goal
    }

    private static readonly string[] ToolLabels =
    {
        "Pfad",
        "Blocker",
        "Loeschen",
        "Start",
        "Stop"
    };

    private const float MinCellSize = 6f;
    private const float MaxCellSize = 32f;

    private LevelData targetLevel;
    private LevelMapDefinition mapDefinition;
    private HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
    private Vector2 scrollPosition;
    private PaintTool selectedTool = PaintTool.Path;
    private string seedInput = string.Empty;
    private string validationMessage = string.Empty;
    private bool isValid;
    private bool validationDirty = true;
    private int newWidth = 44;
    private int newHeight = 32;
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
                blockedCells.Remove(cell);
                pathCells.Add(cell);
                break;
            case PaintTool.Blocked:
                if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                    return;

                pathCells.Remove(cell);
                blockedCells.Add(cell);
                break;
            case PaintTool.Erase:
                blockedCells.Remove(cell);
                if (cell != mapDefinition.startCell && cell != mapDefinition.goalCell)
                    pathCells.Remove(cell);
                break;
            case PaintTool.Start:
                blockedCells.Remove(cell);
                pathCells.Remove(mapDefinition.startCell);
                mapDefinition.startCell = cell;
                pathCells.Add(cell);
                break;
            case PaintTool.Goal:
                blockedCells.Remove(cell);
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

        if (blockedCells.Contains(cell))
            return new Color(0.28f, 0.3f, 0.33f);

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
            pathCells = new List<Vector2Int>()
        };

        blockedCells.Clear();
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
        blockedCells = new HashSet<Vector2Int>(mapDefinition.blockedCells);
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
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = mapDefinition.width,
            height = mapDefinition.height,
            startCell = mapDefinition.startCell,
            goalCell = mapDefinition.goalCell,
            blockedCells = new List<Vector2Int>(blockedCells),
            pathCells = new List<Vector2Int>(pathCells)
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