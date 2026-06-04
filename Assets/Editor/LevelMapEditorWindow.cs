using UnityEditor;
using UnityEngine;
using TD.Level;

namespace TD.Editor
{
    public class LevelMapEditorWindow : EditorWindow
    {
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
        private readonly LevelMapAuthoringState mapState = new LevelMapAuthoringState();
        private LevelMapDefinition mapDefinition => mapState.MapDefinition;
        private Vector2 scrollPosition;
        private LevelMapPaintTool selectedTool = LevelMapPaintTool.Path;
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

            selectedTool = (LevelMapPaintTool)GUILayout.Toolbar((int)selectedTool, ToolLabels);
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

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Zufallsgenerator", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pfad generieren"))
                    GenerateRandomPath();
                if (GUILayout.Button("Blöcke streuen"))
                    ScatterRandomBlocks();
                if (GUILayout.Button("Komplette Map"))
                    GenerateFullRandomMap();
            }
        }

        private int ResolveIntegerSeed()
        {
            if (int.TryParse(seedInput, out int parsed) && parsed != 0)
                return parsed;
            return UnityEngine.Random.Range(1, int.MaxValue);
        }

        private void GenerateRandomPath()
        {
            if (mapDefinition == null) return;
            if (!mapState.GenerateRandomPath(ResolveIntegerSeed()))
            {
                EditorUtility.DisplayDialog("Pfadgenerator", "Kein Pfad gefunden. Anderen Seed versuchen.", "OK");
                return;
            }

            MarkDirty();
        }

        private void ScatterRandomBlocks()
        {
            if (mapDefinition == null) return;
            MapGenerator.ScatterParams parameters = MapGenerator.ScatterParams.Default;
            parameters.destructibleHp = destructibleHp;
            parameters.destructibleReward = destructibleReward;
            if (mapState.ScatterRandomBlocks(ResolveIntegerSeed(), parameters))
                MarkDirty();
        }

        private void GenerateFullRandomMap()
        {
            MapGenerator.ScatterParams parameters = MapGenerator.ScatterParams.Default;
            parameters.destructibleHp = destructibleHp;
            parameters.destructibleReward = destructibleReward;
            LevelMapDefinition generated = MapGenerator.GenerateFullMap(newWidth, newHeight, ResolveIntegerSeed(), parameters);
            LoadDefinition(generated);
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

        private bool PaintCell(Vector2Int cell)
        {
            bool changed = mapState.PaintCell(cell, selectedTool, destructibleHp, destructibleReward);
            if (changed)
                MarkDirty();

            return changed;
        }

        private Color GetCellColor(Vector2Int cell)
        {
            if (cell == mapDefinition.startCell)
                return new Color(0.2f, 0.85f, 0.35f);

            if (cell == mapDefinition.goalCell)
                return new Color(0.95f, 0.25f, 0.2f);

            if (mapState.TryGetOccupant(cell, out OccupantEntry occupant))
            {
                return occupant.type == OccupantType.Rock
                    ? new Color(0.32f, 0.32f, 0.34f)
                    : new Color(0.55f, 0.42f, 0.28f);
            }

            if (mapState.TryGetGroundOverride(cell, out GroundType ground))
            {
                switch (ground)
                {
                    case GroundType.Elevated: return new Color(0.7f, 0.66f, 0.55f);
                    case GroundType.Water:    return new Color(0.25f, 0.55f, 0.85f);
                    case GroundType.Lava:     return new Color(0.95f, 0.32f, 0.12f);
                }
            }

            if (mapState.IsPathCell(cell))
                return new Color(1f, 0.78f, 0.2f);

            return new Color(0.88f, 0.9f, 0.92f);
        }

        private void CreateNewMap(int width, int height)
        {
            int clampedWidth = Mathf.Clamp(width, 1, LevelMapDefinition.MaxSize);
            int clampedHeight = Mathf.Clamp(height, 1, LevelMapDefinition.MaxSize);
            mapState.CreateNewMap(clampedWidth, clampedHeight, true);

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
            mapState.LoadDefinition(definition);
            if (mapDefinition == null)
            {
                MarkDirty();
                return;
            }

            newWidth = mapDefinition.width;
            newHeight = mapDefinition.height;
            scrollPosition = Vector2.zero;
            MarkDirty();
        }

        private LevelMapDefinition BuildDefinition()
        {
            return mapState.BuildDefinition();
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
}
