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
    public void Normalize_MigratesLegacyBlockedCellsIntoOccupants_AndDropsOverlapsWithStartGoal()
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

        Assert.AreEqual(0, definition.blockedCells.Count, "blockedCells must be cleared after migration.");
        Assert.AreEqual(1, definition.occupants.Count, "Only the non-overlapping blocker should remain.");
        Assert.AreEqual(new Vector2Int(1, 1), definition.occupants[0].cell);
        Assert.AreEqual(OccupantType.Rock, definition.occupants[0].type);
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

        Assert.AreEqual(0, definition.blockedCells.Count);
        Assert.AreEqual(1, definition.occupants.Count);
        Assert.AreEqual(new Vector2Int(1, 1), definition.occupants[0].cell);
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
            occupants = new List<OccupantEntry> { new OccupantEntry { cell = new Vector2Int(1, 1), type = OccupantType.Rock } },
            pathCells = new List<Vector2Int> { new Vector2Int(2, 2) }
        };

        LevelMapDefinition clone = definition.Clone();
        clone.occupants.Add(new OccupantEntry { cell = new Vector2Int(3, 3), type = OccupantType.Rock });
        clone.pathCells.Add(new Vector2Int(3, 3));

        Assert.AreEqual(1, definition.occupants.Count);
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

        Assert.AreEqual(0, definition.blockedCells.Count, "blockedCells must always be empty after Normalize.");
        Assert.AreEqual(0, definition.occupants.Count, "Start cell blocker is dropped, OOB blocker is dropped.");
        Assert.AreEqual(LevelMapDefinition.CurrentVersion, definition.version);
    }

    [Test]
    public void GetGround_ReturnsPathForPathCellsAndStartGoal()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 5,
            height = 3,
            startCell = new Vector2Int(0, 1),
            goalCell = new Vector2Int(4, 1),
            pathCells = new List<Vector2Int> { new Vector2Int(2, 1) }
        };
        definition.Normalize();

        Assert.AreEqual(GroundType.Path, definition.GetGround(new Vector2Int(0, 1)));
        Assert.AreEqual(GroundType.Path, definition.GetGround(new Vector2Int(4, 1)));
        Assert.AreEqual(GroundType.Path, definition.GetGround(new Vector2Int(2, 1)));
        Assert.AreEqual(GroundType.Ground, definition.GetGround(new Vector2Int(2, 0)));
    }

    [Test]
    public void GetGround_ReturnsOverrideTypeForElevatedAndWater()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 4,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(3, 3),
            groundOverrides = new List<GroundOverrideEntry>
            {
                new GroundOverrideEntry { cell = new Vector2Int(1, 1), type = GroundType.Elevated },
                new GroundOverrideEntry { cell = new Vector2Int(2, 2), type = GroundType.Water }
            }
        };
        definition.Normalize();

        Assert.AreEqual(GroundType.Elevated, definition.GetGround(new Vector2Int(1, 1)));
        Assert.AreEqual(GroundType.Water, definition.GetGround(new Vector2Int(2, 2)));
    }

    [Test]
    public void Normalize_DropsGroundOverrideThatOverlapsPath()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 3,
            startCell = new Vector2Int(0, 1),
            goalCell = new Vector2Int(3, 1),
            pathCells = new List<Vector2Int> { new Vector2Int(1, 1) },
            groundOverrides = new List<GroundOverrideEntry>
            {
                new GroundOverrideEntry { cell = new Vector2Int(1, 1), type = GroundType.Water },
                new GroundOverrideEntry { cell = new Vector2Int(2, 0), type = GroundType.Lava }
            }
        };

        definition.Normalize();

        Assert.AreEqual(1, definition.groundOverrides.Count);
        Assert.AreEqual(new Vector2Int(2, 0), definition.groundOverrides[0].cell);
    }

    [Test]
    public void Normalize_DropsOccupantsOnPathOrOutOfBounds()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 3,
            startCell = new Vector2Int(0, 1),
            goalCell = new Vector2Int(3, 1),
            pathCells = new List<Vector2Int> { new Vector2Int(1, 1) },
            occupants = new List<OccupantEntry>
            {
                new OccupantEntry { cell = new Vector2Int(1, 1), type = OccupantType.Rock },
                new OccupantEntry { cell = new Vector2Int(99, 99), type = OccupantType.Destructible, maxHp = 50 },
                new OccupantEntry { cell = new Vector2Int(2, 0), type = OccupantType.Destructible, maxHp = 100, reward = 10 }
            }
        };

        definition.Normalize();

        Assert.AreEqual(1, definition.occupants.Count);
        Assert.AreEqual(new Vector2Int(2, 0), definition.occupants[0].cell);
        Assert.AreEqual(OccupantType.Destructible, definition.occupants[0].type);
    }

    [Test]
    public void IsBuildable_RespectsGroundAndOccupants()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 3,
            startCell = new Vector2Int(0, 1),
            goalCell = new Vector2Int(3, 1),
            pathCells = new List<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(2, 1) },
            groundOverrides = new List<GroundOverrideEntry>
            {
                new GroundOverrideEntry { cell = new Vector2Int(0, 2), type = GroundType.Water },
                new GroundOverrideEntry { cell = new Vector2Int(3, 2), type = GroundType.Elevated }
            },
            occupants = new List<OccupantEntry>
            {
                new OccupantEntry { cell = new Vector2Int(2, 0), type = OccupantType.Rock }
            }
        };
        definition.Normalize();

        Assert.IsFalse(definition.IsBuildable(definition.startCell), "Start is not buildable.");
        Assert.IsFalse(definition.IsBuildable(definition.goalCell), "Goal is not buildable.");
        Assert.IsFalse(definition.IsBuildable(new Vector2Int(1, 1)), "Path is not buildable.");
        Assert.IsFalse(definition.IsBuildable(new Vector2Int(0, 2)), "Water is not buildable.");
        Assert.IsTrue(definition.IsBuildable(new Vector2Int(3, 2)), "Elevated is buildable.");
        Assert.IsFalse(definition.IsBuildable(new Vector2Int(2, 0)), "Rock occupant blocks build.");
        Assert.IsTrue(definition.IsBuildable(new Vector2Int(0, 0)), "Plain ground is buildable.");
    }

    [Test]
    public void Normalize_ReconstructsSinglePathSequenceFromUnorderedPathCells()
    {
        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 1,
            startCell = new Vector2Int(0, 0),
            goalCell = new Vector2Int(3, 0),
            pathCells = new List<Vector2Int>
            {
                new Vector2Int(2, 0),
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(3, 0)
            }
        };

        definition.Normalize();

        Assert.AreEqual(1, definition.pathSequences.Count);
        Assert.AreEqual(definition.startCell, definition.pathSequences[0].cells[0]);
        Assert.AreEqual(definition.goalCell, definition.pathSequences[0].cells[definition.pathSequences[0].cells.Count - 1]);
        Assert.IsFalse(definition.HasMultiplePaths);
    }

    [Test]
    public void HasMultiplePaths_IsTrueForTwoSequences()
    {
        List<Vector2Int> top = new List<Vector2Int>
        {
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(3, 1)
        };
        List<Vector2Int> bottom = new List<Vector2Int>
        {
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0),
            new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(3, 1)
        };

        LevelMapDefinition definition = new LevelMapDefinition
        {
            width = 4,
            height = 3,
            startCell = new Vector2Int(0, 1),
            goalCell = new Vector2Int(3, 1),
            pathSequences = new List<PathSequence> { new PathSequence(top), new PathSequence(bottom) }
        };
        definition.Normalize();

        Assert.IsTrue(definition.HasMultiplePaths);
        Assert.IsTrue(definition.IsPath(new Vector2Int(2, 2)));
        Assert.IsTrue(definition.IsPath(new Vector2Int(2, 0)));
        Assert.IsTrue(definition.IsPath(new Vector2Int(1, 1)), "Junction cell shared by both paths.");
        Assert.IsTrue(LevelMapValidator.Validate(definition, true, out string message), message);
    }
}
