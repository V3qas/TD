using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class LevelMapDefinition
{
    public const int CurrentVersion = 1;
    public const int MaxSize = 500;

    public int version = CurrentVersion;
    public int width = 10;
    public int height = 6;
    public Vector2Int startCell = new Vector2Int(0, 2);
    public Vector2Int goalCell = new Vector2Int(9, 2);
    public List<Vector2Int> blockedCells = new List<Vector2Int>();
    public List<Vector2Int> pathCells = new List<Vector2Int>();

    public bool HasExplicitPath => pathCells != null && pathCells.Count > 0;

    public static LevelMapDefinition FromLegacy(
        int legacyWidth,
        int legacyHeight,
        Vector2Int legacyStartCell,
        Vector2Int legacyGoalCell,
        List<Vector2Int> legacyBlockedCells,
        List<Vector2Int> legacyPathCells)
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            version = CurrentVersion,
            width = legacyWidth,
            height = legacyHeight,
            startCell = legacyStartCell,
            goalCell = legacyGoalCell,
            blockedCells = legacyBlockedCells != null ? new List<Vector2Int>(legacyBlockedCells) : new List<Vector2Int>(),
            pathCells = legacyPathCells != null ? new List<Vector2Int>(legacyPathCells) : new List<Vector2Int>()
        };

        definition.Normalize();
        return definition;
    }

    public LevelMapDefinition Clone()
    {
        return new LevelMapDefinition
        {
            version = version,
            width = width,
            height = height,
            startCell = startCell,
            goalCell = goalCell,
            blockedCells = blockedCells != null ? new List<Vector2Int>(blockedCells) : new List<Vector2Int>(),
            pathCells = pathCells != null ? new List<Vector2Int>(pathCells) : new List<Vector2Int>()
        };
    }

    public LevelMapDefinition CloneNormalized()
    {
        LevelMapDefinition copy = Clone();
        copy.Normalize();
        return copy;
    }

    public void Normalize()
    {
        bool hasExplicitPath = HasExplicitPath;

        version = CurrentVersion;
        width = Mathf.Clamp(width, 1, MaxSize);
        height = Mathf.Clamp(height, 1, MaxSize);
        startCell = ClampToBounds(startCell);
        goalCell = ClampToBounds(goalCell);

        blockedCells = NormalizeCells(blockedCells);
        pathCells = NormalizeCells(pathCells);

        blockedCells.RemoveAll(cell => cell == startCell || cell == goalCell);

        if (hasExplicitPath)
        {
            HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>(pathCells)
            {
                startCell,
                goalCell
            };

            HashSet<Vector2Int> blockedSet = new HashSet<Vector2Int>(blockedCells);
            pathSet.RemoveWhere(cell => blockedSet.Contains(cell));

            pathCells = new List<Vector2Int>(pathSet);
            SortCells(pathCells);
        }
    }

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }

    private Vector2Int ClampToBounds(Vector2Int cell)
    {
        return new Vector2Int(
            Mathf.Clamp(cell.x, 0, width - 1),
            Mathf.Clamp(cell.y, 0, height - 1)
        );
    }

    private List<Vector2Int> NormalizeCells(List<Vector2Int> cells)
    {
        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();

        if (cells != null)
        {
            foreach (Vector2Int cell in cells)
            {
                if (IsInBounds(cell))
                    uniqueCells.Add(cell);
            }
        }

        List<Vector2Int> normalizedCells = new List<Vector2Int>(uniqueCells);
        SortCells(normalizedCells);
        return normalizedCells;
    }

    private static void SortCells(List<Vector2Int> cells)
    {
        cells.Sort((firstCell, secondCell) =>
        {
            int rowCompare = firstCell.y.CompareTo(secondCell.y);
            return rowCompare != 0 ? rowCompare : firstCell.x.CompareTo(secondCell.x);
        });
    }
}

public static class LevelMapSeedUtility
{
    public const string SeedPrefix = "TDMS1:";

