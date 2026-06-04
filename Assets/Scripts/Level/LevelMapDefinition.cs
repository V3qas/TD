using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TD.Combat;
using TD.Enemies;
using TD.Grid;

namespace TD.Level
{
    public enum GroundType : byte
    {
        Ground = 0,
        Path = 1,
        Elevated = 2,
        Water = 3,
        Lava = 4
    }

    public enum OccupantType : byte
    {
        None = 0,
        Rock = 1,
        Destructible = 2
    }

    [Serializable]
    public class GroundOverrideEntry
    {
        public Vector2Int cell;
        public GroundType type;
    }

    [Serializable]
    public class OccupantEntry
    {
        public Vector2Int cell;
        public OccupantType type;
        public int maxHp;   // 0 means indestructible (used by Rock)
        public int reward;  // money awarded on destruction (Destructible)
    }

    [Serializable]
    public class PathSequence
    {
        public List<Vector2Int> cells = new List<Vector2Int>();

        public PathSequence() { }

        public PathSequence(IEnumerable<Vector2Int> source)
        {
            cells = source != null ? new List<Vector2Int>(source) : new List<Vector2Int>();
        }
    }

    [Serializable]
    public class LevelMapDefinition
    {
        public const int CurrentVersion = 3;
        public const int MaxSize = 70;

        public int version = CurrentVersion;
        public int width = 10;
        public int height = 6;
        public Vector2Int startCell = new Vector2Int(0, 2);
        public Vector2Int goalCell = new Vector2Int(9, 2);

        // v1 legacy field. Retained ONLY so old seeds still deserialize. Normalize() migrates
        // every entry into 'occupants' (as indestructible Rocks) and clears this list, so the
        // canonical definition stores blockers exactly once.
        public List<Vector2Int> blockedCells = new List<Vector2Int>();

        // Union of all cells covered by any pathSequence. Derived by Normalize() and kept around
        // because many existing consumers index it directly (editors, GridManager preview, etc.).
        public List<Vector2Int> pathCells = new List<Vector2Int>();

        // Non-Ground tiles (Elevated/Water/Lava). Default ground = Ground, so only overrides stored.
        public List<GroundOverrideEntry> groundOverrides = new List<GroundOverrideEntry>();

        // Occupants on top of a tile (Rock, Destructible). After Normalize() this also contains
        // the migrated v1 blockers as indestructible Rocks (maxHp = 0).
        public List<OccupantEntry> occupants = new List<OccupantEntry>();

        // v3: ordered, 4-connected enemy paths from startCell to goalCell. Multiple sequences
        // are allowed and may share cells; shared cells form natural split / merge junctions for
        // future enemy AI. Single-path maps simply have pathSequences.Count == 1.
        public List<PathSequence> pathSequences = new List<PathSequence>();

        public bool HasExplicitPath => (pathSequences != null && pathSequences.Count > 0)
            || (pathCells != null && pathCells.Count > 0);
        public bool HasMultiplePaths => pathSequences != null && pathSequences.Count > 1;

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
                pathCells = pathCells != null ? new List<Vector2Int>(pathCells) : new List<Vector2Int>(),
                groundOverrides = CloneGroundOverrides(groundOverrides),
                occupants = CloneOccupants(occupants),
                pathSequences = ClonePathSequences(pathSequences)
            };
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

        public LevelMapDefinition CloneNormalized()
        {
            LevelMapDefinition copy = Clone();
            copy.Normalize();
            return copy;
        }

        public void Normalize()
        {
            version = CurrentVersion;
            width = Mathf.Clamp(width, 1, MaxSize);
            height = Mathf.Clamp(height, 1, MaxSize);
            startCell = ClampToBounds(startCell);
            goalCell = ClampToBounds(goalCell);

            // Step 1: migrate v1 blockedCells -> Rock occupants. After this, blockedCells is empty.
            MigrateLegacyBlockedCells();

            // Step 2: reconcile pathSequences <-> pathCells. After this, pathCells is the union of
            // all sequence cells (plus start/goal) and is sorted; if pathSequences was empty but
            // pathCells had entries (v1/v2 seeds), one sequence is reconstructed via BFS.
            NormalizePathSequences();

            // Step 3: dedupe ground overrides and drop entries that conflict with path/start/goal.
            NormalizeGroundOverrides();

            // Step 4: dedupe occupants and drop entries that conflict with path/start/goal.
            NormalizeOccupants();

            // Step 5: build O(1) lookup caches used by IsPath/GetGround/IsBuildable/TryGetOccupant.
            RebuildCaches();
        }

