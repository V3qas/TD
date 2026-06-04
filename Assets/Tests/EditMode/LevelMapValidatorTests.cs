using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Combat;
using TD.Level;

namespace TD.Tests.EditMode
{
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

        [Test]
        public void Validate_RejectsStartOutOfBoundsWithoutSilentlyClamping()
        {
            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = 5,
                height = 5,
                startCell = new Vector2Int(99, 99),
                goalCell = new Vector2Int(0, 0)
            };

            bool valid = LevelMapValidator.Validate(definition, false, out string message);

            Assert.IsFalse(valid, "Validator must reject OOB start instead of normalizing.");
            Assert.IsTrue(message.Contains("Start"), message);
        }

        [Test]
        public void Validate_RejectsPathSequenceThroughOccupant()
        {
            List<Vector2Int> path = new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0)
            };
            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = 4,
                height = 1,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(3, 0),
                pathSequences = new List<PathSequence> { new PathSequence(path) },
                occupants = new List<OccupantEntry>
                {
                    new OccupantEntry { cell = new Vector2Int(2, 0), type = OccupantType.Rock }
                }
            };

            bool valid = LevelMapValidator.Validate(definition, true, out string message);

            Assert.IsFalse(valid, "Validator must reject path/occupant overlap rather than dropping the occupant.");
            Assert.IsNotEmpty(message);
        }

        [Test]
        public void Validate_RejectsNonAdjacentPathSequenceStep()
        {
            List<Vector2Int> path = new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(3, 0)
            };
            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = 4,
                height = 1,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(3, 0),
                pathSequences = new List<PathSequence> { new PathSequence(path) }
            };

            bool valid = LevelMapValidator.Validate(definition, true, out string message);

            Assert.IsFalse(valid);
            Assert.IsTrue(message.Contains("non-adjacent"), message);
        }
    }
}
