using System;
using System.Collections.Generic;
using UnityEngine;

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

        // All authored path cells. For v3 maps with trusted pathSequences this is their union;
        // for legacy/editor-authored path-cell sets it can include extra branch cells while
        // pathSequences stores at least one representative route for systems that need ordering.
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

        public static List<PathSequence> ClonePathSequences(List<PathSequence> source)
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

        public static List<GroundOverrideEntry> CloneGroundOverrides(List<GroundOverrideEntry> source)
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

        public static List<OccupantEntry> CloneOccupants(List<OccupantEntry> source)
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

            // Step 2: reconcile pathSequences <-> pathCells. Keep authored pathCells when present,
            // and reconstruct one representative ordered route from them when no sequence exists.
            NormalizePathSequences();

            // Step 3: dedupe ground overrides and drop entries that conflict with path/start/goal.
            NormalizeGroundOverrides();

            // Step 4: dedupe occupants and drop entries that conflict with path/start/goal.
            NormalizeOccupants();

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

            HashSet<Vector2Int> authoredPathSet = null;
            if (pathCells != null && pathCells.Count > 0)
            {
                authoredPathSet = new HashSet<Vector2Int>();
                foreach (Vector2Int cell in pathCells)
                {
                    if (IsInBounds(cell))
                        authoredPathSet.Add(cell);
                }
                authoredPathSet.Add(startCell);
                authoredPathSet.Add(goalCell);
            }

            bool reconstructingFromPathCells = pathSequences.Count == 0 && authoredPathSet != null && authoredPathSet.Count > 0;
            // v1/v2 migration: rebuild a single ordered sequence from the unordered pathCells set.
            if (reconstructingFromPathCells)
            {
                List<Vector2Int> ordered = ReconstructOrderedPath(startCell, goalCell, authoredPathSet);
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

            // Rebuild pathCells from trusted sequences plus any authored path-cell set that was
            // present on the input definition.
            if (pathSequences.Count > 0)
            {
                HashSet<Vector2Int> union = new HashSet<Vector2Int>();
                foreach (PathSequence seq in pathSequences)
                {
                    foreach (Vector2Int cell in seq.cells)
                        union.Add(cell);
                }

                if (authoredPathSet != null)
                {
                    foreach (Vector2Int cell in authoredPathSet)
                        union.Add(cell);
                }

                union.Add(startCell);
                union.Add(goalCell);
                pathCells = new List<Vector2Int>(union);
                SortCells(pathCells);
            }
            else
            {
                pathCells = authoredPathSet != null
                    ? new List<Vector2Int>(authoredPathSet)
                    : new List<Vector2Int>();
                SortCells(pathCells);
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

        private static void SortCells(List<Vector2Int> cells)
        {
            cells.Sort((firstCell, secondCell) =>
            {
                int rowCompare = firstCell.y.CompareTo(secondCell.y);
                return rowCompare != 0 ? rowCompare : firstCell.x.CompareTo(secondCell.x);
            });
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

}
