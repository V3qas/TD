using NUnit.Framework;
using UnityEngine;
using TD.Core;
using TD.Level;

namespace TD.Tests.EditMode
{
    public class GameSessionTests
    {
        [TearDown]
        public void TearDown()
        {
            GameSession.EndTestRun();
            GameSession.EndMapEditorMode();
            GameSession.ClearSelectedLevel();
        }

        [Test]
        public void SelectLevel_ClearsMapEditorAndTestState()
        {
            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            try
            {
                GameSession.BeginMapEditorMode();
                GameSession.BeginTestRun(DifficultyLevel.Hard);

                GameSession.SelectLevel(level);

                Assert.That(GameSession.SelectedLevelData, Is.SameAs(level));
                Assert.That(GameSession.SelectedMapDefinition, Is.Null);
                Assert.That(GameSession.SelectedMapSeed, Is.Empty);
                Assert.That(GameSession.IsMapEditorSession, Is.False);
                Assert.That(GameSession.IsEditorTestRun, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void SelectMapDefinition_ClearsMapEditorAndTestState()
        {
            GameSession.BeginMapEditorMode();
            GameSession.BeginTestRun(DifficultyLevel.Nightmare);
            var definition = new LevelMapDefinition
            {
                width = 2,
                height = 1,
                startCell = Vector2Int.zero,
                goalCell = Vector2Int.right
            };

            GameSession.SelectMapDefinition(definition);

            Assert.That(GameSession.SelectedLevelData, Is.Null);
            Assert.That(GameSession.SelectedMapDefinition, Is.Not.Null);
            Assert.That(GameSession.SelectedMapSeed, Is.Not.Empty);
            Assert.That(GameSession.IsMapEditorSession, Is.False);
            Assert.That(GameSession.IsEditorTestRun, Is.False);
        }

        [Test]
        public void BeginMapEditorMode_ClearsPreviousGameplaySelection()
        {
            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            try
            {
                GameSession.SelectLevel(level);
                GameSession.BeginTestRun(DifficultyLevel.Hard);

                GameSession.BeginMapEditorMode();

                Assert.That(GameSession.SelectedLevelData, Is.Null);
                Assert.That(GameSession.SelectedMapDefinition, Is.Null);
                Assert.That(GameSession.SelectedMapSeed, Is.Empty);
                Assert.That(GameSession.IsMapEditorSession, Is.True);
                Assert.That(GameSession.IsEditorTestRun, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }
    }
}