    public static string Encode(LevelMapDefinition definition)
    {
        string json = ToJson(definition, false);
        return SeedPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static string ToJson(LevelMapDefinition definition, bool prettyPrint = true)
    {
        if (definition == null)
            return string.Empty;

        LevelMapDefinition normalizedDefinition = definition.CloneNormalized();
        return JsonUtility.ToJson(normalizedDefinition, prettyPrint);
    }

    public static bool TryDecode(string seedOrJson, out LevelMapDefinition definition, out string error)
    {
        definition = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(seedOrJson))
        {
            error = "Seed oder JSON ist leer.";
            return false;
        }

        string payload = seedOrJson.Trim();
        string json;

        try
        {
            if (payload.StartsWith(SeedPrefix, StringComparison.Ordinal))
            {
                string encodedPayload = payload.Substring(SeedPrefix.Length);
                json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPayload));
            }
            else if (payload.StartsWith("{", StringComparison.Ordinal))
            {
                json = payload;
            }
            else
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            }

            definition = JsonUtility.FromJson<LevelMapDefinition>(json);
        }
        catch (Exception exception)
        {
            error = $"Seed konnte nicht gelesen werden: {exception.Message}";
            return false;
        }

        if (definition == null)
        {
            error = "Seed enthaelt keine Map-Daten.";
            return false;
        }

        definition.Normalize();
        return true;
    }
}

[Serializable]
public class CustomMapEntry
{
    public string label;
    public string seed;
}

[Serializable]
public class CustomMapCollection
{
    public List<CustomMapEntry> maps = new List<CustomMapEntry>();
}

public static class CustomMapStorage
{
    private const string PlayerPrefsKey = "TD.CustomMaps.v1";
    private const int MaxCustomMaps = 100;

    public static List<CustomMapEntry> GetAll()
    {
        CustomMapCollection collection = LoadCollection();
        return collection.maps != null ? new List<CustomMapEntry>(collection.maps) : new List<CustomMapEntry>();
    }

    public static bool Save(string seed, out CustomMapEntry savedEntry, out string error)
    {
        savedEntry = null;

        if (!LevelMapSeedUtility.TryDecode(seed, out LevelMapDefinition definition, out error))
            return false;

        if (!LevelMapValidator.Validate(definition, true, out error))
            return false;

        string normalizedSeed = LevelMapSeedUtility.Encode(definition);
        CustomMapCollection collection = LoadCollection();

        if (collection.maps == null)
            collection.maps = new List<CustomMapEntry>();

        savedEntry = collection.maps.Find(entry => entry != null && entry.seed == normalizedSeed);
        if (savedEntry == null)
        {
            savedEntry = new CustomMapEntry
            {
                label = $"Custom Map {collection.maps.Count + 1}",
                seed = normalizedSeed
            };

            collection.maps.Add(savedEntry);
        }
        else
        {
            savedEntry.seed = normalizedSeed;
        }

        while (collection.maps.Count > MaxCustomMaps)
            collection.maps.RemoveAt(0);

        SaveCollection(collection);
        error = string.Empty;
        return true;
    }

    private static CustomMapCollection LoadCollection()
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
            return new CustomMapCollection();

        try
        {
            CustomMapCollection collection = JsonUtility.FromJson<CustomMapCollection>(json);
            return collection ?? new CustomMapCollection();
        }
        catch
        {
            return new CustomMapCollection();
        }
    }

    private static void SaveCollection(CustomMapCollection collection)
    {
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(collection));
        PlayerPrefs.Save();
    }
}

