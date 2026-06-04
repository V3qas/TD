using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TD.Grid;
using TD.Level;
using TD.Pathfinding;

namespace TD.Tests.EditMode
{
    public class PathfinderTests
    {
        private GameObject host;
        private GridManager gridManager;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("GridManagerHost");
            gridManager = host.AddComponent<GridManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                Object.DestroyImmediate(host);
        }

        [Test]
        public void FindPath_ReturnsContinuousPathOnOpenGrid()
        {
            gridManager.BuildGrid(new LevelMapDefinition
            {
                width = 5,
                height = 5,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(4, 4)
            });

            Pathfinder pathfinder = new Pathfinder(gridManager);
            List<GridCell> path = pathfinder.FindPath(new Vector2Int(0, 0), new Vector2Int(4, 4));

            Assert.IsNotNull(path);
            Assert.Greater(path.Count, 0);
            Assert.AreEqual(new Vector2Int(0, 0), path[0].Position);
            Assert.AreEqual(new Vector2Int(4, 4), path[path.Count - 1].Position);

            for (int index = 1; index < path.Count; index++)
            {
                int manhattan = Mathf.Abs(path[index].X - path[index - 1].X)
                              + Mathf.Abs(path[index].Y - path[index - 1].Y);
                Assert.AreEqual(1, manhattan, $"Step {index} is not 4-connected.");
            }
        }

        [Test]
        public void FindPath_ReturnsNullWhenBlockersIsolateGoal()
        {
            gridManager.BuildGrid(new LevelMapDefinition
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
            });

            Pathfinder pathfinder = new Pathfinder(gridManager);
            List<GridCell> path = pathfinder.FindPath(new Vector2Int(0, 0), new Vector2Int(2, 2));

            Assert.IsNull(path);
            Assert.IsFalse(pathfinder.HasPath(new Vector2Int(0, 0), new Vector2Int(2, 2)));
        }

        [Test]
        public void FindPath_FollowsExplicitPathCellsOnly()
        {
            gridManager.BuildGrid(new LevelMapDefinition
            {
                width = 4,
                height = 3,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(3, 0),
                pathCells = new List<Vector2Int>
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(3, 1),
                    new Vector2Int(3, 0)
                }
            });

            Pathfinder pathfinder = new Pathfinder(gridManager);
            List<GridCell> path = pathfinder.FindPath(new Vector2Int(0, 0), new Vector2Int(3, 0));

            Assert.IsNotNull(path);
            for (int index = 0; index < path.Count; index++)
            {
                GridCell cell = path[index];
                bool isEndpoint = cell.Position == new Vector2Int(0, 0) || cell.Position == new Vector2Int(3, 0);
                Assert.IsTrue(isEndpoint || cell.IsPath, $"Cell {cell.Position} is not part of the explicit path.");
            }
        }

        [Test]
        public void HasPath_ReturnsFalseForOutOfBoundsEndpoints()
        {
            gridManager.BuildGrid(new LevelMapDefinition
            {
                width = 3,
                height = 3,
                startCell = new Vector2Int(0, 0),
                goalCell = new Vector2Int(2, 2)
            });

            Pathfinder pathfinder = new Pathfinder(gridManager);

            Assert.IsFalse(pathfinder.HasPath(new Vector2Int(-1, 0), new Vector2Int(2, 2)));
            Assert.IsFalse(pathfinder.HasPath(new Vector2Int(0, 0), new Vector2Int(99, 99)));
        }
    }
}
