using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class MapGeneratorTests
{
    [Test]
    public void GeneratePath_IsDeterministicForSameSeed()
    {
        Vector2Int start = new Vector2Int(0, 5);
        Vector2Int goal = new Vector2Int(15, 5);
        List<Vector2Int> a = MapGenerator.GeneratePath(20, 12, start, goal, 1234);
        List<Vector2Int> b = MapGenerator.GeneratePath(20, 12, start, goal, 1234);

        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
            Assert.AreEqual(a[i], b[i], $"Mismatch at index {i}.");
    }

    [Test]
    public void GeneratePath_StartsAtStartAndEndsAtGoal()
    {
        Vector2Int start = new Vector2Int(0, 4);
        Vector2Int goal = new Vector2Int(10, 4);
        List<Vector2Int> path = MapGenerator.GeneratePath(12, 8, start, goal, 99);

        Assert.IsTrue(path.Count >= 2);
        Assert.AreEqual(start, path[0]);
        Assert.AreEqual(goal, path[path.Count - 1]);
    }

    [Test]
    public void GeneratePath_ProducesContiguousFourConnectedSequence()
    {
        Vector2Int start = new Vector2Int(0, 3);
        Vector2Int goal = new Vector2Int(8, 3);
        List<Vector2Int> path = MapGenerator.GeneratePath(10, 7, start, goal, 7);

        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int diff = path[i] - path[i - 1];
            int manhattan = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);
            Assert.AreEqual(1, manhattan, $"Path step {i} is not 4-connected: {path[i - 1]} -> {path[i]}");
        }
    }

    [Test]
    public void GenerateFullMap_PathDoesNotOverlapOccupants()
    {
        LevelMapDefinition definition = MapGenerator.GenerateFullMap(20, 12, 42, MapGenerator.ScatterParams.Default);

        HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>(definition.pathCells);
        pathSet.Add(definition.startCell);
        pathSet.Add(definition.goalCell);

        foreach (OccupantEntry entry in definition.occupants)
            Assert.IsFalse(pathSet.Contains(entry.cell), $"Occupant overlaps path at {entry.cell}");
    }

    [Test]
    public void ScatterBlocks_DoesNotPlaceOnPath()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 12,
            height = 8,
            startCell = new Vector2Int(0, 4),
            goalCell = new Vector2Int(11, 4),
            blockedCells = new List<Vector2Int>(),
            pathCells = new List<Vector2Int>(),
            occupants = new List<OccupantEntry>(),
            groundOverrides = new List<GroundOverrideEntry>()
        };
        for (int x = 0; x < 12; x++)
            definition.pathCells.Add(new Vector2Int(x, 4));

        MapGenerator.ScatterBlocks(definition, 555, MapGenerator.ScatterParams.Default);

        HashSet<Vector2Int> reserved = new HashSet<Vector2Int>(definition.pathCells);
        foreach (OccupantEntry entry in definition.occupants)
            Assert.IsFalse(reserved.Contains(entry.cell));
    }
}