        private void MigrateLegacyBlockedCells()
        {
            if (blockedCells == null)
            {
                blockedCells = new List<Vector2Int>();
                return;
            }
            if (blockedCells.Count == 0)
                return;

            if (occupants == null)
                occupants = new List<OccupantEntry>();

            HashSet<Vector2Int> existing = new HashSet<Vector2Int>();
            foreach (OccupantEntry entry in occupants)
            {
                if (entry != null)
                    existing.Add(entry.cell);
            }

            foreach (Vector2Int cell in blockedCells)
            {
                if (!IsInBounds(cell))
                    continue;
                if (cell == startCell || cell == goalCell)
                    continue;
                if (!existing.Add(cell))
                    continue;
                occupants.Add(new OccupantEntry { cell = cell, type = OccupantType.Rock, maxHp = 0, reward = 0 });
            }

            blockedCells.Clear();
        }

        private void NormalizePathSequences()
        {
            if (pathSequences == null)
                pathSequences = new List<PathSequence>();

            // Drop nulls and empty sequences.
            for (int i = pathSequences.Count - 1; i >= 0; i--)
            {
                PathSequence seq = pathSequences[i];
                if (seq == null || seq.cells == null || seq.cells.Count == 0)
                    pathSequences.RemoveAt(i);
            }

            // v1/v2 migration: rebuild a single ordered sequence from the unordered pathCells set.
            if (pathSequences.Count == 0 && pathCells != null && pathCells.Count > 0)
            {
                HashSet<Vector2Int> set = new HashSet<Vector2Int>();
                foreach (Vector2Int cell in pathCells)
                {
                    if (IsInBounds(cell))
                        set.Add(cell);
                }
                set.Add(startCell);
                set.Add(goalCell);

                List<Vector2Int> ordered = ReconstructOrderedPath(startCell, goalCell, set);
                if (ordered != null && ordered.Count > 0)
                    pathSequences.Add(new PathSequence(ordered));
            }

            // Sanitize each sequence: drop out-of-bounds cells and consecutive duplicates.
            for (int i = pathSequences.Count - 1; i >= 0; i--)
            {
                PathSequence seq = pathSequences[i];
                List<Vector2Int> cleaned = new List<Vector2Int>(seq.cells.Count);
                Vector2Int last = new Vector2Int(int.MinValue, int.MinValue);
                foreach (Vector2Int cell in seq.cells)
                {
                    if (!IsInBounds(cell))
                        continue;
                    if (cleaned.Count > 0 && cell == last)
                        continue;
                    cleaned.Add(cell);
                    last = cell;
                }
                seq.cells = cleaned;
                if (cleaned.Count == 0)
                    pathSequences.RemoveAt(i);
            }

            // Rebuild pathCells as the union of every sequence (plus start/goal when any sequence exists).
            if (pathSequences.Count > 0)
            {
                HashSet<Vector2Int> union = new HashSet<Vector2Int>();
                foreach (PathSequence seq in pathSequences)
                {
                    foreach (Vector2Int cell in seq.cells)
                        union.Add(cell);
                }
                union.Add(startCell);
                union.Add(goalCell);
                pathCells = new List<Vector2Int>(union);
                SortCells(pathCells);
            }
            else
            {
                pathCells = new List<Vector2Int>();
            }
        }

