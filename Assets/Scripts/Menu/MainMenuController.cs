using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TD.Core;
using TD.Level;
using TD.Towers;

namespace TD.Menu
{
    public class MainMenuController : MonoBehaviour
    {
        private const string LanguagePlayerPrefsKey = "TD.UiLanguage";
        private static readonly Vector2 MenuReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly Vector2Int[] CommonResolutions =
        {
            new Vector2Int(1024, 768),
            new Vector2Int(1280, 720),
            new Vector2Int(1280, 800),
            new Vector2Int(1366, 768),
            new Vector2Int(1440, 900),
            new Vector2Int(1600, 900),
            new Vector2Int(1680, 1050),
            new Vector2Int(1920, 1080),
            new Vector2Int(1920, 1200),
            new Vector2Int(2560, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(2560, 1600),
            new Vector2Int(3440, 1440),
            new Vector2Int(3840, 2160),
            new Vector2Int(5120, 1440)
        };

        private enum MenuLanguage
        {
            English,
            German
        }

        [SerializeField] private MainMenuConfig config;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Font font;
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private bool showOnStart = true;

        private GameObject menuRoot;
        private GameObject mainPanel;
        private GameObject playOptionsPanel;
        private GameObject customMapsOptionsPanel;
        private GameObject campaignPanel;
        private GameObject customMapsPanel;
        private GameObject placeholderPanel;
        private GameObject optionsOverlay;
        private CanvasGroup menuContentCanvasGroup;
        private Button playButton;
        private Button customMapsGroupButton;
        private Dropdown languageDropdown;
        private Toggle fullscreenToggle;
        private Dropdown resolutionDropdown;
        private Text placeholderTitle;
        private Text placeholderBody;
        private bool playMenuExpanded;
        private bool customMapsMenuExpanded;
        private MenuLanguage currentLanguage;
        private readonly List<Vector2Int> availableResolutions = new List<Vector2Int>();
        private GameObject selectionBeforeOptions;

        internal bool IsPlayMenuExpanded => playMenuExpanded;
        internal bool IsCustomMapsMenuExpanded => customMapsMenuExpanded;
        internal GameObject PlayOptionsPanel => playOptionsPanel;
        internal GameObject CustomMapsOptionsPanel => customMapsOptionsPanel;
        internal Button PlayButton => playButton;
        internal Button CustomMapsGroupButton => customMapsGroupButton;
        internal GameObject OptionsOverlay => optionsOverlay;
        internal Dropdown LanguageDropdown => languageDropdown;
        internal Toggle FullscreenToggle => fullscreenToggle;
        internal Dropdown ResolutionDropdown => resolutionDropdown;
        internal IReadOnlyList<Vector2Int> AvailableResolutions => availableResolutions;
        internal CanvasGroup MenuContentCanvasGroup => menuContentCanvasGroup;

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
            currentLanguage = (MenuLanguage)Mathf.Clamp(PlayerPrefs.GetInt(LanguagePlayerPrefsKey, 0), 0, 1);
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
            CloseOptionsWindow();
            SetPlayMenuExpanded(false);
        }

        public void HideMenu()
        {
            if (menuRoot != null)
                menuRoot.SetActive(false);
        }

        private void EnsureCanvas()
        {
            if (targetCanvas == null)
                targetCanvas = FindAnyObjectByType<Canvas>();

            if (targetCanvas == null)
            {
                GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                targetCanvas = canvasObject.GetComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = MenuReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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
            {
                GameObject previousRoot = menuRoot;
                menuRoot = null;

                if (Application.isPlaying)
                    Destroy(previousRoot);
                else
                    DestroyImmediate(previousRoot);
            }

            menuRoot = CreateFullScreenRoot();
            GameObject panelFrame = CreatePanelFrame(menuRoot.transform);

            CreateTitle(panelFrame.transform, GetTitle());

            mainPanel = CreateHorizontalPanel("MainPanel", panelFrame.transform);
            campaignPanel = CreateVerticalPanel("CampaignPanel", panelFrame.transform);
            customMapsPanel = CreateVerticalPanel("CustomMapsPanel", panelFrame.transform);
            placeholderPanel = CreateVerticalPanel("PlaceholderPanel", panelFrame.transform);

            BuildMainPanel();
            BuildCampaignPanel();
            BuildPlaceholderPanel();
            BuildOptionsOverlay();
        }

        private GameObject CreateFullScreenRoot()
        {
            GameObject root = new GameObject("MainMenu", typeof(RectTransform));
            root.transform.SetParent(targetCanvas.transform, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CreateBackground(root.transform);

            return root;
        }

        private void CreateBackground(Transform parent)
        {
            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);

            RectTransform rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = background.GetComponent<Image>();
            image.sprite = backgroundSprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = backgroundSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.72f);

            if (backgroundSprite != null && backgroundSprite.rect.height > 0f)
            {
                AspectRatioFitter aspectFitter = background.AddComponent<AspectRatioFitter>();
                aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspectFitter.aspectRatio = backgroundSprite.rect.width / backgroundSprite.rect.height;
            }
        }

