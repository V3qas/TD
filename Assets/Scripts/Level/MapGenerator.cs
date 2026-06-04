using System.Collections.Generic;
using UnityEngine;
using TD.Combat;
using TD.Enemies;
using TD.Pathfinding;

/// <summary>
/// Deterministic procedural generators for paths and obstacles.
///
/// Determinism: every method takes a seed and only uses
/// <see cref="System.Random"/> instances local to the call. The same seed
/// always produces the same map. The <c>0</c> seed is treated as "random"
/// and is replaced internally with a time-based value.
/// </summary>

namespace TD.Level
{
    public static class MapGenerator
    {
        private const float MinCostJitter = 0f;
        private const float MaxCostJitter = 4f;

        public struct ScatterParams
        {
            /// <summary>0..1, fraction of empty cells that should become Rock.</summary>
            public float rockDensity;
            /// <summary>0..1, fraction of empty cells that should become Destructible.</summary>
            public float destructibleDensity;
            public int destructibleHp;
            public int destructibleReward;

            public static ScatterParams Default => new ScatterParams
            {
                rockDensity = 0.06f,
                destructibleDensity = 0.04f,
                destructibleHp = 100,
                destructibleReward = 15
            };
        }

        /// <summary>
        /// Builds a randomized path from <paramref name="start"/> to <paramref name="goal"/>.
        /// Uses A* with per-cell cost <c>1 + rng.next * MaxCostJitter</c> so the
        /// resulting path winds naturally instead of being a straight line.
        /// </summary>
        public static List<Vector2Int> GeneratePath(int width, int height, Vector2Int start, Vector2Int goal, int seed)
        {
            if (width <= 0 || height <= 0)
                return new List<Vector2Int>();

            if (!InBounds(start, width, height) || !InBounds(goal, width, height))
                return new List<Vector2Int>();

            System.Random rng = NewRng(seed);

            float[,] cellCost = new float[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cellCost[x, y] = 1f + (float)rng.NextDouble() * (MaxCostJitter - MinCostJitter) + MinCostJitter;

            return AStar(start, goal, width, height, cellCost);
        }

        /// <summary>
        /// Scatters Rock and Destructible occupants on cells that are not part of
        /// the existing path / start / goal. Path is preserved untouched.
        ///
        /// Uses two independent Perlin-noise lookups (offset per seed) to decide
        /// the type so clusters look organic rather than salt-and-pepper.
        /// </summary>
        public static void ScatterBlocks(LevelMapDefinition definition, int seed, ScatterParams parameters)
        {
            if (definition == null) return;

            System.Random rng = NewRng(seed);
            float noiseOffsetX = (float)rng.NextDouble() * 1000f;
            float noiseOffsetY = (float)rng.NextDouble() * 1000f;

            HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();
            if (definition.pathSequences != null)
            {
                foreach (PathSequence seq in definition.pathSequences)
                {
                    if (seq?.cells == null) continue;
                    foreach (Vector2Int cell in seq.cells)
                        reserved.Add(cell);
                }
            }
            if (definition.pathCells != null)
                foreach (Vector2Int cell in definition.pathCells)
                    reserved.Add(cell);
            reserved.Add(definition.startCell);
            reserved.Add(definition.goalCell);

            if (definition.occupants == null)
                definition.occupants = new List<OccupantEntry>();

            // Drop existing scatter so re-runs are stable.
            definition.occupants.RemoveAll(entry => entry.type == OccupantType.Rock || entry.type == OccupantType.Destructible);

            // Rock threshold is calibrated so density ~ probability per empty cell.
            // Perlin noise returns ~0.5 mean, so we shift to use density directly.
            for (int y = 0; y < definition.height; y++)
            {
                for (int x = 0; x < definition.width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (reserved.Contains(cell)) continue;

                    float noise = Mathf.PerlinNoise(noiseOffsetX + x * 0.35f, noiseOffsetY + y * 0.35f);

                    if (noise > 1f - parameters.rockDensity)
                    {
                        definition.occupants.Add(new OccupantEntry
                        {
                            cell = cell,
                            type = OccupantType.Rock,
                            maxHp = 0,
                            reward = 0
                        });
                    }
                    else if (noise < parameters.destructibleDensity)
                    {
                        definition.occupants.Add(new OccupantEntry
                        {
                            cell = cell,
                            type = OccupantType.Destructible,
                            maxHp = Mathf.Max(1, parameters.destructibleHp),
                            reward = Mathf.Max(0, parameters.destructibleReward)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Convenience helper: creates a fresh definition, lays a random path, and
        /// scatters blocks on the remaining cells. Returns a normalized definition.
        /// </summary>
        public static LevelMapDefinition GenerateFullMap(int width, int height, int seed, ScatterParams scatterParameters)
        {
            Vector2Int start = new Vector2Int(0, height / 2);
            Vector2Int goal = new Vector2Int(width - 1, height / 2);

            List<Vector2Int> path = GeneratePath(width, height, start, goal, seed);

            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = width,
                height = height,
                startCell = start,
                goalCell = goal,
                blockedCells = new List<Vector2Int>(),
                pathCells = path != null ? new List<Vector2Int>(path) : new List<Vector2Int>(),
                occupants = new List<OccupantEntry>(),
                groundOverrides = new List<GroundOverrideEntry>(),
                pathSequences = new List<PathSequence>()
            };

            if (path != null && path.Count > 0)
                definition.pathSequences.Add(new PathSequence(path));

            ScatterBlocks(definition, seed ^ 0x5A5A5A, scatterParameters);
            definition.Normalize();
            return definition;
        }

        /// <summary>
        /// Generates a map with up to two enemy paths sharing the same start and goal but
        /// taking different routes - one is pushed toward the upper half of the grid, the
        /// other toward the lower half. Cells where the sequences overlap form natural split /
        /// merge junctions for future enemy AI.
        ///
        /// The vertical bias is implemented as an additional cost penalty per cell: rows above
        /// the start row are cheaper for the "upper" path and more expensive for the "lower"
        /// path (and vice versa). This nudges A* through the requested half but still allows
        /// the routes to converge at start/goal so they form a real fork+merge shape.
        ///
        /// On very small or narrow grids (e.g. height &lt;= 2) the two biased searches can
        /// collapse onto the same A* solution; in that case the duplicate is dropped and the
        /// resulting map only carries a single PathSequence. Callers that strictly require two
        /// distinct routes must therefore check <see cref="LevelMapDefinition.HasMultiplePaths"/>.
        /// </summary>
        public static LevelMapDefinition GenerateForkAndMerge(int width, int height, int seed, ScatterParams scatterParameters)
        {
            Vector2Int start = new Vector2Int(0, height / 2);
            Vector2Int goal = new Vector2Int(width - 1, height / 2);

            // Upper path: rows above midline cheap, rows below midline expensive.
            List<Vector2Int> upper = GeneratePathBiased(width, height, start, goal, seed, verticalBias: +1f);
            // Lower path: opposite bias, plus a different jitter seed so noise differs too.
            List<Vector2Int> lower = GeneratePathBiased(width, height, start, goal, unchecked(seed * 31 + 1), verticalBias: -1f);

            // Defensive fallback: if the bias still produced identical paths (very narrow maps,
            // height==1, etc.), keep only the unique sequences. We never want two identical
            // PathSequence entries because that adds no information for the AI later.
            List<PathSequence> sequences = new List<PathSequence>();
            if (upper != null && upper.Count > 0)
                sequences.Add(new PathSequence(upper));
            if (lower != null && lower.Count > 0 && (upper == null || !PathsEqual(upper, lower)))
                sequences.Add(new PathSequence(lower));

            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = width,
                height = height,
                startCell = start,
                goalCell = goal,
                blockedCells = new List<Vector2Int>(),
                pathCells = new List<Vector2Int>(),
                occupants = new List<OccupantEntry>(),
                groundOverrides = new List<GroundOverrideEntry>(),
                pathSequences = sequences
            };

            ScatterBlocks(definition, seed ^ 0x5A5A5A, scatterParameters);
            definition.Normalize();
            return definition;
        }

        /// <summary>
        /// Same as <see cref="GeneratePath"/> but adds a vertical bias to the per-cell cost.
        /// Positive <paramref name="verticalBias"/> pushes the route toward the top half of the
        /// grid, negative toward the bottom half. The magnitude of the penalty grows linearly
        /// with the row distance from the start row.
        /// </summary>
        public static List<Vector2Int> GeneratePathBiased(int width, int height, Vector2Int start, Vector2Int goal, int seed, float verticalBias)
        {
            if (width <= 0 || height <= 0)
                return new List<Vector2Int>();
            if (!InBounds(start, width, height) || !InBounds(goal, width, height))
                return new List<Vector2Int>();

            System.Random rng = NewRng(seed);

            // Penalty per row of "wrong side" distance. 2.0 is roughly half the worst-case
            // jitter (MaxCostJitter == 4) so a single row of wrong-side travel does not always
            // dominate noise, but a few rows do - this keeps A* free to take small detours
            // while still pulling the route toward the requested half of the grid.
            const float biasPerRow = 2f;
            int midRow = start.y;

            float[,] cellCost = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float jitter = 1f + (float)rng.NextDouble() * (MaxCostJitter - MinCostJitter) + MinCostJitter;
                    // verticalBias > 0: penalize rows below midRow (encourage upper half).
                    // verticalBias < 0: penalize rows above midRow (encourage lower half).
                    float distance = (y - midRow) * -verticalBias; // >0 means "wrong side".
                    float penalty = distance > 0f ? distance * biasPerRow : 0f;
                    cellCost[x, y] = jitter + penalty;
                }
            }

            return AStar(start, goal, width, height, cellCost);
        }

        private static bool PathsEqual(List<Vector2Int> a, List<Vector2Int> b)
        {
            if (a == null || b == null || a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        private static System.Random NewRng(int seed)
        {
            if (seed == 0)
                seed = unchecked((int)System.DateTime.UtcNow.Ticks);
            return new System.Random(seed);
        }

        private static bool InBounds(Vector2Int cell, int width, int height)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }

        // A* path search with per-cell cost. Self-contained: kept here so the
        // generator does not depend on Pathfinder (which works on a built grid).
        private static readonly Vector2Int[] FourNeighbours =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        private static List<Vector2Int> AStar(Vector2Int start, Vector2Int goal, int width, int height, float[,] cellCost)
        {
            float[,] gScore = new float[width, height];
            Vector2Int[,] cameFrom = new Vector2Int[width, height];
            bool[,] visited = new bool[width, height];

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    gScore[x, y] = float.PositiveInfinity;
                    cameFrom[x, y] = new Vector2Int(-1, -1);
                }

            gScore[start.x, start.y] = 0f;

            // Simple list-based open set is fine for grids up to LevelMapDefinition.MaxSize.
            List<Vector2Int> open = new List<Vector2Int> { start };

            while (open.Count > 0)
            {
                int bestIndex = 0;
                float bestF = gScore[open[0].x, open[0].y] + Manhattan(open[0], goal);
                for (int i = 1; i < open.Count; i++)
                {
                    float f = gScore[open[i].x, open[i].y] + Manhattan(open[i], goal);
                    if (f < bestF)
                    {
                        bestF = f;
                        bestIndex = i;
                    }
                }

                Vector2Int current = open[bestIndex];
                open.RemoveAt(bestIndex);

                if (current == goal)
                    return Reconstruct(cameFrom, current, start);

                visited[current.x, current.y] = true;

                for (int n = 0; n < FourNeighbours.Length; n++)
                {
                    Vector2Int neighbour = current + FourNeighbours[n];
                    if (!InBounds(neighbour, width, height) || visited[neighbour.x, neighbour.y])
                        continue;

                    float tentative = gScore[current.x, current.y] + cellCost[neighbour.x, neighbour.y];
                    if (tentative < gScore[neighbour.x, neighbour.y])
                    {
                        cameFrom[neighbour.x, neighbour.y] = current;
                        gScore[neighbour.x, neighbour.y] = tentative;
                        if (!open.Contains(neighbour))
                            open.Add(neighbour);
                    }
                }
            }

            // No path found - return empty list.
            return new List<Vector2Int>();
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static List<Vector2Int> Reconstruct(Vector2Int[,] cameFrom, Vector2Int current, Vector2Int start)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int node = current;
            while (node != start)
            {
                path.Add(node);
                Vector2Int prev = cameFrom[node.x, node.y];
                if (prev.x < 0) break; // safety
                node = prev;
            }
            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
