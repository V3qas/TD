using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class LevelMapValidatorTests
{
    [Test]
    public void Validate_RejectsNullDefinition()
    {
        bool valid = LevelMapValidator.Validate(null, false, out string message);

        Assert.IsFalse(valid);
        Assert.IsNotEmpty(message);
    }

    [Test]
    public void Validate_RejectsIdenticalStartAndGoal()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 4,
            startCell = new Vector2Int(1, 1),
            goalCell = new Vector2Int(1, 1)
        };

        bool valid = LevelMapValidator.Validate(definition, false, out string message);

        Assert.IsFalse(valid);
        Assert.IsNotEmpty(message);
    }

    [Test]
    public void Validate_AcceptsOpenGridWithReachableGoal()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 4,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(3, 3)
        };

        bool valid = LevelMapValidator.Validate(definition, false, out string message);

        Assert.IsTrue(valid, message);
    }

    [Test]
    public void Validate_RejectsOpenGridWhenBlockersIsolateGoal()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 3,
            height = 3,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(2, 2),
            blockedCells = new List<Vector2Int>
            {
                new Vector2Int(2, 0),
                new Vector2Int(2, 1),
                new Vector2Int(1, 2)
            }
        };

        bool valid = LevelMapValidator.Validate(definition, false, out string message);

        Assert.IsFalse(valid);
        Assert.IsNotEmpty(message);
    }

    [Test]
    public void Validate_RequiresExplicitPathWhenFlagged()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 4,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(3, 3)
        };

        bool valid = LevelMapValidator.Validate(definition, true, out string message);

        Assert.IsFalse(valid);
        Assert.IsNotEmpty(message);
    }

    [Test]
    public void Validate_AcceptsConnectedExplicitPath()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 1,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(3, 0),
            pathCells = new List<Vector2Int>
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0)
            }
        };

        bool valid = LevelMapValidator.Validate(definition, true, out string message);

        Assert.IsTrue(valid, message);
    }

    [Test]
    public void Validate_RejectsDisconnectedExplicitPath()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 1,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(3, 0),
            pathCells = new List<Vector2Int>
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(3, 0)
            }
        };

        bool valid = LevelMapValidator.Validate(definition, true, out string message);

        Assert.IsFalse(valid);
        Assert.IsNotEmpty(message);
    }
}
