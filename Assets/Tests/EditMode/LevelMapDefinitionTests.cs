using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class LevelMapDefinitionTests
{
    [Test]
    public void Normalize_ClampsSizeWithinAllowedRange()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = -10,
            height = LevelMapDefinition.MaxSize + 50
        };

        definition.Normalize();

        Assert.AreEqual(1, definition.width);
        Assert.AreEqual(LevelMapDefinition.MaxSize, definition.height);
    }

    [Test]
    public void Normalize_ClampsStartAndGoalIntoBounds()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 5,
            height = 5,
            startCell = new Vector2Int(-5, -5),
            goalCell = new Vector2Int(99, 99)
        };

        definition.Normalize();

        Assert.AreEqual(new Vector2Int(0, 0), definition.startCell);
        Assert.AreEqual(new Vector2Int(4, 4), definition.goalCell);
    }

    [Test]
    public void Normalize_DropsBlockedCellsThatOverlapStartOrGoal()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 4,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(3, 3),
            blockedCells = new List<Vector2Int>
            {
                new Vector2Int(0, 0),
                new Vector2Int(3, 3),
                new Vector2Int(1, 1)
            }
        };

        definition.Normalize();

        Assert.AreEqual(1, definition.blockedCells.Count);
        Assert.AreEqual(new Vector2Int(1, 1), definition.blockedCells[0]);
    }

    [Test]
    public void Normalize_DeduplicatesCellsAndDropsOutOfBounds()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 3,
            height = 3,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(2, 2),
            blockedCells = new List<Vector2Int>
            {
                new Vector2Int(1, 1),
                new Vector2Int(1, 1),
                new Vector2Int(99, 0)
            }
        };

        definition.Normalize();

        Assert.AreEqual(1, definition.blockedCells.Count);
    }

    [Test]
    public void HasExplicitPath_IsTrueOnlyWhenPathCellsExist()
    {
        LevelMapDefinition empty = new LevelMapDefinition();
        Assert.IsFalse(empty.HasExplicitPath);

        empty.pathCells.Add(new Vector2Int(1, 1));
        Assert.IsTrue(empty.HasExplicitPath);
    }

    [Test]
    public void Clone_ProducesIndependentCopy()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 5,
            height = 5,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(4, 4),
            blockedCells = new List<Vector2Int> { new Vector2Int(1, 1) },
            pathCells = new List<Vector2Int> { new Vector2Int(2, 2) }
        };

        LevelMapDefinition clone = definition.Clone();
        clone.blockedCells.Add(new Vector2Int(3, 3));
        clone.pathCells.Add(new Vector2Int(3, 3));

        Assert.AreEqual(1, definition.blockedCells.Count);
        Assert.AreEqual(1, definition.pathCells.Count);
    }

    [Test]
    public void IsInBounds_ChecksAllAxes()
    {
        LevelMapDefinition definition = new LevelMapDefinition { width = 3, height = 3 };

        Assert.IsTrue(definition.IsInBounds(new Vector2Int(0, 0)));
        Assert.IsTrue(definition.IsInBounds(new Vector2Int(2, 2)));
        Assert.IsFalse(definition.IsInBounds(new Vector2Int(-1, 0)));
        Assert.IsFalse(definition.IsInBounds(new Vector2Int(3, 0)));
        Assert.IsFalse(definition.IsInBounds(new Vector2Int(0, 3)));
    }

    [Test]
    public void FromLegacy_AppliesNormalization()
    {
        LevelMapDefinition definition = LevelMapDefinition.FromLegacy(
            legacyWidth: 3,
            legacyHeight: 3,
            legacyStartCell: new Vector2Int(0, 0),
            legacyGoalCell: new Vector2Int(2, 2),
            legacyBlockedCells: new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(99, 99) },
            legacyPathCells: null);

        Assert.AreEqual(0, definition.blockedCells.Count, "Start cell and out-of-bounds should be removed.");
        Assert.AreEqual(LevelMapDefinition.CurrentVersion, definition.version);
    }
}