        private GameObject CreatePanelFrame(Transform parent)
        {
            GameObject frame = new GameObject("MenuFrame", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(CanvasGroup));
            frame.transform.SetParent(parent, false);

            menuContentCanvasGroup = frame.GetComponent<CanvasGroup>();

            RectTransform rect = frame.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 1020f);

            VerticalLayoutGroup layout = frame.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return frame;
        }

        private GameObject CreateVerticalPanel(string panelName, Transform parent, bool controlChildWidth = true)
        {
            GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(parent, false);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = controlChildWidth;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            return panel;
        }

        private GameObject CreateHorizontalPanel(string panelName, Transform parent)
        {
            GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            panel.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(200, 0, 0, 0);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            return panel;
        }

        private void CreateTitle(Transform parent, string title)
        {
            Text titleText = CreateText("Title", parent, title, 44, TextAnchor.MiddleCenter, Color.white);
            Shadow shadow = titleText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.05f, 0.35f, 0.8f, 0.9f);
            shadow.effectDistance = new Vector2(3f, -3f);

            LayoutElement layoutElement = titleText.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 78f;
        }

        private void BuildMainPanel()
        {
            List<MainMenuButtonConfig> buttons = GetMainButtons();

            GameObject topLevelPanel = CreateVerticalPanel("TopLevelPanel", mainPanel.transform);
            SetColumnWidth(topLevelPanel, 210f);
            playButton = CreateHexagonButton(
                topLevelPanel.transform,
                Localize("Play", "Spielen"),
                "PlayButton",
                TogglePlayMenu,
                true,
                210f,
                182f,
                23,
                false);
            CreateActionHexagon(topLevelPanel.transform, buttons, MainMenuAction.TowerUpgrade, 210f, 182f, 23);
            CreateActionHexagon(topLevelPanel.transform, buttons, MainMenuAction.Options, 210f, 182f, 23);
            CreateActionHexagon(topLevelPanel.transform, buttons, MainMenuAction.Exit, 210f, 182f, 23, true);

            playOptionsPanel = CreateVerticalPanel("PlayOptionsPanel", mainPanel.transform);
            SetColumnWidth(playOptionsPanel, 190f);
            CreateActionHexagon(playOptionsPanel.transform, buttons, MainMenuAction.SingleCampaign, 190f, 165f, 20);
            CreateActionHexagon(playOptionsPanel.transform, buttons, MainMenuAction.Infinite, 190f, 165f, 20);
            CreateActionHexagon(playOptionsPanel.transform, buttons, MainMenuAction.Challenge, 190f, 165f, 20);
            customMapsGroupButton = CreateHexagonButton(
                playOptionsPanel.transform,
                Localize("Custom Maps", "Benutzerkarten"),
                "CustomMapsGroupButton",
                ToggleCustomMapsMenu,
                true,
                190f,
                165f,
                20,
                false);
            playOptionsPanel.SetActive(false);

            customMapsOptionsPanel = CreateVerticalPanel("CustomMapsOptionsPanel", mainPanel.transform);
            SetColumnWidth(customMapsOptionsPanel, 174f);
            VerticalLayoutGroup customMapsLayout = customMapsOptionsPanel.GetComponent<VerticalLayoutGroup>();
            customMapsLayout.padding = new RectOffset(0, 0, 525, 0);
            CreateActionHexagon(customMapsOptionsPanel.transform, buttons, MainMenuAction.CustomMaps, 174f, 151f, 18);
            CreateActionHexagon(customMapsOptionsPanel.transform, buttons, MainMenuAction.MapEditor, 174f, 151f, 18);
            customMapsOptionsPanel.SetActive(false);
        }

        private void SetColumnWidth(GameObject column, float width)
        {
            LayoutElement layoutElement = column.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
        }

        private Button CreateActionHexagon(
            Transform parent,
            List<MainMenuButtonConfig> buttons,
            MainMenuAction action,
            float width,
            float height,
            int fontSize,
            bool danger = false)
        {
            MainMenuButtonConfig buttonConfig = FindButtonConfig(buttons, action);
            if (buttonConfig == null)
                return null;

            MainMenuButtonConfig capturedConfig = buttonConfig;
            string label = GetActionLabel(buttonConfig);
            return CreateHexagonButton(
                parent,
                label,
                GetActionObjectName(action),
                () => HandleMainMenuAction(capturedConfig),
                buttonConfig.isEnabled,
                width,
                height,
                fontSize,
                danger);
        }

        private MainMenuButtonConfig FindButtonConfig(List<MainMenuButtonConfig> buttons, MainMenuAction action)
        {
            return buttons.Find(button => button != null && button.action == action);
        }

        private string GetActionLabel(MainMenuButtonConfig buttonConfig)
        {
            if (currentLanguage == MenuLanguage.English)
                return buttonConfig.label;

            switch (buttonConfig.action)
            {
                case MainMenuAction.SingleCampaign:
                    return "Einzelkampagne";
                case MainMenuAction.Infinite:
                    return "Endlos";
                case MainMenuAction.Challenge:
                    return "Herausforderung";
                case MainMenuAction.TowerUpgrade:
                    return "Türme";
                case MainMenuAction.Options:
                    return "Optionen";
                case MainMenuAction.MapEditor:
                    return "Karteneditor";
                case MainMenuAction.CustomMaps:
                    return "Benutzerkarten";
                case MainMenuAction.Exit:
                    return "Beenden";
                default:
                    return buttonConfig.label;
            }
        }

        private string GetActionObjectName(MainMenuAction action)
        {
            switch (action)
            {
                case MainMenuAction.TowerUpgrade:
                    return "TowersButton";
                default:
                    return action + "Button";
            }
        }

        private Button CreateHexagonButton(
            Transform parent,
            string label,
            string objectName,
            UnityAction onClick,
            bool interactable,
            float width,
            float height,
            int fontSize,
            bool danger)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(HexagonGraphic),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            HexagonGraphic border = buttonObject.GetComponent<HexagonGraphic>();
            border.color = Color.white;

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(HexagonGraphic));
            fillObject.transform.SetParent(buttonObject.transform, false);

            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(7f, 7f);
            fillRect.offsetMax = new Vector2(-7f, -7f);

            HexagonGraphic fill = fillObject.GetComponent<HexagonGraphic>();
            fill.color = danger
                ? new Color(0.16f, 0.025f, 0.035f, 0.97f)
                : new Color(0.025f, 0.055f, 0.11f, 0.96f);
            fill.raycastTarget = false;

            Button button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;
            button.targetGraphic = border;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = danger
                ? new Color(0.82f, 0.06f, 0.09f, 1f)
                : new Color(0.02f, 0.48f, 0.82f, 1f);
            colors.highlightedColor = danger
                ? new Color(1f, 0.18f, 0.2f, 1f)
                : new Color(0.16f, 0.76f, 1f, 1f);
            colors.pressedColor = danger
                ? new Color(0.5f, 0.015f, 0.03f, 1f)
                : new Color(0.96f, 0.12f, 0.68f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.18f, 0.18f, 0.2f, 0.8f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            Text labelText = CreateText("Label", buttonObject.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            labelText.fontStyle = FontStyle.Bold;
            labelText.raycastTarget = false;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 13;
            labelText.resizeTextMaxSize = fontSize;

            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(18f, 18f);
            labelRect.offsetMax = new Vector2(-18f, -18f);

            Shadow shadow = labelText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return button;
        }

        private void TogglePlayMenu()
        {
            SetPlayMenuExpanded(!playMenuExpanded);
        }

        private void SetPlayMenuExpanded(bool expanded)
        {
            playMenuExpanded = expanded;

            if (playOptionsPanel != null)
                playOptionsPanel.SetActive(expanded);

            if (!expanded)
                SetCustomMapsMenuExpanded(false);

            if (mainPanel != null && mainPanel.activeInHierarchy)
                LayoutRebuilder.ForceRebuildLayoutImmediate(mainPanel.GetComponent<RectTransform>());
        }

        private void ToggleCustomMapsMenu()
        {
            SetCustomMapsMenuExpanded(!customMapsMenuExpanded);
        }

        private void SetCustomMapsMenuExpanded(bool expanded)
        {
            customMapsMenuExpanded = expanded && playMenuExpanded;

            if (customMapsOptionsPanel != null)
                customMapsOptionsPanel.SetActive(customMapsMenuExpanded);

            if (mainPanel != null && mainPanel.activeInHierarchy)
                LayoutRebuilder.ForceRebuildLayoutImmediate(mainPanel.GetComponent<RectTransform>());
        }

        private void BuildCampaignPanel()
        {
            ClearPanel(campaignPanel.transform);

            CreateText("CampaignTitle", campaignPanel.transform, GetCampaignTitle(), 24, TextAnchor.MiddleCenter, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            List<CampaignLevelConfig> levels = GetCampaignLevels();

            if (levels.Count == 0)
            {
                CreateText("NoLevelsText", campaignPanel.transform, Localize("No levels configured.", "Keine Level konfiguriert."), 18, TextAnchor.MiddleCenter, Color.white)
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

            CreateButton(campaignPanel.transform, Localize("Back", "Zurück"), ShowMainMenu, true);
        }

        private void BuildPlaceholderPanel()
        {
            placeholderTitle = CreateText("PlaceholderTitle", placeholderPanel.transform, string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
            placeholderTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            placeholderBody = CreateText("PlaceholderBody", placeholderPanel.transform, string.Empty, 17, TextAnchor.MiddleCenter, new Color(0.82f, 0.84f, 0.88f));
            placeholderBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;

            CreateButton(placeholderPanel.transform, Localize("Back", "Zurück"), ShowMainMenu, true);
        }

        private void BuildOptionsOverlay()
        {
            optionsOverlay = new GameObject("OptionsOverlay", typeof(RectTransform), typeof(Image));
            optionsOverlay.transform.SetParent(menuRoot.transform, false);

            RectTransform overlayRect = optionsOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image dimmer = optionsOverlay.GetComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.62f);
            dimmer.raycastTarget = true;

            GameObject windowObject = new GameObject(
                "OptionsWindow",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup));
            windowObject.transform.SetParent(optionsOverlay.transform, false);

            RectTransform windowRect = windowObject.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(520f, 520f);

            Image windowImage = windowObject.GetComponent<Image>();
            windowImage.color = new Color(0.025f, 0.055f, 0.11f, 0.98f);

            Outline outline = windowObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.62f, 0.95f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);

            VerticalLayoutGroup layout = windowObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(42, 42, 30, 30);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            Text title = CreateText("OptionsTitle", windowObject.transform, Localize("Options", "Optionen"), 30, TextAnchor.MiddleCenter, Color.white);
            title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            Text languageLabel = CreateText("LanguageLabel", windowObject.transform, Localize("Language", "Sprache"), 18, TextAnchor.MiddleLeft, Color.white);
            LayoutElement languageLabelLayout = languageLabel.gameObject.AddComponent<LayoutElement>();
            languageLabelLayout.preferredWidth = 360f;
            languageLabelLayout.preferredHeight = 30f;

            languageDropdown = CreateLanguageDropdown(windowObject.transform);

            fullscreenToggle = CreateFullscreenToggle(windowObject.transform);

            Text resolutionLabel = CreateText("ResolutionLabel", windowObject.transform, Localize("Resolution", "Auflösung"), 18, TextAnchor.MiddleLeft, Color.white);
            LayoutElement resolutionLabelLayout = resolutionLabel.gameObject.AddComponent<LayoutElement>();
            resolutionLabelLayout.preferredWidth = 360f;
            resolutionLabelLayout.preferredHeight = 30f;

            resolutionDropdown = CreateResolutionDropdown(windowObject.transform);
            CreateButton(windowObject.transform, Localize("Close", "Schließen"), CloseOptionsWindow, true);

            optionsOverlay.SetActive(false);
        }

        private Dropdown CreateLanguageDropdown(Transform parent)
        {
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("English"),
                new Dropdown.OptionData("Deutsch")
            };

            return CreateDropdown(
                parent,
                "LanguageDropdown",
                options,
                (int)currentLanguage,
                HandleLanguageChanged);
        }

        private Dropdown CreateResolutionDropdown(Transform parent)
        {
            BuildAvailableResolutions();

            return CreateDropdown(
                parent,
                "ResolutionDropdown",
                BuildResolutionOptions(),
                GetActiveResolutionIndex(),
                HandleResolutionChanged);
        }

        private List<Dropdown.OptionData> BuildResolutionOptions()
        {
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            for (int i = 0; i < availableResolutions.Count; i++)
            {
                Vector2Int resolution = availableResolutions[i];
                options.Add(new Dropdown.OptionData(resolution.x + " x " + resolution.y));
            }

            return options;
        }

        private int GetActiveResolutionIndex()
        {
            Vector2Int activeResolution = GetActiveResolution(GetDisplayBounds());
            int selectedIndex = availableResolutions.FindIndex(
                resolution => resolution.x == activeResolution.x && resolution.y == activeResolution.y);

            return Mathf.Max(0, selectedIndex >= 0 ? selectedIndex : availableResolutions.Count - 1);
        }

        private Dropdown CreateDropdown(
            Transform parent,
            string objectName,
            List<Dropdown.OptionData> options,
            int selectedIndex,
            UnityAction<int> onValueChanged)
        {
            GameObject dropdownObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Dropdown),
                typeof(LayoutElement));
            dropdownObject.transform.SetParent(parent, false);

            LayoutElement dropdownLayout = dropdownObject.GetComponent<LayoutElement>();
            dropdownLayout.preferredWidth = 360f;
            dropdownLayout.preferredHeight = 52f;

            Image background = dropdownObject.GetComponent<Image>();
            background.color = new Color(0.07f, 0.15f, 0.25f, 1f);

            Text caption = CreateText("Label", dropdownObject.transform, string.Empty, 18, TextAnchor.MiddleLeft, Color.white);
            RectTransform captionRect = caption.GetComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(16f, 0f);
            captionRect.offsetMax = new Vector2(-52f, 0f);

            Text arrow = CreateText("Arrow", dropdownObject.transform, "v", 18, TextAnchor.MiddleCenter, Color.white);
            RectTransform arrowRect = arrow.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = Vector2.one;
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.sizeDelta = new Vector2(44f, 0f);
            arrowRect.anchoredPosition = Vector2.zero;

            GameObject templateObject = new GameObject(
                "Template",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScrollRect));
            templateObject.transform.SetParent(dropdownObject.transform, false);

            RectTransform templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0f, Mathf.Min(5, Mathf.Max(1, options.Count)) * 48f);

            Image templateImage = templateObject.GetComponent<Image>();
            templateImage.color = new Color(0.035f, 0.08f, 0.14f, 1f);

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(templateObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportObject.GetComponent<Image>().color = Color.white;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject itemObject = new GameObject(
                "Item",
                typeof(RectTransform),
                typeof(Image),
                typeof(Toggle),
                typeof(LayoutElement));
            itemObject.transform.SetParent(contentObject.transform, false);
            itemObject.GetComponent<LayoutElement>().preferredHeight = 48f;

            Image itemBackground = itemObject.GetComponent<Image>();
            itemBackground.color = new Color(0.055f, 0.13f, 0.22f, 1f);

            Toggle itemToggle = itemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackground;

            string initialLabel = options.Count > 0 ? options[0].text : string.Empty;
            Text itemLabel = CreateText("Item Label", itemObject.transform, initialLabel, 17, TextAnchor.MiddleLeft, Color.white);
            RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(16f, 0f);
            itemLabelRect.offsetMax = new Vector2(-12f, 0f);

            ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.captionText = caption;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            dropdown.options = options;
            dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, options.Count - 1)));
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(onValueChanged);

            templateObject.SetActive(false);
            return dropdown;
        }

        private Toggle CreateFullscreenToggle(Transform parent)
        {
            GameObject toggleObject = new GameObject(
                "FullscreenToggle",
                typeof(RectTransform),
                typeof(Toggle),
                typeof(LayoutElement));
            toggleObject.transform.SetParent(parent, false);

            LayoutElement toggleLayout = toggleObject.GetComponent<LayoutElement>();
            toggleLayout.preferredWidth = 360f;
            toggleLayout.preferredHeight = 44f;

            GameObject boxObject = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxObject.transform.SetParent(toggleObject.transform, false);
            RectTransform boxRect = boxObject.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(36f, 36f);

            Image boxImage = boxObject.GetComponent<Image>();
            boxImage.color = new Color(0.07f, 0.15f, 0.25f, 1f);

            Text checkmark = CreateText("Checkmark", boxObject.transform, "X", 24, TextAnchor.MiddleCenter, new Color(0.08f, 0.75f, 1f));
            checkmark.fontStyle = FontStyle.Bold;
            RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = Vector2.zero;
            checkmarkRect.anchorMax = Vector2.one;
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;

            Text label = CreateText("Label", toggleObject.transform, Localize("Fullscreen", "Vollbild"), 18, TextAnchor.MiddleLeft, Color.white);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(52f, 0f);
            labelRect.offsetMax = Vector2.zero;

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkmark;
            toggle.SetIsOnWithoutNotify(DisplaySettings.IsFullscreenMode(Screen.fullScreenMode));
            toggle.onValueChanged.AddListener(HandleFullscreenChanged);
            return toggle;
        }

        private void BuildAvailableResolutions()
        {
            availableResolutions.Clear();

            Vector2Int displayBounds = GetDisplayBounds();

            for (int i = 0; i < CommonResolutions.Length; i++)
            {
                Vector2Int resolution = CommonResolutions[i];
                if (FitsWithinDisplay(resolution, displayBounds))
                    AddResolutionIfMissing(resolution);
            }

            Vector2Int activeResolution = GetActiveResolution(displayBounds);
            if (FitsWithinDisplay(activeResolution, displayBounds))
                AddResolutionIfMissing(activeResolution);

            if (availableResolutions.Count == 0)
                AddResolutionIfMissing(displayBounds);

            availableResolutions.Sort((left, right) =>
            {
                int widthComparison = left.x.CompareTo(right.x);
                return widthComparison != 0 ? widthComparison : left.y.CompareTo(right.y);
            });
        }

        private static Vector2Int GetDisplayBounds()
        {
            Resolution currentResolution = Screen.currentResolution;
            int maximumWidth = currentResolution.width;
            int maximumHeight = currentResolution.height;

            Resolution[] supportedResolutions = Screen.resolutions;
            for (int i = 0; i < supportedResolutions.Length; i++)
            {
                maximumWidth = Mathf.Max(maximumWidth, supportedResolutions[i].width);
                maximumHeight = Mathf.Max(maximumHeight, supportedResolutions[i].height);
            }

            maximumWidth = Mathf.Max(maximumWidth, Screen.width);
            maximumHeight = Mathf.Max(maximumHeight, Screen.height);

            if (maximumWidth <= 0 || maximumHeight <= 0)
                return new Vector2Int(1920, 1080);

            return new Vector2Int(maximumWidth, maximumHeight);
        }

        private static Vector2Int GetActiveResolution(Vector2Int displayBounds)
        {
            Vector2Int activeResolution = new Vector2Int(Screen.width, Screen.height);
            return FitsWithinDisplay(activeResolution, displayBounds)
                ? activeResolution
                : displayBounds;
        }

        private static bool FitsWithinDisplay(Vector2Int resolution, Vector2Int displayBounds)
        {
            return resolution.x > 0 &&
                   resolution.y > 0 &&
                   resolution.x <= displayBounds.x &&
                   resolution.y <= displayBounds.y;
        }

        private void AddResolutionIfMissing(Vector2Int resolution)
        {
            if (resolution.x <= 0 || resolution.y <= 0)
                return;

            if (!availableResolutions.Contains(resolution))
                availableResolutions.Add(resolution);
        }

        private void HandleFullscreenChanged(bool fullscreen)
        {
            DisplaySettings.Apply(GetSelectedResolution(), fullscreen);
        }

        private void HandleResolutionChanged(int resolutionIndex)
        {
            if (resolutionIndex < 0 || resolutionIndex >= availableResolutions.Count)
                return;

            Vector2Int resolution = availableResolutions[resolutionIndex];
            bool fullscreen = fullscreenToggle != null
                ? fullscreenToggle.isOn
                : DisplaySettings.IsFullscreenMode(Screen.fullScreenMode);
            DisplaySettings.Apply(resolution, fullscreen);
        }

        private Vector2Int GetSelectedResolution()
        {
            if (resolutionDropdown != null &&
                resolutionDropdown.value >= 0 &&
                resolutionDropdown.value < availableResolutions.Count)
            {
                return availableResolutions[resolutionDropdown.value];
            }

            return GetActiveResolution(GetDisplayBounds());
        }

        private void ShowOptionsWindow()
        {
            if (optionsOverlay == null)
                return;

            RefreshDisplayControls();

            if (!optionsOverlay.activeSelf)
            {
                EventSystem eventSystem = GetAvailableEventSystem();
                selectionBeforeOptions = eventSystem != null
                    ? eventSystem.currentSelectedGameObject
                    : null;
            }

            if (menuContentCanvasGroup != null)
            {
                menuContentCanvasGroup.interactable = false;
                menuContentCanvasGroup.blocksRaycasts = false;
            }

            optionsOverlay.transform.SetAsLastSibling();
            optionsOverlay.SetActive(true);

            EventSystem optionsEventSystem = GetAvailableEventSystem();
            if (optionsEventSystem != null && languageDropdown != null)
            {
                optionsEventSystem.SetSelectedGameObject(null);
                optionsEventSystem.SetSelectedGameObject(languageDropdown.gameObject);
            }
        }

        private void RefreshDisplayControls()
        {
            if (fullscreenToggle != null)
                fullscreenToggle.SetIsOnWithoutNotify(DisplaySettings.IsFullscreenMode(Screen.fullScreenMode));

            if (resolutionDropdown == null)
                return;

            BuildAvailableResolutions();
            resolutionDropdown.options = BuildResolutionOptions();
            resolutionDropdown.SetValueWithoutNotify(GetActiveResolutionIndex());
            resolutionDropdown.RefreshShownValue();
        }

        private void CloseOptionsWindow()
        {
            bool wasOpen = optionsOverlay != null && optionsOverlay.activeSelf;
            if (optionsOverlay != null)
                optionsOverlay.SetActive(false);

            if (menuContentCanvasGroup != null)
            {
                menuContentCanvasGroup.interactable = true;
                menuContentCanvasGroup.blocksRaycasts = true;
            }

            EventSystem eventSystem = GetAvailableEventSystem();
            if (!wasOpen || eventSystem == null)
                return;

            GameObject objectToSelect = selectionBeforeOptions;
            Selectable previousSelectable = objectToSelect != null
                ? objectToSelect.GetComponent<Selectable>()
                : null;

            if (objectToSelect == null ||
                !objectToSelect.activeInHierarchy ||
                previousSelectable != null && !previousSelectable.IsInteractable())
            {
                objectToSelect = playButton != null ? playButton.gameObject : null;
            }

            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(objectToSelect);
            selectionBeforeOptions = null;
        }

        private EventSystem GetAvailableEventSystem()
        {
            return EventSystem.current != null
                ? EventSystem.current
                : FindAnyObjectByType<EventSystem>();
        }

        private void HandleLanguageChanged(int languageIndex)
        {
            MenuLanguage selectedLanguage = (MenuLanguage)Mathf.Clamp(languageIndex, 0, 1);
            if (selectedLanguage == currentLanguage)
                return;

            currentLanguage = selectedLanguage;
            PlayerPrefs.SetInt(LanguagePlayerPrefsKey, (int)currentLanguage);
            PlayerPrefs.Save();

            BuildMenu();
            ShowMainMenu();
            ShowOptionsWindow();
        }

        private string Localize(string english, string german)
        {
            return currentLanguage == MenuLanguage.German ? german : english;
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
            layoutElement.preferredWidth = 340f;
            layoutElement.preferredHeight = 48f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;

            Button button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.04f, 0.10f, 0.18f, 0.92f);
            colors.highlightedColor = new Color(0.04f, 0.48f, 0.78f, 0.96f);
            colors.pressedColor = new Color(0.52f, 0.08f, 0.42f, 0.96f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.08f, 0.08f, 0.11f, 0.76f);
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
                    ShowPlaceholder(GetActionLabel(buttonConfig));
                    break;
                case MainMenuAction.Options:
                    ShowOptionsWindow();
                    break;
                case MainMenuAction.Exit:
                    QuitApplication();
                    break;
            }
        }

        private void QuitApplication()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #elif UNITY_WEBGL
            Debug.Log("MainMenuController: Quit not supported in WebGL builds.");
    #else
            Application.Quit();
    #endif
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
            placeholderBody.text = Localize(
                "This section is prepared and can be filled in later.",
                "Dieser Bereich ist vorbereitet und kann später ergänzt werden.");

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
                Debug.LogError("MainMenuController: No LevelData assigned to this level button.");
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
                Debug.LogError($"MainMenuController: Map seed '{label}' is invalid ({error}).");
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
            if (currentLanguage == MenuLanguage.German)
                return "Einzelkampagne";

            return config != null && !string.IsNullOrWhiteSpace(config.campaignTitle) ? config.campaignTitle : "Single Campaign";
        }

        private List<MainMenuButtonConfig> GetMainButtons()
        {
            List<MainMenuButtonConfig> buttons;

            if (config != null && config.mainButtons != null && config.mainButtons.Count > 0)
            {
                buttons = new List<MainMenuButtonConfig>(config.mainButtons);
            }
            else
            {
                buttons = new List<MainMenuButtonConfig>
                {
                    new MainMenuButtonConfig("Single Campaign", MainMenuAction.SingleCampaign),
                    new MainMenuButtonConfig("Infinite", MainMenuAction.Infinite),
                    new MainMenuButtonConfig("Challenge", MainMenuAction.Challenge),
                    new MainMenuButtonConfig("Towers", MainMenuAction.TowerUpgrade),
                    new MainMenuButtonConfig("Map Editor", MainMenuAction.MapEditor),
                    new MainMenuButtonConfig("Custom Maps", MainMenuAction.CustomMaps),
                    new MainMenuButtonConfig("Options", MainMenuAction.Options)
                };
            }

            // Ensure there is always an Exit button
            if (!buttons.Exists(b => b.action == MainMenuAction.Exit))
                buttons.Add(new MainMenuButtonConfig("Exit", MainMenuAction.Exit));

            return buttons;
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

            CreateText("CustomMapsTitle", customMapsPanel.transform, Localize("Custom Maps", "Benutzerkarten"), 24, TextAnchor.MiddleCenter, Color.white)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            List<CustomMapEntry> customMaps = CustomMapStorage.GetAll();
            if (customMaps.Count == 0)
            {
                CreateText("NoCustomMapsText", customMapsPanel.transform, Localize("No custom maps saved.", "Keine Benutzerkarten gespeichert."), 18, TextAnchor.MiddleCenter, Color.white)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
            }
            else
            {
                foreach (CustomMapEntry customMap in customMaps)
                {
                    CustomMapEntry capturedMap = customMap;
                    string label = string.IsNullOrWhiteSpace(customMap.label)
                        ? Localize("Custom Map", "Benutzerkarte")
                        : customMap.label;
                    CreateButton(customMapsPanel.transform, label, () => StartCustomMap(capturedMap), true);
                }
            }

            CreateButton(customMapsPanel.transform, Localize("Back", "Zurück"), ShowMainMenu, true);
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
}
