using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private MainMenuConfig config;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Font font;
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private bool showOnStart = true;

    private GameObject menuRoot;
    private GameObject mainPanel;
    private GameObject campaignPanel;
    private GameObject customMapsPanel;
    private GameObject placeholderPanel;
    private Text placeholderTitle;
    private Text placeholderBody;

    private Font RuntimeFont
    {
        get
        {
            if (font != null)
                return font;

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return legacyFont != null ? legacyFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    private void Start()
    {
        EnsureCanvas();
        EnsureEventSystem();
        BuildMenu();

        if (showOnStart)
            ShowMainMenu();
        else
            HideMenu();
    }

    public void ShowMainMenu()
    {
        if (menuRoot == null)
            return;

        menuRoot.SetActive(true);
        mainPanel.SetActive(true);
        campaignPanel.SetActive(false);
        customMapsPanel.SetActive(false);
        placeholderPanel.SetActive(false);
    }

    public void HideMenu()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    private void EnsureCanvas()
    {
        if (targetCanvas != null)
            return;

        targetCanvas = FindAnyObjectByType<Canvas>();

        if (targetCanvas != null)
            return;

        GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        targetCanvas = canvasObject.GetComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private void BuildMenu()
    {
        if (menuRoot != null)
            Destroy(menuRoot);

        menuRoot = CreateFullScreenRoot();
        GameObject panelFrame = CreatePanelFrame(menuRoot.transform);

        CreateTitle(panelFrame.transform, GetTitle());

        mainPanel = CreateVerticalPanel("MainPanel", panelFrame.transform);
        campaignPanel = CreateVerticalPanel("CampaignPanel", panelFrame.transform);
        customMapsPanel = CreateVerticalPanel("CustomMapsPanel", panelFrame.transform);
        placeholderPanel = CreateVerticalPanel("PlaceholderPanel", panelFrame.transform);

        BuildMainPanel();
        BuildCampaignPanel();
        BuildPlaceholderPanel();
    }

    private GameObject CreateFullScreenRoot()
    {
        GameObject root = new GameObject("MainMenu", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(targetCanvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = root.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);

        return root;
    }

    private GameObject CreatePanelFrame(Transform parent)
    {
        GameObject frame = new GameObject("MenuFrame", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        frame.transform.SetParent(parent, false);

        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 560f);

        Image image = frame.GetComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);

        VerticalLayoutGroup layout = frame.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return frame;
    }

    private GameObject CreateVerticalPanel(string panelName, Transform parent)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(parent, false);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return panel;
    }

    private void CreateTitle(Transform parent, string title)
    {
        Text titleText = CreateText("Title", parent, title, 34, TextAnchor.MiddleCenter, Color.white);
        LayoutElement layoutElement = titleText.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 64f;
    }

    private void BuildMainPanel()
    {
        List<MainMenuButtonConfig> buttons = GetMainButtons();

        foreach (MainMenuButtonConfig buttonConfig in buttons)
        {
            MainMenuButtonConfig capturedConfig = buttonConfig;
            CreateButton(mainPanel.transform, buttonConfig.label, () => HandleMainMenuAction(capturedConfig), buttonConfig.isEnabled);
        }
    }

    private void BuildCampaignPanel()
    {
        ClearPanel(campaignPanel.transform);

        CreateText("CampaignTitle", campaignPanel.transform, GetCampaignTitle(), 24, TextAnchor.MiddleCenter, Color.white)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

        List<CampaignLevelConfig> levels = GetCampaignLevels();

        if (levels.Count == 0)
        {
            CreateText("NoLevelsText", campaignPanel.transform, "Kein Level konfiguriert.", 18, TextAnchor.MiddleCenter, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
        }
        else
        {
            foreach (CampaignLevelConfig levelConfig in levels)
            {
                CampaignLevelConfig capturedLevel = levelConfig;
                CreateButton(campaignPanel.transform, levelConfig.label, () => StartCampaignLevel(capturedLevel), levelConfig.isUnlocked);
            }
        }

        CreateButton(campaignPanel.transform, "Back", ShowMainMenu, true);
    }

    private void BuildPlaceholderPanel()
    {
        placeholderTitle = CreateText("PlaceholderTitle", placeholderPanel.transform, string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
        placeholderTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

        placeholderBody = CreateText("PlaceholderBody", placeholderPanel.transform, string.Empty, 17, TextAnchor.MiddleCenter, new Color(0.82f, 0.84f, 0.88f));
        placeholderBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;

        CreateButton(placeholderPanel.transform, "Back", ShowMainMenu, true);
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text textComponent = textObject.GetComponent<Text>();
        textComponent.text = text;
        textComponent.font = RuntimeFont;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;

        return textComponent;
    }

    private Button CreateButton(Transform parent, string label, UnityAction onClick, bool interactable)
    {
        GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 48f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = interactable ? new Color(0.18f, 0.32f, 0.52f, 1f) : new Color(0.18f, 0.18f, 0.2f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.interactable = interactable;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.24f, 0.44f, 0.68f, 1f);
        colors.pressedColor = new Color(0.12f, 0.24f, 0.38f, 1f);
        colors.disabledColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        button.colors = colors;

        Text labelText = CreateText("Label", buttonObject.transform, label, 18, TextAnchor.MiddleCenter, Color.white);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private void HandleMainMenuAction(MainMenuButtonConfig buttonConfig)
    {
        switch (buttonConfig.action)
        {
            case MainMenuAction.SingleCampaign:
                ShowCampaignMenu();
                break;
            case MainMenuAction.MapEditor:
                OpenMapEditor();
                break;
            case MainMenuAction.CustomMaps:
                ShowCustomMapsMenu();
                break;
            case MainMenuAction.Infinite:
            case MainMenuAction.Challenge:
            case MainMenuAction.TowerUpgrade:
            case MainMenuAction.Options:
                ShowPlaceholder(buttonConfig.label);
                break;
        }
    }

    private void ShowCampaignMenu()
    {
        BuildCampaignPanel();
        mainPanel.SetActive(false);
        campaignPanel.SetActive(true);
        customMapsPanel.SetActive(false);
        placeholderPanel.SetActive(false);
    }

    private void ShowCustomMapsMenu()
    {
        BuildCustomMapsPanel();
        mainPanel.SetActive(false);
        campaignPanel.SetActive(false);
        customMapsPanel.SetActive(true);
        placeholderPanel.SetActive(false);
    }

    private void ShowPlaceholder(string title)
    {
        placeholderTitle.text = title;
        placeholderBody.text = "Dieser Bereich ist als Menuepunkt vorbereitet und kann spaeter gefuellt werden.";

        mainPanel.SetActive(false);
        campaignPanel.SetActive(false);
        customMapsPanel.SetActive(false);
        placeholderPanel.SetActive(true);
    }

    private void StartCampaignLevel(CampaignLevelConfig levelConfig)
    {
        if (!string.IsNullOrWhiteSpace(levelConfig.mapSeed))
        {
            StartMapSeed(levelConfig.mapSeed, levelConfig.label);
            return;
        }

        LevelData selectedLevel = levelConfig.levelData;
        if (selectedLevel == null)
        {
            Debug.LogError("MainMenuController: Kein LevelData fuer diesen Level-Button zugewiesen.");
            return;
        }

        GameSession.SelectLevel(selectedLevel);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void StartCustomMap(CustomMapEntry customMap)
    {
        if (customMap == null || string.IsNullOrWhiteSpace(customMap.seed))
            return;

        StartMapSeed(customMap.seed, customMap.label);
    }

    private void StartMapSeed(string mapSeed, string label)
    {
        if (!GameSession.SelectMapSeed(mapSeed, out string error))
        {
            Debug.LogError($"MainMenuController: Map-Seed '{label}' ist ungueltig ({error}).");
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OpenMapEditor()
    {
        GameSession.BeginMapEditorMode();
        SceneManager.LoadScene(gameplaySceneName);
    }

    private string GetTitle()
    {
        return config != null && !string.IsNullOrWhiteSpace(config.title) ? config.title : "Tower Defense";
    }

    private string GetCampaignTitle()
    {
        return config != null && !string.IsNullOrWhiteSpace(config.campaignTitle) ? config.campaignTitle : "Single Campaign";
    }

    private List<MainMenuButtonConfig> GetMainButtons()
    {
        if (config != null && config.mainButtons != null && config.mainButtons.Count > 0)
            return config.mainButtons;

        return new List<MainMenuButtonConfig>
        {
            new MainMenuButtonConfig("Single Campaign", MainMenuAction.SingleCampaign),
            new MainMenuButtonConfig("Infinite", MainMenuAction.Infinite),
            new MainMenuButtonConfig("Challenge", MainMenuAction.Challenge),
            new MainMenuButtonConfig("Tower Upgrade", MainMenuAction.TowerUpgrade),
            new MainMenuButtonConfig("Map Editor", MainMenuAction.MapEditor),
            new MainMenuButtonConfig("Custom Maps", MainMenuAction.CustomMaps),
            new MainMenuButtonConfig("Options", MainMenuAction.Options)
        };
    }

    private List<CampaignLevelConfig> GetCampaignLevels()
    {
        if (config != null && config.campaignLevels != null && config.campaignLevels.Count > 0)
            return config.campaignLevels;

        return new List<CampaignLevelConfig>();
    }

    private void BuildCustomMapsPanel()
    {
        ClearPanel(customMapsPanel.transform);

        CreateText("CustomMapsTitle", customMapsPanel.transform, "Custom Maps", 24, TextAnchor.MiddleCenter, Color.white)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

        List<CustomMapEntry> customMaps = CustomMapStorage.GetAll();
        if (customMaps.Count == 0)
        {
            CreateText("NoCustomMapsText", customMapsPanel.transform, "Keine Custom Maps gespeichert.", 18, TextAnchor.MiddleCenter, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
        }
        else
        {
            foreach (CustomMapEntry customMap in customMaps)
            {
                CustomMapEntry capturedMap = customMap;
                string label = string.IsNullOrWhiteSpace(customMap.label) ? "Custom Map" : customMap.label;
                CreateButton(customMapsPanel.transform, label, () => StartCustomMap(capturedMap), true);
            }
        }

        CreateButton(customMapsPanel.transform, "Back", ShowMainMenu, true);
    }

    private void ClearPanel(Transform panelTransform)
    {
        for (int i = panelTransform.childCount - 1; i >= 0; i--)
        {
            GameObject child = panelTransform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
