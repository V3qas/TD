using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TD.Core;
using TD.Towers;

namespace TD.UI
{
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
        private Text livesText;
        private Text contextTitleText;
        private Text contextBodyText;
        private Image towerIconImage;
        private Button upgradeButton;
        private Text upgradeButtonText;
        private Button sellButton;
        private Text sellButtonText;
        private Button targetingButton;
        private Text targetingButtonText;
        private Button backToEditorButton;
        private Button menuButton;
        private GameObject matchEndOverlay;
        private Text matchEndTitleText;
        private Text matchEndBodyText;
        private Button restartButton;
        private Text matchExitButtonText;
        private Tower currentSelectedTower;
        private TowerSelectionController towerSelectionController;

        public event System.Action OnBackToEditorRequested;

        private Font RuntimeFont => RuntimeUiFactory.ResolveFont(font);

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
            gameState.OnLivesChanged -= UpdateLivesText;
            gameState.OnMatchEnded -= HandleMatchEnded;

            if (buildManager != null)
                buildManager.OnBuildSelectionChanged -= HandleBuildSelectionChanged;
        }

        public void Show()
        {
            if (hudRoot != null)
                hudRoot.SetActive(true);

            if (matchEndOverlay != null)
                matchEndOverlay.SetActive(false);

            RefreshTopButtons();
        }

        public void Hide()
        {
            towerSelectionController?.DeselectTower();
            ClearContext();
            if (hudRoot != null)
                hudRoot.SetActive(false);

            if (matchEndOverlay != null)
                matchEndOverlay.SetActive(false);
        }

        public void ShowTower(Tower tower)
        {
            if ((gameState != null && !gameState.IsPlaying) || tower == null || tower.Data == null)
            {
                ClearContext();
                return;
            }

            currentSelectedTower = tower;
            TowerData data = tower.Data;
            contextTitleText.text = string.IsNullOrWhiteSpace(data.towerName) ? "Tower" : data.towerName;
            contextBodyText.text =
                $"Level: {tower.CurrentUpgradeLevel + 1}\n" +
                $"Damage: {tower.Damage:0.#}\n" +
                $"Speed: {tower.AttackSpeed:0.##}/s\n" +
                $"Range: {tower.Range:0.#}\n" +
                $"Targeting: {tower.TargetingMode.ToDisplayName()}\n" +
                $"Next upgrade: {GetUpgradeText(tower)}";

            towerIconImage.sprite = data.icon;
            towerIconImage.enabled = data.icon != null;

            if (sellButton != null)
            {
                sellButton.gameObject.SetActive(true);
                if (sellButtonText != null)
                    sellButtonText.text = $"Sell ({tower.GetSellValue()} Gold)";
            }

            if (targetingButton != null)
            {
                targetingButton.gameObject.SetActive(true);
                targetingButton.interactable = true;
                if (targetingButtonText != null)
                    targetingButtonText.text = $"Targeting: {tower.TargetingMode.ToDisplayName()}";
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

            if (targetingButton != null)
                targetingButton.gameObject.SetActive(false);

            if (contextTitleText == null || contextBodyText == null || towerIconImage == null)
                return;

            contextTitleText.text = "Tower Selection";
            contextBodyText.text =
                "Choose a tower below, then left-click a free tile to build.\n" +
                "Right-click or Esc cancels. Select a placed tower to upgrade or sell it.";
            towerIconImage.sprite = null;
            towerIconImage.enabled = false;

            if (towerListPanel != null)
                towerListPanel.SetActive(true);
        }

        private void SubscribeToGameState()
        {
            gameState.OnRoundChanged += UpdateRoundText;
            gameState.OnMoneyChanged += UpdateMoneyText;
            gameState.OnLivesChanged += UpdateLivesText;
            gameState.OnMatchEnded += HandleMatchEnded;

            if (buildManager != null)
                buildManager.OnBuildSelectionChanged += HandleBuildSelectionChanged;
        }

        private void RefreshAll()
        {
            UpdateRoundText(gameState.CurrentRound);
            UpdateMoneyText(gameState.Money);
            UpdateLivesText(gameState.Lives);
            ClearContext();

            if (matchEndOverlay != null)
                matchEndOverlay.SetActive(false);
        }

        private string GetUpgradeText(Tower tower)
        {
            int cost = tower.GetNextUpgradeCost();
            return cost >= 0 ? cost.ToString() : "max";
        }

        private void UpdateRoundText(int round)
        {
            if (roundText != null)
                roundText.text = $"Wave: {round} / {gameState.MaxRounds}";
        }

        private void UpdateMoneyText(int money)
        {
            if (moneyText != null)
                moneyText.text = $"Gold: {money}";

            UpdateTowerButtonStates();
        }

        private void UpdateLivesText(int lives)
        {
            if (livesText != null)
                livesText.text = $"Lives: {lives}";
        }

        private void EnsureCanvas()
        {
            targetCanvas = RuntimeUiFactory.EnsureCanvas(
                targetCanvas,
                "InGameHudCanvas",
                new Vector2(1920f, 1080f),
                false,
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
                0.5f);
        }

        private void EnsureEventSystem()
        {
            RuntimeUiFactory.EnsureEventSystem(transform);
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

            backToEditorButton = CreateButton(hudRoot.transform, "Back to Editor", HandleBackToEditorClicked, true);
            backToEditorButton.gameObject.SetActive(false);

            menuButton = CreateButton(hudRoot.transform, "Menu", HandleMenuClicked, true);
            menuButton.gameObject.SetActive(false);

            roundText = CreateText("RoundText", hudRoot.transform, "Wave: 1 / 5", 22, TextAnchor.MiddleRight, Color.white);
            roundText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            moneyText = CreateText("MoneyText", hudRoot.transform, "Gold: 0", 20, TextAnchor.MiddleRight, new Color(1f, 0.86f, 0.32f));
            moneyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            livesText = CreateText("LivesText", hudRoot.transform, "Lives: 10", 20, TextAnchor.MiddleRight, new Color(0.94f, 0.42f, 0.42f));
            livesText.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

            CreateDivider(hudRoot.transform);
            BuildContextArea(hudRoot.transform);
            CreateDivider(hudRoot.transform);
            BuildTowerListArea(hudRoot.transform);
            BuildMatchEndOverlay();
        }

        private void BuildMatchEndOverlay()
        {
            matchEndOverlay = new GameObject("MatchEndOverlay", typeof(RectTransform), typeof(Image));
            matchEndOverlay.transform.SetParent(targetCanvas.transform, false);

            RectTransform overlayRect = matchEndOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = matchEndOverlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.78f);

            GameObject panel = new GameObject("MatchEndPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(matchEndOverlay.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(420f, 300f);

            panel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 0.98f);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 28);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            matchEndTitleText = CreateText("MatchEndTitle", panel.transform, "Victory", 34, TextAnchor.MiddleCenter, Color.white);
            matchEndTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

            matchEndBodyText = CreateText("MatchEndBody", panel.transform, string.Empty, 19, TextAnchor.MiddleCenter, new Color(0.84f, 0.86f, 0.9f));
            matchEndBodyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

            restartButton = CreateButton(panel.transform, "Restart", HandleRestartClicked, true);
            Button exitButton = CreateButton(panel.transform, "Main Menu", HandleMatchExitClicked, true);
            matchExitButtonText = exitButton.GetComponentInChildren<Text>();

            matchEndOverlay.SetActive(false);
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

            contextTitleText = CreateText("ContextTitle", parent, "Selection", 20, TextAnchor.MiddleLeft, Color.white);
            contextTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            contextBodyText = CreateText("ContextBody", parent, "No tower selected.", 16, TextAnchor.UpperLeft, new Color(0.82f, 0.84f, 0.88f));
            contextBodyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 170f;

            targetingButton = CreateButton(parent, "Targeting", CycleSelectedTowerTargeting, true);
            targetingButtonText = targetingButton.GetComponentInChildren<Text>();
            targetingButton.gameObject.SetActive(false);

            upgradeButton = CreateButton(parent, "Upgrade", UpgradeSelectedTower, true);
            upgradeButtonText = upgradeButton.GetComponentInChildren<Text>();
            upgradeButton.gameObject.SetActive(false);

            sellButton = CreateButton(parent, "Sell", SellSelectedTower, true);
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

            Text title = CreateText("TowerListTitle", towerListPanel.transform, "Buildable Towers", 18, TextAnchor.MiddleLeft, Color.white);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            towerBuildButtons.Clear();

            if (buildManager == null || buildManager.AvailableTowers.Count == 0)
            {
                Text emptyText = CreateText("NoTowerBuildOptions", towerListPanel.transform, "No towers configured.", 15, TextAnchor.MiddleLeft, new Color(0.82f, 0.84f, 0.88f));
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

            CreateButton(towerListPanel.transform, "Cancel Selection", ClearBuildSelection, true);
            UpdateTowerButtonStates();
        }

        private Text CreateText(string objectName, Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
        {
            return RuntimeUiFactory.CreateText(objectName, parent, text, fontSize, alignment, color, RuntimeFont);
        }

        private Button CreateButton(Transform parent, string label, UnityAction onClick, bool interactable)
        {
            return RuntimeUiFactory.CreatePanelButton(parent, label, onClick, interactable, RuntimeFont, 48f, 8f);
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

            contextTitleText.text = "Build Mode";
            contextBodyText.text =
                $"{GetTowerName(towerData)}\n" +
                $"Cost: {towerData.cost}\n" +
                $"Damage: {towerData.DamagePerShot:0.#}\n" +
                $"Speed: {towerData.AttacksPerSecond:0.##}/s\n" +
                $"Range: {towerData.TargetingRange:0.#}\n" +
                "Left-click builds. Right-click or Esc cancels.";

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
            RuntimeUiFactory.CreateDivider(parent, new Color(1f, 1f, 1f, 0.16f));
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
            GameSession.EndTestRun();
            GameSession.EndMapEditorMode();
            GameSession.ClearSelectedLevel();
            SceneManager.LoadScene(menuSceneName);
        }

        private void HandleBackToEditorClicked()
        {
            if (matchEndOverlay != null)
                matchEndOverlay.SetActive(false);

            OnBackToEditorRequested?.Invoke();
        }

        private void HandleMatchEnded(MatchState result)
        {
            buildManager?.ClearSelectedTowerToBuild();
            towerSelectionController?.DeselectTower();
            ClearContext();
            UpdateTowerButtonStates();

            if (matchEndTitleText != null)
                matchEndTitleText.text = result == MatchState.Won ? "Victory" : "Defeat";

            if (matchEndBodyText != null)
            {
                matchEndBodyText.text = result == MatchState.Won
                    ? "All waves cleared. The base is safe."
                    : "The base has fallen.";
            }

            bool editorTest = GameSession.IsEditorTestRun;
            if (restartButton != null)
                restartButton.gameObject.SetActive(!editorTest);

            if (matchExitButtonText != null)
                matchExitButtonText.text = editorTest ? "Back to Editor" : "Main Menu";

            if (matchEndOverlay != null)
                matchEndOverlay.SetActive(true);
        }

        private void HandleRestartClicked()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private void HandleMatchExitClicked()
        {
            if (GameSession.IsEditorTestRun)
            {
                HandleBackToEditorClicked();
                return;
            }

            HandleMenuClicked();
        }

        private void SellSelectedTower()
        {
            if (gameState != null && !gameState.IsPlaying)
                return;

            if (buildManager != null && currentSelectedTower != null)
                buildManager.SellTower(currentSelectedTower);

            towerSelectionController?.DeselectTower();
            currentSelectedTower = null;
            ClearContext();
        }

        private void CycleSelectedTowerTargeting()
        {
            if ((gameState != null && !gameState.IsPlaying) || currentSelectedTower == null)
                return;

            currentSelectedTower.CycleTargetingMode();
            ShowTower(currentSelectedTower);
        }

        private void UpgradeSelectedTower()
        {
            if ((gameState != null && !gameState.IsPlaying) || currentSelectedTower == null)
                return;

            if (!TowerUpgradeService.TryPurchase(currentSelectedTower, gameState))
            {
                RefreshUpgradeButton();
                return;
            }

            ShowTower(currentSelectedTower);
            towerSelectionController?.RefreshTowerRange(currentSelectedTower);
        }
    }
}
