using NUnit.Framework;
using UnityEngine;
using TD.Core;

namespace TD.Tests.EditMode
{
    public class GameStateTests
    {
        private GameObject gameStateObject;
        private GameState gameState;

        [SetUp]
        public void SetUp()
        {
            if (GameState.Instance != null)
                Object.DestroyImmediate(GameState.Instance.gameObject);

            gameStateObject = new GameObject("GameStateTests");
            gameState = gameStateObject.AddComponent<GameState>();
            gameState.ResetState();
        }

        [TearDown]
        public void TearDown()
        {
            if (gameStateObject != null)
                Object.DestroyImmediate(gameStateObject);
        }

        [Test]
        public void InitialState_UsesMvpDefaults()
        {
            Assert.That(gameState.CurrentRound, Is.EqualTo(1));
            Assert.That(gameState.Money, Is.EqualTo(100));
            Assert.That(gameState.Lives, Is.EqualTo(10));
            Assert.That(gameState.MaxRounds, Is.EqualTo(5));
            Assert.That(gameState.State, Is.EqualTo(MatchState.Playing));
        }

        [Test]
        public void DamageBase_ClampsLivesAndRaisesLostOnce()
        {
            int endEventCount = 0;
            gameState.OnMatchEnded += _ => endEventCount++;

            gameState.DamageBase(4);
            gameState.DamageBase(20);
            gameState.DamageBase(1);

            Assert.That(gameState.Lives, Is.Zero);
            Assert.That(gameState.State, Is.EqualTo(MatchState.Lost));
            Assert.That(endEventCount, Is.EqualTo(1));
        }

        [Test]
        public void Win_EndsMatchOnlyOnce()
        {
            int endEventCount = 0;
            gameState.OnMatchEnded += _ => endEventCount++;

            Assert.That(gameState.Win(), Is.True);
            Assert.That(gameState.Win(), Is.False);
            Assert.That(gameState.Lose(), Is.False);
            Assert.That(gameState.State, Is.EqualTo(MatchState.Won));
            Assert.That(endEventCount, Is.EqualTo(1));
        }

        [Test]
        public void TransactionsAndRoundChanges_AreRejectedAfterMatchEnd()
        {
            gameState.Win();

            gameState.AddMoney(50);
            bool spent = gameState.TrySpendMoney(25);
            gameState.SetRound(3);

            Assert.That(gameState.Money, Is.EqualTo(100));
            Assert.That(spent, Is.False);
            Assert.That(gameState.CurrentRound, Is.EqualTo(1));
        }

        [Test]
        public void ResetState_RestoresCompleteStartingState()
        {
            gameState.TrySpendMoney(50);
            gameState.SetRound(4);
            gameState.DamageBase(7);
            gameState.Win();

            gameState.ResetState();

            Assert.That(gameState.CurrentRound, Is.EqualTo(1));
            Assert.That(gameState.Money, Is.EqualTo(100));
            Assert.That(gameState.Lives, Is.EqualTo(10));
            Assert.That(gameState.State, Is.EqualTo(MatchState.Playing));
        }

        [Test]
        public void SetRound_ClampsToConfiguredMaximum()
        {
            gameState.SetRound(99);

            Assert.That(gameState.CurrentRound, Is.EqualTo(5));
        }
    }
}