public static class LevelMapValidator
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    public static bool Validate(LevelMapDefinition definition, bool requireExplicitPath, out string message)
    {
        if (definition == null)
        {
            message = "Map-Daten fehlen.";
            return false;
        }

        if (definition.width < 1 || definition.width > LevelMapDefinition.MaxSize ||
            definition.height < 1 || definition.height > LevelMapDefinition.MaxSize)
        {
            message = $"Die Map darf maximal {LevelMapDefinition.MaxSize}x{LevelMapDefinition.MaxSize} Tiles gross sein.";
            return false;
        }

        if (!IsInBounds(definition.startCell, definition.width, definition.height))
        {
            message = "Start liegt ausserhalb der Map.";
            return false;
        }

        if (!IsInBounds(definition.goalCell, definition.width, definition.height))
        {
            message = "Stop liegt ausserhalb der Map.";
            return false;
        }

        if (definition.startCell == definition.goalCell)
        {
            message = "Start und Stop muessen unterschiedliche Tiles sein.";
            return false;
        }

        if (!TryBuildCellSet(definition.blockedCells, definition.width, definition.height, "Blocker", out HashSet<Vector2Int> blockedCells, out message))
            return false;

        if (blockedCells.Contains(definition.startCell) || blockedCells.Contains(definition.goalCell))
        {
            message = "Start und Stop duerfen nicht blockiert sein.";
            return false;
        }

        bool hasExplicitPath = definition.pathCells != null && definition.pathCells.Count > 0;
        if (requireExplicitPath && !hasExplicitPath)
        {
            message = "Es muss ein durchgehender Pfad gezeichnet werden.";
            return false;
        }

        if (hasExplicitPath)
            return ValidateExplicitPath(definition, blockedCells, out message);

        return ValidateOpenGridPath(definition, blockedCells, out message);
    }

    private static bool ValidateExplicitPath(LevelMapDefinition definition, HashSet<Vector2Int> blockedCells, out string message)
    {
        if (!TryBuildCellSet(definition.pathCells, definition.width, definition.height, "Pfad", out HashSet<Vector2Int> pathCells, out message))
            return false;

        pathCells.Add(definition.startCell);
        pathCells.Add(definition.goalCell);

        foreach (Vector2Int pathCell in pathCells)
        {
            if (blockedCells.Contains(pathCell))
            {
                message = "Der Pfad darf keine blockierten Tiles enthalten.";
                return false;
            }
        }

        HashSet<Vector2Int> visitedCells = FindReachableCells(definition.startCell, definition.width, definition.height, pathCells, blockedCells);

        if (!visitedCells.Contains(definition.goalCell))
        {
            message = "Start und Stop muessen durch einen durchgehenden Pfad verbunden sein.";
            return false;
        }

        if (visitedCells.Count != pathCells.Count)
        {
            message = "Alle gezeichneten Pfad-Tiles muessen mit dem Start verbunden sein.";
            return false;
        }

        message = "Map ist gueltig.";
        return true;
    }

    private static bool ValidateOpenGridPath(LevelMapDefinition definition, HashSet<Vector2Int> blockedCells, out string message)
    {
        HashSet<Vector2Int> visitedCells = FindReachableCells(definition.startCell, definition.width, definition.height, null, blockedCells);

        if (!visitedCells.Contains(definition.goalCell))
        {
            message = "Start und Stop sind nicht verbunden.";
            return false;
        }

        message = "Map ist gueltig.";
        return true;
    }

    private static HashSet<Vector2Int> FindReachableCells(
        Vector2Int startCell,
        int width,
        int height,
        HashSet<Vector2Int> allowedCells,
        HashSet<Vector2Int> blockedCells)
    {
        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();

        if (!CanVisit(startCell, width, height, allowedCells, blockedCells))
            return visitedCells;

        frontier.Enqueue(startCell);
        visitedCells.Add(startCell);

        while (frontier.Count > 0)
        {
            Vector2Int currentCell = frontier.Dequeue();

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int nextCell = currentCell + direction;
                if (visitedCells.Contains(nextCell) || !CanVisit(nextCell, width, height, allowedCells, blockedCells))
                    continue;

                visitedCells.Add(nextCell);
                frontier.Enqueue(nextCell);
            }
        }

        return visitedCells;
    }

    private static bool CanVisit(
        Vector2Int cell,
        int width,
        int height,
        HashSet<Vector2Int> allowedCells,
        HashSet<Vector2Int> blockedCells)
    {
        if (!IsInBounds(cell, width, height))
            return false;

        if (blockedCells.Contains(cell))
            return false;

        return allowedCells == null || allowedCells.Contains(cell);
    }

    private static bool TryBuildCellSet(
        List<Vector2Int> cells,
        int width,
        int height,
        string label,
        out HashSet<Vector2Int> cellSet,
        out string message)
    {
        cellSet = new HashSet<Vector2Int>();
        message = string.Empty;

        if (cells == null)
            return true;

        foreach (Vector2Int cell in cells)
        {
            if (!IsInBounds(cell, width, height))
            {
                message = $"{label}-Tile {cell} liegt ausserhalb der Map.";
                return false;
            }

            cellSet.Add(cell);
        }

        return true;
    }

    private static bool IsInBounds(Vector2Int cell, int width, int height)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }
}