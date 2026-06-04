using System;

namespace TD.Core
{
    public enum DifficultyLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Nightmare = 3
    }

    [Serializable]
    public class DifficultySettings
    {
        public DifficultyLevel level = DifficultyLevel.Normal;

        public float healthMultiplier     = 1f;
        public float speedMultiplier      = 1f;
        public float rewardMultiplier     = 1f;
        public float amountMultiplier     = 1f;
        public float amountScaleMultiplier = 1f;

        public static DifficultySettings ForLevel(DifficultyLevel difficultyLevel)
        {
            switch (difficultyLevel)
            {
                case DifficultyLevel.Easy:
                    return new DifficultySettings
                    {
                        level                = DifficultyLevel.Easy,
                        healthMultiplier     = 0.7f,
                        speedMultiplier      = 0.85f,
                        rewardMultiplier     = 1.4f,
                        amountMultiplier     = 0.7f,
                        amountScaleMultiplier = 0.7f
                    };

                case DifficultyLevel.Hard:
                    return new DifficultySettings
                    {
                        level                = DifficultyLevel.Hard,
                        healthMultiplier     = 1.4f,
                        speedMultiplier      = 1.15f,
                        rewardMultiplier     = 0.8f,
                        amountMultiplier     = 1.4f,
                        amountScaleMultiplier = 1.3f
                    };

                case DifficultyLevel.Nightmare:
                    return new DifficultySettings
                    {
                        level                = DifficultyLevel.Nightmare,
                        healthMultiplier     = 2.0f,
                        speedMultiplier      = 1.35f,
                        rewardMultiplier     = 0.6f,
                        amountMultiplier     = 2.0f,
                        amountScaleMultiplier = 1.7f
                    };

                default: // Normal
                    return new DifficultySettings { level = DifficultyLevel.Normal };
            }
        }
    }
}
