using System.Collections.Generic;
using UnityEngine;
using TD.Core;

namespace TD.Enemies
{
    /// <summary>
    /// Pure round-composition logic for enemy spawning. Decides which enemies
    /// and how many spawn in a given round, applying difficulty scaling. Kept
    /// free of MonoBehaviour/scene state so it can be unit tested in isolation.
    /// </summary>
    public static class WavePlanner
    {
        /// <summary>
        /// Fills <paramref name="output"/> with the ordered spawn entries for the
        /// given round. Falls back to <paramref name="fallback"/> when no
        /// configured entry applies.
        /// </summary>
        public static void BuildRound(
            Queue<EnemySpawnEntry> output,
            IReadOnlyList<EnemySpawnEntry> spawnEntries,
            int round,
            DifficultySettings difficulty,
            EnemySpawnEntry fallback)
        {
            output.Clear();

            if (spawnEntries != null)
            {
                for (int e = 0; e < spawnEntries.Count; e++)
                {
                    EnemySpawnEntry entry = spawnEntries[e];
                    if (!IsValid(entry) || round < entry.firstRound)
                        continue;

                    int amount = GetSpawnAmount(entry, round, difficulty);
                    for (int i = 0; i < amount; i++)
                        output.Enqueue(entry);
                }
            }

            if (output.Count == 0 && IsValid(fallback))
            {
                int amount = GetSpawnAmount(fallback, round, difficulty);
                for (int i = 0; i < amount; i++)
                    output.Enqueue(fallback);
            }
        }

        /// <summary>True when the entry has both data and a prefab assigned.</summary>
        public static bool IsValid(EnemySpawnEntry entry)
        {
            return entry != null && entry.enemyData != null && entry.enemyPrefab != null;
        }

        private static int GetSpawnAmount(EnemySpawnEntry entry, int round, DifficultySettings difficulty)
        {
            int scaledBase = Mathf.Max(1, Mathf.RoundToInt(entry.baseAmount * difficulty.amountMultiplier));
            int scaledPerRound = Mathf.RoundToInt(entry.amountPerRound * difficulty.amountScaleMultiplier);
            return scaledBase + ((round - entry.firstRound) * scaledPerRound);
        }
    }
}
