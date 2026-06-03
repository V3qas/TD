using NUnit.Framework;

public class DifficultyTests
{
    [Test]
    public void ForLevel_Easy_ReturnsLowerHealthAndHigherReward()
    {
        DifficultySettings settings = DifficultySettings.ForLevel(DifficultyLevel.Easy);

        Assert.AreEqual(DifficultyLevel.Easy, settings.level);
        Assert.Less(settings.healthMultiplier, 1f);
        Assert.Greater(settings.rewardMultiplier, 1f);
        Assert.Less(settings.amountMultiplier, 1f);
    }

    [Test]
    public void ForLevel_Normal_ReturnsAllNeutralMultipliers()
    {
        DifficultySettings settings = DifficultySettings.ForLevel(DifficultyLevel.Normal);

        Assert.AreEqual(DifficultyLevel.Normal, settings.level);
        Assert.AreEqual(1f, settings.healthMultiplier);
        Assert.AreEqual(1f, settings.speedMultiplier);
        Assert.AreEqual(1f, settings.rewardMultiplier);
        Assert.AreEqual(1f, settings.amountMultiplier);
        Assert.AreEqual(1f, settings.amountScaleMultiplier);
    }

    [Test]
    public void ForLevel_Hard_ScalesEnemyDifficultyUp()
    {
        DifficultySettings settings = DifficultySettings.ForLevel(DifficultyLevel.Hard);

        Assert.AreEqual(DifficultyLevel.Hard, settings.level);
        Assert.Greater(settings.healthMultiplier, 1f);
        Assert.Greater(settings.speedMultiplier, 1f);
        Assert.Less(settings.rewardMultiplier, 1f);
        Assert.Greater(settings.amountMultiplier, 1f);
    }

    [Test]
    public void ForLevel_Nightmare_IsHarderThanHard()
    {
        DifficultySettings hard = DifficultySettings.ForLevel(DifficultyLevel.Hard);
        DifficultySettings nightmare = DifficultySettings.ForLevel(DifficultyLevel.Nightmare);

        Assert.Greater(nightmare.healthMultiplier, hard.healthMultiplier);
        Assert.Greater(nightmare.speedMultiplier, hard.speedMultiplier);
        Assert.Greater(nightmare.amountMultiplier, hard.amountMultiplier);
        Assert.Less(nightmare.rewardMultiplier, hard.rewardMultiplier);
    }

    [Test]
    public void ForLevel_ReturnsFreshInstanceEachCall()
    {
        DifficultySettings first = DifficultySettings.ForLevel(DifficultyLevel.Hard);
        DifficultySettings second = DifficultySettings.ForLevel(DifficultyLevel.Hard);

        Assert.AreNotSame(first, second);
    }
}
