using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "TowerDefense/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Grid Size")]
    public int width = 10;
    public int height = 6;

    [Header("Seed / JSON")]
    [TextArea(3, 12)] public string mapSeed;

    [Header("Special Cells")]
    public Vector2Int startCell = new Vector2Int(0, 2);
    public Vector2Int goalCell = new Vector2Int(9, 2);

    [Header("Static Obstacles")]
    public List<Vector2Int> blockedCells = new List<Vector2Int>();

    [Header("Enemy Path")]
    public List<Vector2Int> pathCells = new List<Vector2Int>();

    public LevelMapDefinition GetMapDefinition()
    {
        if (!string.IsNullOrWhiteSpace(mapSeed))
        {
            if (LevelMapSeedUtility.TryDecode(mapSeed, out LevelMapDefinition seedDefinition, out string seedError))
                return seedDefinition;

            Debug.LogWarning($"LevelData '{name}': Map seed ist ungueltig ({seedError}). Legacy-Felder werden verwendet.");
        }

        return LevelMapDefinition.FromLegacy(width, height, startCell, goalCell, blockedCells, pathCells);
    }

    public bool TryGetMapDefinition(out LevelMapDefinition definition, out string error)
    {
        definition = GetMapDefinition();
        return LevelMapValidator.Validate(definition, false, out error);
    }

    public void ApplyDefinition(LevelMapDefinition definition, bool updateSeed = true)
    {
        if (definition == null)
            return;

        LevelMapDefinition normalizedDefinition = definition.CloneNormalized();

        width = normalizedDefinition.width;
        height = normalizedDefinition.height;
        startCell = normalizedDefinition.startCell;
        goalCell = normalizedDefinition.goalCell;
        blockedCells = new List<Vector2Int>(normalizedDefinition.blockedCells);
        pathCells = new List<Vector2Int>(normalizedDefinition.pathCells);

        if (updateSeed)
            mapSeed = LevelMapSeedUtility.Encode(normalizedDefinition);
    }

    private void OnValidate()
    {
        width = Mathf.Clamp(width, 1, LevelMapDefinition.MaxSize);
        height = Mathf.Clamp(height, 1, LevelMapDefinition.MaxSize);
    }
}