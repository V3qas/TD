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

    [Header("Static Obstacles (legacy v1)")]
    public List<Vector2Int> blockedCells = new List<Vector2Int>();

    [Header("Enemy Path (legacy v1/v2; pathSequences is canonical)")]
    public List<Vector2Int> pathCells = new List<Vector2Int>();

    [Header("Enemy Paths (v3, ordered, multi-path)")]
    public List<PathSequence> pathSequences = new List<PathSequence>();

    [Header("Ground Overrides")]
    public List<GroundOverrideEntry> groundOverrides = new List<GroundOverrideEntry>();

    [Header("Occupants")]
    public List<OccupantEntry> occupants = new List<OccupantEntry>();

    public LevelMapDefinition GetMapDefinition()
    {
        LevelMapDefinition definition = BuildRawMapDefinition();
        definition.Normalize();
        return definition;
    }

    public bool TryGetMapDefinition(out LevelMapDefinition definition, out string error)
    {
        // Validate against the *raw* authored data so problems like occupants placed on a
        // path cell are reported instead of being silently swept away by Normalize().
        definition = BuildRawMapDefinition();
        if (!LevelMapValidator.Validate(definition, false, out error))
            return false;

        definition.Normalize();
        return true;
    }

    private LevelMapDefinition BuildRawMapDefinition()
    {
        if (!string.IsNullOrWhiteSpace(mapSeed))
        {
            if (LevelMapSeedUtility.TryDecodeRaw(mapSeed, out LevelMapDefinition seedDefinition, out string seedError))
                return seedDefinition;

            Debug.LogWarning($"LevelData '{name}': Map seed is invalid ({seedError}). Falling back to legacy fields.");
        }

        return new LevelMapDefinition
        {
            version = LevelMapDefinition.CurrentVersion,
            width = width,
            height = height,
            startCell = startCell,
            goalCell = goalCell,
            blockedCells = blockedCells != null ? new List<Vector2Int>(blockedCells) : new List<Vector2Int>(),
            pathCells = pathCells != null ? new List<Vector2Int>(pathCells) : new List<Vector2Int>(),
            groundOverrides = CloneGroundOverrides(groundOverrides),
            occupants = CloneOccupants(occupants),
            pathSequences = ClonePathSequences(pathSequences)
        };
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
        groundOverrides = CloneGroundOverrides(normalizedDefinition.groundOverrides);
        occupants = CloneOccupants(normalizedDefinition.occupants);
        pathSequences = ClonePathSequences(normalizedDefinition.pathSequences);

        if (updateSeed)
            mapSeed = LevelMapSeedUtility.Encode(normalizedDefinition);
    }

    private void OnValidate()
    {
        width = Mathf.Clamp(width, 1, LevelMapDefinition.MaxSize);
        height = Mathf.Clamp(height, 1, LevelMapDefinition.MaxSize);
    }

    private static List<GroundOverrideEntry> CloneGroundOverrides(List<GroundOverrideEntry> source)
    {
        List<GroundOverrideEntry> copy = new List<GroundOverrideEntry>();
        if (source == null)
            return copy;

        foreach (GroundOverrideEntry entry in source)
        {
            if (entry == null)
                continue;

            copy.Add(new GroundOverrideEntry { cell = entry.cell, type = entry.type });
        }

        return copy;
    }

    private static List<OccupantEntry> CloneOccupants(List<OccupantEntry> source)
    {
        List<OccupantEntry> copy = new List<OccupantEntry>();
        if (source == null)
            return copy;

        foreach (OccupantEntry entry in source)
        {
            if (entry == null)
                continue;

            copy.Add(new OccupantEntry
            {
                cell = entry.cell,
                type = entry.type,
                maxHp = entry.maxHp,
                reward = entry.reward
            });
        }

        return copy;
    }

    private static List<PathSequence> ClonePathSequences(List<PathSequence> source)
    {
        List<PathSequence> copy = new List<PathSequence>();
        if (source == null)
            return copy;
        foreach (PathSequence seq in source)
        {
            if (seq == null)
                continue;
            copy.Add(new PathSequence(seq.cells));
        }
        return copy;
    }
}