        private static List<Vector2Int> ReconstructOrderedPath(Vector2Int start, Vector2Int goal, HashSet<Vector2Int> allowedCells)
        {
            if (allowedCells == null || !allowedCells.Contains(start) || !allowedCells.Contains(goal))
                return null;

            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> prev = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                if (cur == goal)
                    break;
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int n = cur + d;
                    if (!allowedCells.Contains(n) || prev.ContainsKey(n))
                        continue;
                    prev[n] = cur;
                    queue.Enqueue(n);
                }
            }

            if (!prev.ContainsKey(goal))
                return null;

            List<Vector2Int> result = new List<Vector2Int>();
            Vector2Int node = goal;
            while (node != start)
            {
                result.Add(node);
                node = prev[node];
            }
            result.Add(start);
            result.Reverse();
            return result;
        }

        private void RebuildCaches()
        {
            // Caches were intentionally removed: with MaxSize=70 (<=4900 cells) the lookup
            // helpers scan the underlying lists directly. Keeping a method here as a no-op
            // so existing Normalize() callers don't need to change.
        }

        private void NormalizeGroundOverrides()
        {
            if (groundOverrides == null)
            {
                groundOverrides = new List<GroundOverrideEntry>();
                return;
            }

            Dictionary<Vector2Int, GroundType> unique = new Dictionary<Vector2Int, GroundType>();
            HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>(pathCells ?? new List<Vector2Int>());
            pathSet.Add(startCell);
            pathSet.Add(goalCell);

            foreach (GroundOverrideEntry entry in groundOverrides)
            {
                if (entry == null)
                    continue;

                if (!IsInBounds(entry.cell))
                    continue;

                // Plain Ground/Path are not overrides; drop them.
                if (entry.type == GroundType.Ground || entry.type == GroundType.Path)
                    continue;

                // Path tiles must remain Path; drop conflicting overrides.
                if (pathSet.Contains(entry.cell))
                    continue;

                unique[entry.cell] = entry.type;
            }

            List<GroundOverrideEntry> normalized = new List<GroundOverrideEntry>(unique.Count);
            foreach (KeyValuePair<Vector2Int, GroundType> pair in unique)
                normalized.Add(new GroundOverrideEntry { cell = pair.Key, type = pair.Value });

            normalized.Sort((a, b) =>
            {
                int rowCompare = a.cell.y.CompareTo(b.cell.y);
                return rowCompare != 0 ? rowCompare : a.cell.x.CompareTo(b.cell.x);
            });

            groundOverrides = normalized;
        }

        private void NormalizeOccupants()
        {
            if (occupants == null)
            {
                occupants = new List<OccupantEntry>();
                return;
            }

            Dictionary<Vector2Int, OccupantEntry> unique = new Dictionary<Vector2Int, OccupantEntry>();
            HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>(pathCells ?? new List<Vector2Int>());
            pathSet.Add(startCell);
            pathSet.Add(goalCell);

            foreach (OccupantEntry entry in occupants)
            {
                if (entry == null)
                    continue;

                if (entry.type == OccupantType.None)
                    continue;

                if (!IsInBounds(entry.cell))
                    continue;

                // Occupants cannot sit on path tiles, start, or goal.
                if (pathSet.Contains(entry.cell))
                    continue;

                unique[entry.cell] = new OccupantEntry
                {
                    cell = entry.cell,
                    type = entry.type,
                    maxHp = Mathf.Max(0, entry.maxHp),
                    reward = Mathf.Max(0, entry.reward)
                };
            }

            List<OccupantEntry> normalized = new List<OccupantEntry>(unique.Values);
            normalized.Sort((a, b) =>
            {
                int rowCompare = a.cell.y.CompareTo(b.cell.y);
                return rowCompare != 0 ? rowCompare : a.cell.x.CompareTo(b.cell.x);
            });

            occupants = normalized;
        }

        public bool IsPath(Vector2Int cell)
        {
            if (!IsInBounds(cell))
                return false;
            if (cell == startCell || cell == goalCell)
                return true;
            if (pathCells != null)
            {
                for (int i = 0; i < pathCells.Count; i++)
                    if (pathCells[i] == cell)
                        return true;
            }
            if (pathSequences != null)
            {
                for (int s = 0; s < pathSequences.Count; s++)
                {
                    PathSequence seq = pathSequences[s];
                    if (seq?.cells == null) continue;
                    for (int i = 0; i < seq.cells.Count; i++)
                        if (seq.cells[i] == cell)
                            return true;
                }
            }
            return false;
        }

        public GroundType GetGround(Vector2Int cell)
        {
            if (!IsInBounds(cell))
                return GroundType.Ground;
            if (IsPath(cell))
                return GroundType.Path;
            if (groundOverrides != null)
            {
                for (int i = 0; i < groundOverrides.Count; i++)
                {
                    GroundOverrideEntry entry = groundOverrides[i];
                    if (entry != null && entry.cell == cell)
                        return entry.type;
                }
            }
            return GroundType.Ground;
        }

        public bool TryGetOccupant(Vector2Int cell, out OccupantEntry occupant)
        {
            occupant = null;
            if (!IsInBounds(cell) || occupants == null)
                return false;
            for (int i = 0; i < occupants.Count; i++)
            {
                OccupantEntry entry = occupants[i];
                if (entry != null && entry.type != OccupantType.None && entry.cell == cell)
                {
                    occupant = entry;
                    return true;
                }
            }
            return false;
        }

        public bool IsBuildable(Vector2Int cell)
        {
            if (!IsInBounds(cell))
                return false;
            if (cell == startCell || cell == goalCell)
                return false;
            if (IsPath(cell))
                return false;
            GroundType ground = GetGround(cell);
            if (ground == GroundType.Water || ground == GroundType.Lava)
                return false;
            if (TryGetOccupant(cell, out _))
                return false;
            return true;
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

        /// <summary>
        /// Decodes a seed or JSON payload and returns the map definition <b>without</b> running
        /// <see cref="LevelMapDefinition.Normalize"/>. Use this when the caller wants to run
        /// <see cref="LevelMapValidator.Validate"/> against the authored data before any
        /// silent migration / cleanup happens (e.g. occupants placed on path cells would be
        /// dropped by Normalize and never seen by the validator).
        /// </summary>
        public static bool TryDecodeRaw(string seedOrJson, out LevelMapDefinition definition, out string error)
        {
            definition = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(seedOrJson))
            {
                error = "Seed or JSON is empty.";
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
                error = $"Seed could not be read: {exception.Message}";
                return false;
            }

            if (definition == null)
            {
                error = "Seed contains no map data.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Convenience wrapper around <see cref="TryDecodeRaw"/> that additionally normalizes
        /// the decoded definition. Suitable for runtime callers that just want a ready-to-use
        /// map and do not run the validator themselves.
        /// </summary>
        public static bool TryDecode(string seedOrJson, out LevelMapDefinition definition, out string error)
        {
            if (!TryDecodeRaw(seedOrJson, out definition, out error))
                return false;

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
        private const string LegacyPlayerPrefsKey = "TD.CustomMaps.v1";
        private const string FileName = "customMaps.json";
        private const int MaxCustomMaps = 100;

        private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static List<CustomMapEntry> GetAll()
        {
            CustomMapCollection collection = LoadCollection();
            return collection.maps != null ? new List<CustomMapEntry>(collection.maps) : new List<CustomMapEntry>();
        }

        public static bool Save(string seed, out CustomMapEntry savedEntry, out string error)
        {
            savedEntry = null;

            // Validate against the raw decoded data so malformed seeds (e.g. occupants on path
            // cells) are rejected instead of silently fixed up by Normalize().
            if (!LevelMapSeedUtility.TryDecodeRaw(seed, out LevelMapDefinition definition, out error))
                return false;

            if (!LevelMapValidator.Validate(definition, true, out error))
                return false;

            definition.Normalize();

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
            string json = ReadFromDisk();

            // First-run migration: pull data from PlayerPrefs and persist it as a file.
            if (string.IsNullOrWhiteSpace(json))
            {
                string legacyJson = PlayerPrefs.GetString(LegacyPlayerPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(legacyJson))
                {
                    CustomMapCollection migrated = ParseCollection(legacyJson);
                    SaveCollection(migrated);
                    return migrated;
                }

                return new CustomMapCollection();
            }

            return ParseCollection(json);
        }

        private static CustomMapCollection ParseCollection(string json)
        {
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

        private static string ReadFromDisk()
        {
            try
            {
                string path = FilePath;
                return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"CustomMapStorage: failed to read '{FileName}' ({exception.Message}).");
                return string.Empty;
            }
        }

        private static void SaveCollection(CustomMapCollection collection)
        {
            string json = JsonUtility.ToJson(collection, true);

            try
            {
                string path = FilePath;
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);

                System.IO.File.WriteAllText(path, json);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"CustomMapStorage: failed to write '{FileName}' ({exception.Message}). Falling back to PlayerPrefs.");
                PlayerPrefs.SetString(LegacyPlayerPrefsKey, json);
                PlayerPrefs.Save();
            }
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
                message = "Map data is missing.";
                return false;
            }

            // Validation runs on the *authored* (raw) definition so that malformed input is
            // rejected loudly. Normalize() is only consulted for derived information that has
            // no authoritative source on the raw object.
            int width = definition.width;
            int height = definition.height;

            if (width < 1 || width > LevelMapDefinition.MaxSize ||
                height < 1 || height > LevelMapDefinition.MaxSize)
            {
                message = $"The map may be at most {LevelMapDefinition.MaxSize}x{LevelMapDefinition.MaxSize} tiles.";
                return false;
            }

            if (!IsInBounds(definition.startCell, width, height))
            {
                message = "Start is outside the map.";
                return false;
            }
            if (!IsInBounds(definition.goalCell, width, height))
            {
                message = "Goal is outside the map.";
                return false;
            }
            if (definition.startCell == definition.goalCell)
            {
                message = "Start and goal must be different tiles.";
                return false;
            }

            // Effective blockers: legacy blockedCells + non-None occupants. Either source
            // counts as a blocker for path / build / connectivity checks.
            HashSet<Vector2Int> blockerCells = new HashSet<Vector2Int>();
            if (definition.blockedCells != null)
            {
                foreach (Vector2Int cell in definition.blockedCells)
                {
                    if (!IsInBounds(cell, width, height))
                    {
                        message = $"Blocked tile {cell} is outside the map.";
                        return false;
                    }
                    blockerCells.Add(cell);
                }
            }

            if (definition.occupants != null)
            {
                HashSet<Vector2Int> occupantSeen = new HashSet<Vector2Int>();
                foreach (OccupantEntry entry in definition.occupants)
                {
                    if (entry == null || entry.type == OccupantType.None)
                        continue;
                    if (!IsInBounds(entry.cell, width, height))
                    {
                        message = $"Object tile {entry.cell} is outside the map.";
                        return false;
                    }
                    if (entry.type == OccupantType.Destructible && entry.maxHp <= 0)
                    {
                        message = $"Destructible object at {entry.cell} needs maxHp > 0.";
                        return false;
                    }
                    if (!occupantSeen.Add(entry.cell))
                    {
                        message = $"Object cell {entry.cell} is duplicated.";
                        return false;
                    }
                    blockerCells.Add(entry.cell);
                }
            }

            if (blockerCells.Contains(definition.startCell) || blockerCells.Contains(definition.goalCell))
            {
                message = "Start and goal must not be blocked.";
                return false;
            }

            if (!ValidateGroundOverridesRaw(definition, blockerCells, out message))
                return false;

            bool hasPathSequences = definition.pathSequences != null && definition.pathSequences.Count > 0;
            bool hasLegacyPathCells = !hasPathSequences && definition.pathCells != null && definition.pathCells.Count > 0;
            bool hasExplicitPath = hasPathSequences || hasLegacyPathCells;

            if (requireExplicitPath && !hasExplicitPath)
            {
                message = "A continuous path must be drawn.";
                return false;
            }

            if (hasPathSequences)
                return ValidatePathSequencesRaw(definition, blockerCells, out message);

            if (hasLegacyPathCells)
                return ValidateLegacyPathCells(definition, blockerCells, out message);

            return ValidateOpenGridPathRaw(definition, blockerCells, out message);
        }

        private static bool ValidatePathSequencesRaw(LevelMapDefinition definition, HashSet<Vector2Int> blockerCells, out string message)
        {
            for (int i = 0; i < definition.pathSequences.Count; i++)
            {
                PathSequence seq = definition.pathSequences[i];
                if (seq == null || seq.cells == null || seq.cells.Count < 2)
                {
                    message = $"Path #{i + 1} has too few cells.";
                    return false;
                }
                if (seq.cells[0] != definition.startCell)
                {
                    message = $"Path #{i + 1} must start at the start cell.";
                    return false;
                }
                if (seq.cells[seq.cells.Count - 1] != definition.goalCell)
                {
                    message = $"Path #{i + 1} must end at the goal cell.";
                    return false;
                }
                HashSet<Vector2Int> seqSeen = new HashSet<Vector2Int>();
                for (int k = 0; k < seq.cells.Count; k++)
                {
                    Vector2Int cell = seq.cells[k];
                    if (!IsInBounds(cell, definition.width, definition.height))
                    {
                        message = $"Path #{i + 1} contains an out-of-bounds cell {cell}.";
                        return false;
                    }
                    if (blockerCells.Contains(cell))
                    {
                        message = $"Path #{i + 1} runs through a blocked cell at {cell}.";
                        return false;
                    }
                    if (!seqSeen.Add(cell))
                    {
                        message = $"Path #{i + 1} visits {cell} twice.";
                        return false;
                    }
                    if (k > 0)
                    {
                        Vector2Int diff = cell - seq.cells[k - 1];
                        if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) != 1)
                        {
                            message = $"Path #{i + 1} has a non-adjacent step from {seq.cells[k - 1]} to {cell}.";
                            return false;
                        }
                    }
                }
            }

            message = "Map is valid.";
            return true;
        }

        private static bool ValidateLegacyPathCells(LevelMapDefinition definition, HashSet<Vector2Int> blockerCells, out string message)
        {
            HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in definition.pathCells)
            {
                if (!IsInBounds(cell, definition.width, definition.height))
                {
                    message = $"Path tile {cell} is outside the map.";
                    return false;
                }
                if (blockerCells.Contains(cell))
                {
                    message = $"Path tile {cell} overlaps a blocked cell.";
                    return false;
                }
                pathSet.Add(cell);
            }
            pathSet.Add(definition.startCell);
            pathSet.Add(definition.goalCell);

            // BFS along path cells; goal must be reachable.
            HashSet<Vector2Int> visited = new HashSet<Vector2Int> { definition.startCell };
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(definition.startCell);
            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                if (cur == definition.goalCell)
                {
                    message = "Map is valid.";
                    return true;
                }
                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int n = cur + dir;
                    if (!pathSet.Contains(n) || !visited.Add(n))
                        continue;
                    queue.Enqueue(n);
                }
            }

            message = "Path cells do not form a continuous route from start to goal.";
            return false;
        }

        private static bool ValidateOpenGridPathRaw(LevelMapDefinition definition, HashSet<Vector2Int> blockerCells, out string message)
        {
            Dictionary<Vector2Int, GroundType> ground = new Dictionary<Vector2Int, GroundType>();
            if (definition.groundOverrides != null)
            {
                foreach (GroundOverrideEntry entry in definition.groundOverrides)
                {
                    if (entry != null)
                        ground[entry.cell] = entry.type;
                }
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int> { definition.startCell };
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(definition.startCell);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                if (cur == definition.goalCell)
                {
                    message = "Map is valid.";
                    return true;
                }
                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int n = cur + dir;
                    if (!IsInBounds(n, definition.width, definition.height) || !visited.Add(n))
                        continue;
                    if (n != definition.goalCell)
                    {
                        if (blockerCells.Contains(n))
                            continue;
                        if (ground.TryGetValue(n, out GroundType gt) && (gt == GroundType.Water || gt == GroundType.Lava))
                            continue;
                    }
                    queue.Enqueue(n);
                }
            }

            message = "Start and goal are not connected.";
            return false;
        }

        private static bool ValidateGroundOverridesRaw(LevelMapDefinition definition, HashSet<Vector2Int> blockerCells, out string message)
        {
            message = string.Empty;
            if (definition.groundOverrides == null || definition.groundOverrides.Count == 0)
                return true;

            // Path cells from authored sources (sequences union OR legacy list).
            HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>();
            if (definition.pathSequences != null)
            {
                foreach (PathSequence seq in definition.pathSequences)
                {
                    if (seq?.cells == null) continue;
                    foreach (Vector2Int cell in seq.cells)
                        pathSet.Add(cell);
                }
            }
            if (definition.pathCells != null)
            {
                foreach (Vector2Int cell in definition.pathCells)
                    pathSet.Add(cell);
            }
            pathSet.Add(definition.startCell);
            pathSet.Add(definition.goalCell);

            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            foreach (GroundOverrideEntry entry in definition.groundOverrides)
            {
                if (entry == null)
                    continue;
                if (!IsInBounds(entry.cell, definition.width, definition.height))
                {
                    message = $"Ground tile {entry.cell} is outside the map.";
                    return false;
                }
                if (entry.type == GroundType.Ground || entry.type == GroundType.Path)
                {
                    message = $"Ground override for {entry.cell} must not be 'Ground' or 'Path'.";
                    return false;
                }
                if (pathSet.Contains(entry.cell))
                {
                    message = $"Ground override must not be on a path cell ({entry.cell}).";
                    return false;
                }
                if (blockerCells.Contains(entry.cell))
                {
                    message = $"Ground override and object overlap at {entry.cell}.";
                    return false;
                }
                if (!seen.Add(entry.cell))
                {
                    message = $"Ground override cell {entry.cell} is duplicated.";
                    return false;
                }
            }

            return true;
        }

        private static bool IsInBounds(Vector2Int cell, int width, int height)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }
    }
}
