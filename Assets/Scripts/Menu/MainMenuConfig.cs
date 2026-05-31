using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MainMenuConfig", menuName = "TowerDefense/Menu/Main Menu Config")]
public class MainMenuConfig : ScriptableObject
{
    [Header("Main Menu")]
    public string title = "Tower Defense";

    public List<MainMenuButtonConfig> mainButtons = new List<MainMenuButtonConfig>
    {
        new MainMenuButtonConfig("Single Campaign", MainMenuAction.SingleCampaign),
        new MainMenuButtonConfig("Infinite", MainMenuAction.Infinite),
        new MainMenuButtonConfig("Challenge", MainMenuAction.Challenge),
        new MainMenuButtonConfig("Tower Upgrade", MainMenuAction.TowerUpgrade),
        new MainMenuButtonConfig("Map Editor", MainMenuAction.MapEditor),
        new MainMenuButtonConfig("Custom Maps", MainMenuAction.CustomMaps),
        new MainMenuButtonConfig("Options", MainMenuAction.Options)
    };

    [Header("Campaign")]
    public string campaignTitle = "Single Campaign";
    public List<CampaignLevelConfig> campaignLevels = new List<CampaignLevelConfig>
    {
        new CampaignLevelConfig("Level 1")
    };
}

public enum MainMenuAction
{
    SingleCampaign,
    Infinite,
    Challenge,
    TowerUpgrade,
    Options,
    MapEditor,
    CustomMaps
}

[Serializable]
public class MainMenuButtonConfig
{
    public string label;
    public MainMenuAction action;
    public bool isEnabled = true;

    public MainMenuButtonConfig()
    {
    }

    public MainMenuButtonConfig(string label, MainMenuAction action)
    {
        this.label = label;
        this.action = action;
        isEnabled = true;
    }
}

[Serializable]
public class CampaignLevelConfig
{
    public string label;
    public LevelData levelData;
    [TextArea(3, 8)] public string mapSeed;
    public bool isUnlocked = true;

    public CampaignLevelConfig()
    {
    }

    public CampaignLevelConfig(string label)
    {
        this.label = label;
        isUnlocked = true;
    }
}
