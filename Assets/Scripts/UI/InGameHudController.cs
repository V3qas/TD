using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameHudController : MonoBehaviour
{
    public const float PanelWidth = 280f;

    [SerializeField] private GameState gameState;
    [SerializeField] private BuildManager buildManager;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Font font;
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private string menuSceneName = "Menu";

    private class TowerBuildButton
    {
        public TowerData towerData;
        public Button button;
        public Text labelText;
    }

    private readonly List<TowerBuildButton> towerBuildButtons = new List<TowerBuildButton>();

    private GameObject hudRoot;
    private GameObject towerListPanel;
    private Text roundText;
    private Text moneyText;
    private Text contextTitleText;
    private Text contextBodyText;
    private Image towerIconImage;
    private Button upgradeButton;
    private Text upgradeButtonText;
    private Button sellButton;
    private Text sellButtonText;
    private Button backToEditorButton;
    private Button menuButton;
    private Tower currentSelectedTower;
    private TowerSelectionController towerSelectionController;

    public event System.Action OnBackToEditorRequested;

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
        if (gameState == null)
            gameState = GameState.GetOrCreate();

        if (buildManager == null)
            buildManager = FindAnyObjectByType<BuildManager>();

        EnsureCanvas();
        EnsureEventSystem();
        BuildHud();
        EnsureTowerSelectionController();
        SubscribeToGameState();
        RefreshAll();

        hudRoot.SetActive(showOnStart && (!GameSession.IsMapEditorSession || GameSession.IsEditorTestRun));
        RefreshTopButtons();
    }

    private void OnDestroy()
    {
        if (gameState == null)
            return;

        gameState.OnRoundChanged -= UpdateRoundText;
        gameState.OnMoneyChanged -= UpdateMoneyText;

        if (buildManager != null)
            buildManager.OnBuildSelectionChanged -= HandleBuildSelectionChanged;
    }

    public void Show()
    {
        if (hudRoot != null)
            hudRoot.SetActive(true);

        RefreshTopButtons();
    }

    public void Hide()
    {
        if (hudRoot != null)
            hudRoot.SetActive(false);
    }

    public void ShowTower(Tower tower)
    {
        if (tower == null || tower.Data == null)
        {
            ClearContext();
            return;
        }

        currentSelectedTower = tower;
        TowerData data = tower.Data;
        contextTitleText.text = string.IsNullOrWhiteSpace(data.towerName) ? "Tower" : data.towerName;
        contextBodyText.text =
            $"Level: {tower.CurrentUpgradeLevel + 1}\n" +
            $"Schaden: {tower.Damage:0.#}\n" +
            $"Tempo: {tower.AttackSpeed:0.##}/s\n" +
            $"Reichweite: {tower.Range:0.#}\n" +
            $"Naechstes Upgrade: {GetUpgradeText(tower)}";

        towerIconImage.sprite = data.icon;
        towerIconImage.enabled = data.icon != null;

        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(true);
            if (sellButtonText != null)
                sellButtonText.text = $"Verkaufen ({tower.GetSellValue()} Gold)";
        }

        RefreshUpgradeButton();

        if (towerListPanel != null)
            towerListPanel.SetActive(false);
    }

    public void ClearContext()
    {
        currentSelectedTower = null;

        if (sellButton != null)
            sellButton.gameObject.SetActive(false);

        if (upgradeButton != null)
            upgradeButton.gameObject.SetActive(false);

        contextTitleText.text = "Turmauswahl";
        contextBodyText.text = "Waehle einen Turm aus der Liste, um ihn im Ghostmode zu platzieren.";
        towerIconImage.sprite = null;
        towerIconImage.enabled = false;

        if (towerListPanel != null)
            towerListPanel.SetActive(true);
    }

    private void SubscribeToGameState()
    {
        gameState.OnRoundChanged += UpdateRoundText;
        gameState.OnMoneyChanged += UpdateMoneyText;

        if (buildManager != null)
            buildManager.OnBuildSelectionChanged += HandleBuildSelectionChanged;
    }

    private void RefreshAll()
    {
        UpdateRoundText(gameState.CurrentRound);
        UpdateMoneyText(gameState.Money);
        ClearContext();
    }

    private string GetUpgradeText(Tower tower)
    {
        int cost = tower.GetNextUpgradeCost();
        return cost >= 0 ? cost.ToString() : "max";
    }

    private void UpdateRoundText(int round)
    {
        if (roundText != null)
            roundText.text = $"Runde: {round}";
    }

    private void UpdateMoneyText(int money)
    {
        if (moneyText != null)
            moneyText.text = $"Geld: {money}";

        UpdateTowerButtonStates();
    }

    private void EnsureCanvas()
    {
        if (targetCanvas != null)
            return;

        targetCanvas = FindAnyObjectByType<Canvas>();

        if (targetCanvas != null)
            return;

        GameObject canvasObject = new GameObject("InGameHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

    private void EnsureTowerSelectionController()
    {
        TowerSelectionController selectionController = FindAnyObjectByType<TowerSelectionController>();

        if (selectionController == null)
            selectionController = gameObject.AddComponent<TowerSelectionController>();

        towerSelectionController = selectionController;
        selectionController.Configure(Camera.main, this, buildManager);
    }

    private void BuildHud()
    {
        hudRoot = new GameObject("InGameHud", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        hudRoot.transform.SetParent(targetCanvas.transform, false);

        RectTransform rect = hudRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(PanelWidth, 0f);
        rect.anchoredPosition = Vector2.zero;

        Image background = hudRoot.GetComponent<Image>();
        background.color = new Color(0.07f, 0.08f, 0.1f, 1f);

        VerticalLayoutGroup layout = hudRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        backToEditorButton = CreateButton(hudRoot.transform, "Zurueck zum Editor", HandleBackToEditorClicked, true);
        backToEditorButton.gameObject.SetActive(false);

        menuButton = CreateButton(hudRoot.transform, "Menue", HandleMenuClicked, true);
        menuButton.gameObject.SetActive(false);

        roundText = CreateText("RoundText", hudRoot.transform, "Runde: 1", 22, TextAnchor.MiddleRight, Color.white);
        roundText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
        moneyText = CreateText("MoneyText", hudRoot.transform, "Geld: 0", 20, TextAnchor.MiddleRight, new Color(1f, 0.86f, 0.32f));
        moneyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

        CreateDivider(hudRoot.transform);
        BuildContextArea(hudRoot.transform);
        CreateDivider(hudRoot.transform);
        BuildTowerListArea(hudRoot.transform);
    }

    private void BuildContextArea(Transform parent)
    {
        GameObject iconFrame = new GameObject("TowerIconFrame", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconFrame.transform.SetParent(parent, false);
        iconFrame.GetComponent<LayoutElement>().preferredHeight = 86f;
        iconFrame.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.18f, 0.95f);

        towerIconImage = new GameObject("TowerIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        towerIconImage.transform.SetParent(iconFrame.transform, false);
        towerIconImage.preserveAspect = true;
        towerIconImage.enabled = false;

        RectTransform iconRect = towerIconImage.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(64f, 64f);

        contextTitleText = CreateText("ContextTitle", parent, "Auswahl", 20, TextAnchor.MiddleLeft, Color.white);
        contextTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

        contextBodyText = CreateText("ContextBody", parent, "Kein Turm ausgewaehlt.", 16, TextAnchor.UpperLeft, new Color(0.82f, 0.84f, 0.88f));
        contextBodyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;

        upgradeButton = CreateButton(parent, "Upgrade", UpgradeSelectedTower, true);
        upgradeButtonText = upgradeButton.GetComponentInChildren<Text>();
        upgradeButton.gameObject.SetActive(false);

        sellButton = CreateButton(parent, "Verkaufen", SellSelectedTower, true);
        sellButtonText = sellButton.GetComponentInChildren<Text>();
        sellButton.gameObject.SetActive(false);
    }

    private void BuildTowerListArea(Transform parent)
    {
        towerListPanel = new GameObject("TowerListPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
        towerListPanel.transform.SetParent(parent, false);

        VerticalLayoutGroup layout = towerListPanel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text title = CreateText("TowerListTitle", towerListPanel.transform, "Baubare Tuerme", 18, TextAnchor.MiddleLeft, Color.white);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        towerBuildButtons.Clear();

        if (buildManager == null || buildManager.AvailableTowers.Count == 0)
        {
            Text emptyText = CreateText("NoTowerBuildOptions", towerListPanel.transform, "Keine Tuerme konfiguriert.", 15, TextAnchor.MiddleLeft, new Color(0.82f, 0.84f, 0.88f));
            emptyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            return;
        }

        foreach (TowerData towerData in buildManager.AvailableTowers)
        {
            TowerData capturedTowerData = towerData;
            Button button = CreateButton(towerListPanel.transform, GetTowerButtonLabel(towerData), () => SelectTowerForBuilding(capturedTowerData), true);

            towerBuildButtons.Add(new TowerBuildButton
            {
                towerData = towerData,
                button = button,
                labelText = button.GetComponentInChildren<Text>()
            });
        }

        CreateButton(towerListPanel.transform, "Auswahl abbrechen", ClearBuildSelection, true);
        UpdateTowerButtonStates();
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

        Text labelText = CreateText("Label", buttonObject.transform, label, 16, TextAnchor.MiddleCenter, Color.white);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        return button;
    }

    private void SelectTowerForBuilding(TowerData towerData)
    {
        if (buildManager == null || towerData == null)
            return;

        if (!buildManager.CanAfford(towerData))
            return;

        buildManager.SelectTowerToBuild(towerData);
    }

    private void ClearBuildSelection()
    {
        if (buildManager != null)
            buildManager.ClearSelectedTowerToBuild();
        else
            ClearContext();
    }

    private void HandleBuildSelectionChanged(TowerData towerData)
    {
        if (towerData == null)
        {
            ClearContext();
            return;
        }

        contextTitleText.text = "Baumodus";
        contextBodyText.text =
            $"{GetTowerName(towerData)}\n" +
            $"Kosten: {towerData.cost}\n" +
            $"Schaden: {towerData.damage:0.#}\n" +
            $"Tempo: {towerData.attackSpeed:0.##}/s\n" +
            $"Reichweite: {towerData.range:0.#}\n" +
            "Linksklick baut. Rechtsklick oder Esc bricht ab.";

        towerIconImage.sprite = towerData.icon;
        towerIconImage.enabled = towerData.icon != null;

        if (towerListPanel != null)
            towerListPanel.SetActive(true);
    }

    private void UpdateTowerButtonStates()
    {
        foreach (TowerBuildButton towerBuildButton in towerBuildButtons)
        {
            if (towerBuildButton.towerData == null || towerBuildButton.button == null)
                continue;

            bool canAfford = buildManager == null || buildManager.CanAfford(towerBuildButton.towerData);
            bool hasPrefab = towerBuildButton.towerData.towerPrefab != null;

            towerBuildButton.button.interactable = canAfford && hasPrefab;

            if (towerBuildButton.labelText != null)
                towerBuildButton.labelText.text = GetTowerButtonLabel(towerBuildButton.towerData);
        }

        RefreshUpgradeButton();
    }

    private void RefreshUpgradeButton()
    {
        if (upgradeButton == null)
            return;

        if (currentSelectedTower == null)
        {
            upgradeButton.gameObject.SetActive(false);
            return;
        }

        int cost = currentSelectedTower.GetNextUpgradeCost();
        bool canUpgrade = cost >= 0;
        bool canAfford = gameState == null || gameState.Money >= cost;

        upgradeButton.gameObject.SetActive(canUpgrade);
        upgradeButton.interactable = canUpgrade && canAfford;

        if (upgradeButtonText != null)
            upgradeButtonText.text = canUpgrade ? $"Upgrade ({cost} Gold)" : "Upgrade max";
    }

    private string GetTowerButtonLabel(TowerData towerData)
    {
        return $"{GetTowerName(towerData)} - {towerData.cost} Gold";
    }

    private string GetTowerName(TowerData towerData)
    {
        return string.IsNullOrWhiteSpace(towerData.towerName) ? towerData.name : towerData.towerName;
    }

    private void CreateDivider(Transform parent)
    {
        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divider.transform.SetParent(parent, false);
        divider.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);
        divider.GetComponent<LayoutElement>().preferredHeight = 1f;
    }

    private void RefreshTopButtons()
    {
        if (backToEditorButton != null)
            backToEditorButton.gameObject.SetActive(GameSession.IsEditorTestRun);

        if (menuButton != null)
            menuButton.gameObject.SetActive(!GameSession.IsEditorTestRun && !GameSession.IsMapEditorSession);
    }

    private void HandleMenuClicked()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    private void HandleBackToEditorClicked()
    {
        OnBackToEditorRequested?.Invoke();
    }

    private void SellSelectedTower()
    {
        if (buildManager != null && currentSelectedTower != null)
            buildManager.SellTower(currentSelectedTower);

        towerSelectionController?.DeselectTower();
        currentSelectedTower = null;
        ClearContext();
    }

    private void UpgradeSelectedTower()
    {
        if (currentSelectedTower == null)
            return;

        int cost = currentSelectedTower.GetNextUpgradeCost();
        if (cost < 0)
        {
            RefreshUpgradeButton();
            return;
        }

        if (gameState != null && !gameState.TrySpendMoney(cost))
        {
            RefreshUpgradeButton();
            return;
        }

        if (!currentSelectedTower.TryUpgrade())
        {
            gameState?.AddMoney(cost);
            RefreshUpgradeButton();
            return;
        }

        ShowTower(currentSelectedTower);
        towerSelectionController?.RefreshTowerRange(currentSelectedTower);
    }
}
