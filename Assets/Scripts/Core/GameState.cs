using System;
using UnityEngine;

namespace TD.Core
{
    public enum MatchState
    {
        Playing,
        Won,
        Lost
    }

    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [SerializeField] private int startingRound = 1;
        [SerializeField] private int startingMoney = 100;
        [SerializeField, Min(1)] private int startingLives = 10;
        [SerializeField, Min(1)] private int maxRounds = 5;

        public event Action<int> OnRoundChanged;
        public event Action<int> OnMoneyChanged;
        public event Action<int> OnLivesChanged;
        public event Action<MatchState> OnMatchEnded;

        public int CurrentRound { get; private set; }
        public int Money { get; private set; }
        public int Lives { get; private set; }
        public int StartingLives => Mathf.Max(1, startingLives);
        public int MaxRounds => Mathf.Max(1, maxRounds);
        public MatchState State { get; private set; }
        public bool IsPlaying => State == MatchState.Playing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResetState();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static GameState GetOrCreate()
        {
            if (Instance != null)
                return Instance;

            GameState existing = FindAnyObjectByType<GameState>();
            if (existing != null)
                return existing;

            GameObject gameStateObject = new GameObject("GameState");
            return gameStateObject.AddComponent<GameState>();
        }

        public void ResetState()
        {
            ResetState(startingRound);
        }

        public void ResetState(int round)
        {
            CurrentRound = Mathf.Clamp(round, 1, MaxRounds);
            Money = Mathf.Max(0, startingMoney);
            Lives = StartingLives;
            State = MatchState.Playing;
            OnRoundChanged?.Invoke(CurrentRound);
            OnMoneyChanged?.Invoke(Money);
            OnLivesChanged?.Invoke(Lives);
        }

        public void SetRound(int round)
        {
            if (!IsPlaying)
                return;

            CurrentRound = Mathf.Clamp(round, 1, MaxRounds);
            OnRoundChanged?.Invoke(CurrentRound);
        }

        public void AddMoney(int amount)
        {
            if (!IsPlaying || amount <= 0)
                return;

            Money += amount;
            OnMoneyChanged?.Invoke(Money);
        }

        public bool TrySpendMoney(int amount)
        {
            if (!IsPlaying)
                return false;

            if (amount <= 0)
                return true;

            if (Money < amount)
                return false;

            Money -= amount;
            OnMoneyChanged?.Invoke(Money);
            return true;
        }

        public void DamageBase(int amount)
        {
            if (!IsPlaying || amount <= 0)
                return;

            Lives = Mathf.Max(0, Lives - amount);
            OnLivesChanged?.Invoke(Lives);

            if (Lives == 0)
                Lose();
        }

        public bool Win()
        {
            return EndMatch(MatchState.Won);
        }

        public bool Lose()
        {
            return EndMatch(MatchState.Lost);
        }

        private bool EndMatch(MatchState result)
        {
            if (!IsPlaying || result == MatchState.Playing)
                return false;

            State = result;
            OnMatchEnded?.Invoke(State);
            return true;
        }
    }
}
