using TD.Level;

namespace TD.Core
{
    public static class GameSession
    {
        public static LevelData SelectedLevelData { get; private set; }
        public static LevelMapDefinition SelectedMapDefinition { get; private set; }
        public static string SelectedMapSeed { get; private set; }
        public static bool IsEditorTestRun { get; private set; }
        public static DifficultySettings SelectedDifficulty { get; private set; } = new DifficultySettings();

        public static void SelectLevel(LevelData levelData)
        {
            SelectedLevelData = levelData;
            SelectedMapDefinition = null;
            SelectedMapSeed = string.Empty;
            SelectedDifficulty = new DifficultySettings();
        }

        public static bool SelectMapSeed(string mapSeed, out string error)
        {
            if (!LevelMapSeedUtility.TryDecode(mapSeed, out LevelMapDefinition definition, out error))
                return false;

            SelectMapDefinition(definition, LevelMapSeedUtility.Encode(definition));
            return true;
        }

        public static void SelectMapDefinition(LevelMapDefinition definition, string mapSeed = null)
        {
            SelectedLevelData = null;
            SelectedMapDefinition = definition != null ? definition.CloneNormalized() : null;
            SelectedMapSeed = !string.IsNullOrWhiteSpace(mapSeed) && SelectedMapDefinition != null
                ? mapSeed
                : SelectedMapDefinition != null ? LevelMapSeedUtility.Encode(SelectedMapDefinition) : string.Empty;
            SelectedDifficulty = new DifficultySettings();
        }

        public static void ClearSelectedLevel()
        {
            SelectedLevelData = null;
            SelectedMapDefinition = null;
            SelectedMapSeed = string.Empty;
            SelectedDifficulty = new DifficultySettings();
        }

        public static void BeginTestRun(DifficultyLevel difficulty)
        {
            IsEditorTestRun = true;
            SelectedDifficulty = DifficultySettings.ForLevel(difficulty);
        }

        public static void EndTestRun()
        {
            IsEditorTestRun = false;
            SelectedDifficulty = new DifficultySettings();
        }

        public static void SelectDifficulty(DifficultySettings settings)
        {
            SelectedDifficulty = settings ?? new DifficultySettings();
        }

        public static bool IsMapEditorSession { get; private set; }

        public static void BeginMapEditorMode()
        {
            IsMapEditorSession = true;
        }

        public static void EndMapEditorMode()
        {
            IsMapEditorSession = false;
        }
    }
}
