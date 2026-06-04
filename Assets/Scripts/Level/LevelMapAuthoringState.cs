using System.Collections.Generic;
using UnityEngine;

namespace TD.Level
{
    public sealed class LevelMapAuthoringState
    {
        private LevelMapDefinition mapDefinition;
        private readonly Dictionary<Vector2Int, OccupantEntry> occupants = new Dictionary<Vector2Int, OccupantEntry>();
        private readonly Dictionary<Vector2Int, GroundType> groundOverrides = new Dictionary<Vector2Int, GroundType>();
        private readonly HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
        private List<PathSequence> loadedPathSequences = new List<PathSequence>();

        public LevelMapDefinition MapDefinition => mapDefinition;

        public void CreateNewMap(int width, int height, bool includeStraightPath)
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
                occupants = new List<OccupantEntry>(),
                pathSequences = new List<PathSequence>()
            };

            occupants.Clear();
            groundOverrides.Clear();
            pathCells.Clear();
            loadedPathSequences.Clear();

            if (includeStraightPath)
            {
                for (int column = 0; column < clampedWidth; column++)
                    pathCells.Add(new Vector2Int(column, pathRow));
            }
        }

        public void LoadDefinition(LevelMapDefinition definition)
        {
            occupants.Clear();
            groundOverrides.Clear();
            pathCells.Clear();
            loadedPathSequences.Clear();

            if (definition == null)
            {
                mapDefinition = null;
                return;
            }

            mapDefinition = definition.CloneNormalized();

            if (mapDefinition.occupants != null)
            {
                for (int i = 0; i < mapDefinition.occupants.Count; i++)
                {
                    OccupantEntry entry = mapDefinition.occupants[i];
                    if (entry != null)
                        occupants[entry.cell] = entry;
                }
            }

            if (mapDefinition.groundOverrides != null)
            {
                for (int i = 0; i < mapDefinition.groundOverrides.Count; i++)
                {
                    GroundOverrideEntry entry = mapDefinition.groundOverrides[i];
                    if (entry != null)
                        groundOverrides[entry.cell] = entry.type;
                }
            }

            if (mapDefinition.pathCells != null)
            {
                for (int i = 0; i < mapDefinition.pathCells.Count; i++)
                    pathCells.Add(mapDefinition.pathCells[i]);
            }

            pathCells.Add(mapDefinition.startCell);
            pathCells.Add(mapDefinition.goalCell);
            loadedPathSequences = LevelMapDefinition.ClonePathSequences(mapDefinition.pathSequences);
        }

        public LevelMapDefinition BuildDefinition()
        {
            if (mapDefinition == null)
                return null;

            List<OccupantEntry> occupantList = new List<OccupantEntry>(occupants.Count);
            foreach (KeyValuePair<Vector2Int, OccupantEntry> pair in occupants)
                occupantList.Add(pair.Value);

            List<GroundOverrideEntry> groundList = new List<GroundOverrideEntry>(groundOverrides.Count);
            foreach (KeyValuePair<Vector2Int, GroundType> pair in groundOverrides)
                groundList.Add(new GroundOverrideEntry { cell = pair.Key, type = pair.Value });

            List<PathSequence> sequences = PathSequencesStillMatch(pathCells, loadedPathSequences, mapDefinition.startCell, mapDefinition.goalCell)
                ? LevelMapDefinition.ClonePathSequences(loadedPathSequences)
                : new List<PathSequence>();

            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = mapDefinition.width,
                height = mapDefinition.height,
                startCell = mapDefinition.startCell,
                goalCell = mapDefinition.goalCell,
                blockedCells = new List<Vector2Int>(),
                pathCells = new List<Vector2Int>(pathCells),
                occupants = occupantList,
                groundOverrides = groundList,
                pathSequences = sequences
            };

            definition.Normalize();
            return definition;
        }

        public bool PaintCell(Vector2Int cell, LevelMapPaintTool tool, int destructibleHp, int destructibleReward)
        {
            if (mapDefinition == null)
                return false;

            bool changed = false;
            bool pathChanged = false;

            switch (tool)
            {
                case LevelMapPaintTool.Path:
                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    pathChanged |= pathCells.Add(cell);
                    break;
                case LevelMapPaintTool.Erase:
                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    if (cell != mapDefinition.startCell && cell != mapDefinition.goalCell)
                        pathChanged |= pathCells.Remove(cell);
                    break;
                case LevelMapPaintTool.Rock:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    pathChanged |= pathCells.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    changed |= SetOccupant(cell, OccupantType.Rock, 0, 0);
                    break;
                case LevelMapPaintTool.Destructible:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    pathChanged |= pathCells.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    changed |= SetOccupant(cell, OccupantType.Destructible, Mathf.Max(1, destructibleHp), Mathf.Max(0, destructibleReward));
                    break;
                case LevelMapPaintTool.Elevated:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    pathChanged |= pathCells.Remove(cell);
                    changed |= occupants.Remove(cell);
                    changed |= SetGroundOverride(cell, GroundType.Elevated);
                    break;
                case LevelMapPaintTool.Water:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    pathChanged |= pathCells.Remove(cell);
                    changed |= occupants.Remove(cell);
                    changed |= SetGroundOverride(cell, GroundType.Water);
                    break;
                case LevelMapPaintTool.Lava:
                    if (cell == mapDefinition.startCell || cell == mapDefinition.goalCell)
                        return false;
                    pathChanged |= pathCells.Remove(cell);
                    changed |= occupants.Remove(cell);
                    changed |= SetGroundOverride(cell, GroundType.Lava);
                    break;
                case LevelMapPaintTool.Start:
                    if (cell == mapDefinition.goalCell)
                        return false;

                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    if (mapDefinition.startCell != cell)
                    {
                        pathChanged |= pathCells.Remove(mapDefinition.startCell);
                        mapDefinition.startCell = cell;
                        pathChanged |= pathCells.Add(cell);
                        changed = true;
                    }
                    break;
                case LevelMapPaintTool.Goal:
                    if (cell == mapDefinition.startCell)
                        return false;

                    changed |= occupants.Remove(cell);
                    changed |= groundOverrides.Remove(cell);
                    if (mapDefinition.goalCell != cell)
                    {
                        pathChanged |= pathCells.Remove(mapDefinition.goalCell);
                        mapDefinition.goalCell = cell;
                        pathChanged |= pathCells.Add(cell);
                        changed = true;
                    }
                    break;
            }

            if (pathChanged)
            {
                loadedPathSequences.Clear();
                changed = true;
            }

            return changed;
        }

        public bool GenerateRandomPath(int seed)
        {
            if (mapDefinition == null)
                return false;

            List<Vector2Int> path = MapGenerator.GeneratePath(
                mapDefinition.width,
                mapDefinition.height,
                mapDefinition.startCell,
                mapDefinition.goalCell,
                seed);

            if (path == null || path.Count == 0)
                return false;

            pathCells.Clear();
            for (int i = 0; i < path.Count; i++)
                pathCells.Add(path[i]);

            pathCells.Add(mapDefinition.startCell);
            pathCells.Add(mapDefinition.goalCell);

            foreach (Vector2Int cell in pathCells)
            {
                occupants.Remove(cell);
                groundOverrides.Remove(cell);
            }

            loadedPathSequences = new List<PathSequence> { new PathSequence(path) };
            return true;
        }

        public bool ScatterRandomBlocks(int seed, MapGenerator.ScatterParams parameters)
        {
            LevelMapDefinition definition = BuildDefinition();
            if (definition == null)
                return false;

            MapGenerator.ScatterBlocks(definition, seed, parameters);
            definition.Normalize();
            LoadDefinition(definition);
            return true;
        }

        public bool TryGetOccupant(Vector2Int cell, out OccupantEntry occupant)
        {
            return occupants.TryGetValue(cell, out occupant);
        }

        public bool TryGetGroundOverride(Vector2Int cell, out GroundType ground)
        {
            return groundOverrides.TryGetValue(cell, out ground);
        }

        public bool IsPathCell(Vector2Int cell)
        {
            return pathCells.Contains(cell);
        }

        private bool SetOccupant(Vector2Int cell, OccupantType type, int maxHp, int reward)
        {
            if (occupants.TryGetValue(cell, out OccupantEntry existing)
                && existing.type == type
                && existing.maxHp == maxHp
                && existing.reward == reward)
            {
                return false;
            }

            occupants[cell] = new OccupantEntry { cell = cell, type = type, maxHp = maxHp, reward = reward };
            return true;
        }

        private bool SetGroundOverride(Vector2Int cell, GroundType type)
        {
            if (groundOverrides.TryGetValue(cell, out GroundType existing) && existing == type)
                return false;

            groundOverrides[cell] = type;
            return true;
        }

        private static bool PathSequencesStillMatch(HashSet<Vector2Int> currentPathCells, List<PathSequence> sequences, Vector2Int startCell, Vector2Int goalCell)
        {
            if (sequences == null || sequences.Count == 0)
                return false;

            HashSet<Vector2Int> union = new HashSet<Vector2Int>();
            foreach (PathSequence seq in sequences)
            {
                if (seq?.cells == null)
                    return false;

                if (seq.cells.Count == 0 || seq.cells[0] != startCell || seq.cells[seq.cells.Count - 1] != goalCell)
                    return false;

                foreach (Vector2Int cell in seq.cells)
                    union.Add(cell);
            }

            return union.SetEquals(currentPathCells);
        }
    }
}
