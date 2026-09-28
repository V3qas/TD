using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TD.Grid;
using TD.Level;

namespace TD.Tests.EditMode
{
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

            Assert.That(gridManager.BuildGrid(definition), Is.True);

            List<Vector3> worldPath = gridManager.GetCachedEnemyPathWorld();

            Assert.IsNotNull(worldPath);
            Assert.AreEqual(5, worldPath.Count);
            Assert.AreEqual(new Vector3(0.5f, 1.5f, 0f), worldPath[0]);
            Assert.AreEqual(new Vector3(4.5f, 1.5f, 0f), worldPath[worldPath.Count - 1]);
        }

        [Test]
        public void BuildGridPreservesAllAuthoredPathSequencesInOrder()
        {
            List<Vector2Int> upperPath = new List<Vector2Int>
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 2),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2),
                new Vector2Int(4, 2),
                new Vector2Int(4, 1)
            };
            List<Vector2Int> lowerPath = new List<Vector2Int>
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0),
                new Vector2Int(4, 0),
                new Vector2Int(4, 1)
            };
            LevelMapDefinition definition = new LevelMapDefinition
            {
                width = 5,
                height = 3,
                startCell = new Vector2Int(0, 1),
                goalCell = new Vector2Int(4, 1),
                pathSequences = new List<PathSequence>
                {
                    new PathSequence(upperPath),
                    new PathSequence(lowerPath)
                }
            };

            Assert.That(gridManager.BuildGrid(definition), Is.True);

            Assert.That(gridManager.CachedEnemyPathCount, Is.EqualTo(2));
            AssertWorldPathMatchesCells(gridManager.GetCachedEnemyPathWorld(0), upperPath);
            AssertWorldPathMatchesCells(gridManager.GetCachedEnemyPathWorld(1), lowerPath);
        }

        [Test]
        public void LoadMap_InvalidDefinition_DoesNotReportSuccess()
        {
            LevelLoader loader = gridObject.AddComponent<LevelLoader>();
            FieldInfo gridManagerField = typeof(LevelLoader).GetField(
                "gridManager",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(gridManagerField, Is.Not.Null);
            gridManagerField.SetValue(loader, gridManager);

            int mapLoadedEvents = 0;
            loader.OnMapLoaded += _ => mapLoadedEvents++;
            LevelMapDefinition invalidDefinition = CreateOpenMap(
                0,
                3,
                new Vector2Int(0, 1),
                new Vector2Int(2, 1));

            LogAssert.Expect(
                LogType.Error,
                $"LevelLoader: Map data is invalid (The map may be at most {LevelMapDefinition.MaxSize}x{LevelMapDefinition.MaxSize} tiles.).");

            bool loaded = loader.LoadMap(invalidDefinition);

            Assert.That(loaded, Is.False);
            Assert.That(loader.HasLoadedLevel, Is.False);
            Assert.That(loader.LoadedMapDefinition, Is.Null);
            Assert.That(mapLoadedEvents, Is.Zero);
            Assert.That(gridManager.HasGrid, Is.False);
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

        [Test]
        public void PaintingPreview_ReusesObjectsAndUpdatesCellState()
        {
            LevelMapAuthoringState state = new LevelMapAuthoringState();
            state.CreateNewMap(5, 3, true);
            gridManager.BuildGridPreview(state.BuildDefinition());
            Transform existingCell = gridObject.transform.GetChild(2);
            Vector2Int cell = new Vector2Int(2, 0);

            state.PaintCell(cell, LevelMapPaintTool.Water, 100, 15);
            gridManager.UpdatePreviewCell(state, cell);
            Assert.That(gridObject.transform.childCount, Is.EqualTo(15));
            Assert.That(gridObject.transform.GetChild(2), Is.SameAs(existingCell));
            Assert.That(gridManager.GetGroundType(cell), Is.EqualTo(GroundType.Water));
            Assert.That(gridManager.CanBuildAt(cell), Is.False);
            Assert.That(existingCell.GetComponent<SpriteRenderer>().color, Is.EqualTo(new Color(0.25f, 0.55f, 0.85f)));
        }

        [Test]
        public void MovingStart_UpdatesOldAndNewPreviewCells()
        {
            LevelMapAuthoringState state = new LevelMapAuthoringState();
            state.CreateNewMap(5, 3, true);
            gridManager.BuildGridPreview(state.BuildDefinition());
            Vector2Int oldStart = state.MapDefinition.startCell;
            Vector2Int newStart = new Vector2Int(1, 1);
            state.PaintCell(newStart, LevelMapPaintTool.Start, 100, 15);
            gridManager.UpdatePreviewCell(state, newStart);
            gridManager.UpdatePreviewCell(state, oldStart);
            Assert.That(gridManager.StartCell, Is.EqualTo(newStart));
            Assert.That(gridManager.IsPathCell(oldStart), Is.False);
            Assert.That(gridObject.transform.GetChild(5).GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white));
            Assert.That(gridObject.transform.GetChild(6).GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.green));
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

        private static void AssertWorldPathMatchesCells(List<Vector3> worldPath, List<Vector2Int> cells)
        {
            Assert.That(worldPath, Is.Not.Null);
            Assert.That(worldPath.Count, Is.EqualTo(cells.Count));
            for (int index = 0; index < cells.Count; index++)
            {
                Assert.That(
                    worldPath[index],
                    Is.EqualTo(new Vector3(cells[index].x + 0.5f, cells[index].y + 0.5f, 0f)));
            }
        }
    }
}
