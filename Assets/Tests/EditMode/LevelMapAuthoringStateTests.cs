using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Level;

namespace TD.Tests.EditMode
{
    public class LevelMapAuthoringStateTests
    {
        [Test]
        public void CreateNewMap_WithStraightPath_ProducesValidExplicitPath()
        {
            LevelMapAuthoringState state = new LevelMapAuthoringState();
            state.CreateNewMap(5, 3, true);

            LevelMapDefinition definition = state.BuildDefinition();

            Assert.IsTrue(LevelMapValidator.Validate(definition, true, out string message), message);
            Assert.AreEqual(1, definition.pathSequences.Count);
            Assert.AreEqual(definition.startCell, definition.pathSequences[0].cells[0]);
            Assert.AreEqual(definition.goalCell, definition.pathSequences[0].cells[definition.pathSequences[0].cells.Count - 1]);
        }

        [Test]
        public void CreateNewMap_WithoutStraightPath_StartsWithoutExplicitPath()
        {
            LevelMapAuthoringState state = new LevelMapAuthoringState();
            state.CreateNewMap(5, 3, false);

            LevelMapDefinition definition = state.BuildDefinition();

            Assert.IsFalse(definition.HasExplicitPath);
            Assert.IsFalse(LevelMapValidator.Validate(definition, true, out _));
        }

        [Test]
        public void LoadDefinition_MigratesLegacyBlockedCellsIntoAuthoringOccupants()
        {
            LevelMapAuthoringState state = new LevelMapAuthoringState();
            LevelMapDefinition legacyDefinition = new LevelMapDefinition
            {
                width = 4,
                height = 3,
                startCell = new Vector2Int(0, 1),
                goalCell = new Vector2Int(3, 1),
                blockedCells = new List<Vector2Int> { new Vector2Int(2, 0) },
                pathCells = new List<Vector2Int>
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(3, 1)
                }
            };

            state.LoadDefinition(legacyDefinition);
            LevelMapDefinition built = state.BuildDefinition();

            Assert.AreEqual(0, built.blockedCells.Count);
            Assert.IsTrue(state.TryGetOccupant(new Vector2Int(2, 0), out OccupantEntry occupant));
            Assert.AreEqual(OccupantType.Rock, occupant.type);
            Assert.AreEqual(1, built.occupants.Count);
        }

        [Test]
        public void BuildDefinition_PreservesLoadedMultiPathSequencesUntilPathCellsChange()
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

            LevelMapAuthoringState state = new LevelMapAuthoringState();
            state.LoadDefinition(new LevelMapDefinition
            {
                width = 4,
                height = 3,
                startCell = new Vector2Int(0, 1),
                goalCell = new Vector2Int(3, 1),
                pathSequences = new List<PathSequence> { new PathSequence(top), new PathSequence(bottom) }
            });

            Assert.AreEqual(2, state.BuildDefinition().pathSequences.Count);

            state.PaintCell(new Vector2Int(0, 2), LevelMapPaintTool.Path, 100, 15);

            LevelMapDefinition edited = state.BuildDefinition();
            Assert.AreEqual(1, edited.pathSequences.Count, "Manual path edits should drop stale loaded multi-path sequences.");
            Assert.IsTrue(edited.pathCells.Contains(new Vector2Int(0, 2)));
        }
    }
}
