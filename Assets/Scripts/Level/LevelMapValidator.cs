using System.Collections.Generic;
using UnityEngine;

namespace TD.Level
{
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
            {
                if (!ValidatePathCellsRaw(definition, blockerCells, out message))
                    return false;
                return ValidatePathSequencesRaw(definition, blockerCells, out message);
            }

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
            if (!ValidatePathCellsRaw(definition, blockerCells, out message))
                return false;

            HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in definition.pathCells)
                pathSet.Add(cell);
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

        private static bool ValidatePathCellsRaw(LevelMapDefinition definition, HashSet<Vector2Int> blockerCells, out string message)
        {
            message = string.Empty;
            if (definition.pathCells == null)
                return true;

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
            }

            return true;
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
