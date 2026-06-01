using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GridLogicEditModeTests
{
    private GameObject gridObject;
    private GridManager gridManager;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("GridManagerTest");
        gridManager = gridObject.AddComponent<GridManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (gridObject != null)
            Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void BuildGridCreatesPathCacheForOpenMap()
    {
        LevelMapDefinition definition = CreateOpenMap(5, 3, new Vector2Int(0, 1), new Vector2Int(4, 1));

        gridManager.BuildGrid(definition);

        List<Vector3> worldPath = gridManager.GetCachedEnemyPathWorld();

        Assert.IsNotNull(worldPath);
        Assert.AreEqual(5, worldPath.Count);
        Assert.AreEqual(new Vector3(0.5f, 1.5f, 0f), worldPath[0]);
        Assert.AreEqual(new Vector3(4.5f, 1.5f, 0f), worldPath[worldPath.Count - 1]);
    }

    [Test]
    public void CanBuildAtRejectsStartGoalAndReservedPathButAllowsFreeCell()
    {
        LevelMapDefinition definition = CreateOpenMap(5, 3, new Vector2Int(0, 1), new Vector2Int(4, 1));

        gridManager.BuildGrid(definition);

        Assert.IsFalse(gridManager.CanBuildAt(definition.startCell));
        Assert.IsFalse(gridManager.CanBuildAt(definition.goalCell));
        Assert.IsFalse(gridManager.CanBuildAt(new Vector2Int(2, 1)));
        Assert.IsTrue(gridManager.CanBuildAt(new Vector2Int(2, 0)));
    }

    [Test]
    public void TryOccupyCellMarksAndClearsBuildableCell()
    {
        LevelMapDefinition definition = CreateOpenMap(5, 3, new Vector2Int(0, 1), new Vector2Int(4, 1));
        Vector2Int buildCell = new Vector2Int(2, 0);

        gridManager.BuildGrid(definition);

        Assert.IsTrue(gridManager.TryOccupyCell(buildCell));
        Assert.IsTrue(gridManager.GetCell(buildCell).IsOccupied);

        gridManager.ClearOccupiedCell(buildCell);

        Assert.IsFalse(gridManager.GetCell(buildCell).IsOccupied);
    }

    [Test]
    public void BuildGridPreviewCreatesAndClearsTemporaryVisuals()
    {
        LevelMapDefinition definition = CreateOpenMap(3, 2, new Vector2Int(0, 0), new Vector2Int(2, 0));

        gridManager.BuildGridPreview(definition);

        Assert.AreEqual(6, gridObject.transform.childCount);

        gridManager.ClearPreviewVisuals();

        Assert.AreEqual(0, gridObject.transform.childCount);
    }

    private static LevelMapDefinition CreateOpenMap(int width, int height, Vector2Int startCell, Vector2Int goalCell)
    {
        return new LevelMapDefinition
        {
            width = width,
            height = height,
            startCell = startCell,
            goalCell = goalCell,
            blockedCells = new List<Vector2Int>(),
            pathCells = new List<Vector2Int>()
        };
    }
}