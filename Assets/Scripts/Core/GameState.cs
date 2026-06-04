using System;
using UnityEngine;

namespace TD.Core
{
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [SerializeField] private int startingRound = 1;
        [SerializeField] private int startingMoney = 100;

        public event Action<int> OnRoundChanged;
        public event Action<int> OnMoneyChanged;

        public int CurrentRound { get; private set; }
        public int Money { get; private set; }

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
            CurrentRound = Mathf.Max(1, round);
            Money = Mathf.Max(0, startingMoney);
            OnRoundChanged?.Invoke(CurrentRound);
            OnMoneyChanged?.Invoke(Money);
        }

        public void SetRound(int round)
        {
            CurrentRound = Mathf.Max(1, round);
            OnRoundChanged?.Invoke(CurrentRound);
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0)
                return;

            Money += amount;
            OnMoneyChanged?.Invoke(Money);
        }

        public bool TrySpendMoney(int amount)
        {
            if (amount <= 0)
                return true;

            if (Money < amount)
                return false;

            Money -= amount;
            OnMoneyChanged?.Invoke(Money);
            return true;
        }
    }
}
