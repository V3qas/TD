using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TD.Menu;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TD.Tests.EditMode
{
    public class MainMenuLayoutTests
    {
        private const string LanguagePlayerPrefsKey = "TD.UiLanguage";
        private const string ResolutionWidthPlayerPrefsKey = "TD.ResolutionWidth";
        private const string ResolutionHeightPlayerPrefsKey = "TD.ResolutionHeight";

        private GameObject canvasObject;
        private GameObject controllerObject;
        private MainMenuConfig config;
        private bool hadStoredLanguage;
        private int storedLanguage;
        private bool hadStoredResolutionWidth;
        private bool hadStoredResolutionHeight;
        private int storedResolutionWidth;
        private int storedResolutionHeight;

        [SetUp]
        public void SetUp()
        {
            hadStoredLanguage = PlayerPrefs.HasKey(LanguagePlayerPrefsKey);
            storedLanguage = PlayerPrefs.GetInt(LanguagePlayerPrefsKey, 0);
            hadStoredResolutionWidth = PlayerPrefs.HasKey(ResolutionWidthPlayerPrefsKey);
            hadStoredResolutionHeight = PlayerPrefs.HasKey(ResolutionHeightPlayerPrefsKey);
            storedResolutionWidth = PlayerPrefs.GetInt(ResolutionWidthPlayerPrefsKey, 0);
            storedResolutionHeight = PlayerPrefs.GetInt(ResolutionHeightPlayerPrefsKey, 0);
            PlayerPrefs.SetInt(LanguagePlayerPrefsKey, 0);
            PlayerPrefs.SetInt(ResolutionWidthPlayerPrefsKey, int.MaxValue);
            PlayerPrefs.SetInt(ResolutionHeightPlayerPrefsKey, int.MaxValue);
        }

        [TearDown]
        public void TearDown()
        {
            if (controllerObject != null)
                Object.DestroyImmediate(controllerObject);

            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);

            if (config != null)
                Object.DestroyImmediate(config);

            if (hadStoredLanguage)
                PlayerPrefs.SetInt(LanguagePlayerPrefsKey, storedLanguage);
            else
                PlayerPrefs.DeleteKey(LanguagePlayerPrefsKey);

            if (hadStoredResolutionWidth)
                PlayerPrefs.SetInt(ResolutionWidthPlayerPrefsKey, storedResolutionWidth);
            else
                PlayerPrefs.DeleteKey(ResolutionWidthPlayerPrefsKey);

            if (hadStoredResolutionHeight)
                PlayerPrefs.SetInt(ResolutionHeightPlayerPrefsKey, storedResolutionHeight);
            else
                PlayerPrefs.DeleteKey(ResolutionHeightPlayerPrefsKey);

            PlayerPrefs.Save();
        }

        [Test]
        public void HexagonMenu_UsesRequestedHierarchyAndSizing()
        {
            config = ScriptableObject.CreateInstance<MainMenuConfig>();
            config.mainButtons = new List<MainMenuButtonConfig>
            {
                new MainMenuButtonConfig("Single Campaign", MainMenuAction.SingleCampaign),
                new MainMenuButtonConfig("Infinite", MainMenuAction.Infinite),
                new MainMenuButtonConfig("Challenge", MainMenuAction.Challenge),
                new MainMenuButtonConfig("Towers", MainMenuAction.TowerUpgrade),
                new MainMenuButtonConfig("Map Editor", MainMenuAction.MapEditor),
                new MainMenuButtonConfig("Custom Maps", MainMenuAction.CustomMaps),
                new MainMenuButtonConfig("Options", MainMenuAction.Options),
                new MainMenuButtonConfig("Exit", MainMenuAction.Exit)
            };

            canvasObject = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            controllerObject = new GameObject("TestMainMenuController");
            MainMenuController controller = controllerObject.AddComponent<MainMenuController>();

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("config").objectReferenceValue = config;
            serializedController.FindProperty("targetCanvas").objectReferenceValue = canvasObject.GetComponent<Canvas>();
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MainMenuBackground.png");
            Assert.That(backgroundSprite, Is.Not.Null);
            serializedController.FindProperty("backgroundSprite").objectReferenceValue = backgroundSprite;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            MethodInfo startMethod = typeof(MainMenuController).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(startMethod, Is.Not.Null);
            startMethod.Invoke(controller, null);

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            Assert.That(canvasScaler, Is.Not.Null);
            Assert.That(canvasScaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(canvasScaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));

            Assert.That(controller.PlayButton, Is.Not.Null);
            Assert.That(controller.PlayOptionsPanel.activeSelf, Is.False);
            Assert.That(controller.CustomMapsOptionsPanel.activeSelf, Is.False);

            Transform topLevelPanel = controller.PlayButton.transform.parent;
            Assert.That(topLevelPanel.Find("TowersButton"), Is.Not.Null);
            Assert.That(topLevelPanel.Find("OptionsButton"), Is.Not.Null);
            Assert.That(topLevelPanel.Find("ExitButton"), Is.Not.Null);

            Assert.That(controller.PlayOptionsPanel.transform.Find("SingleCampaignButton"), Is.Not.Null);
            Assert.That(controller.PlayOptionsPanel.transform.Find("InfiniteButton"), Is.Not.Null);
            Assert.That(controller.PlayOptionsPanel.transform.Find("ChallengeButton"), Is.Not.Null);
            Assert.That(controller.CustomMapsGroupButton, Is.Not.Null);

            Assert.That(controller.CustomMapsOptionsPanel.transform.Find("CustomMapsButton"), Is.Not.Null);
            Assert.That(controller.CustomMapsOptionsPanel.transform.Find("MapEditorButton"), Is.Not.Null);

            RectTransform menuFrame = topLevelPanel.parent.parent.GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuFrame);

            Vector2[] ultrawideResolutions =
            {
                new Vector2(2560f, 1080f),
                new Vector2(3440f, 1440f),
                new Vector2(5120f, 1440f)
            };

            foreach (Vector2 screenResolution in ultrawideResolutions)
            {
                float canvasScale = Mathf.Min(
                    screenResolution.x / canvasScaler.referenceResolution.x,
                    screenResolution.y / canvasScaler.referenceResolution.y);
                float availableUiHeight = screenResolution.y / canvasScale;
                Assert.That(availableUiHeight, Is.GreaterThanOrEqualTo(menuFrame.rect.height));
            }

            RectTransform playButtonRect = controller.PlayButton.GetComponent<RectTransform>();
            Assert.That(playButtonRect.rect.width, Is.EqualTo(210f).Within(0.1f));
            Assert.That(playButtonRect.rect.height, Is.EqualTo(182f).Within(0.1f));
            Assert.That(topLevelPanel.localPosition.x, Is.LessThan(-50f));

            Button exitButton = topLevelPanel.Find("ExitButton").GetComponent<Button>();
            Assert.That(exitButton.colors.normalColor.r, Is.GreaterThan(exitButton.colors.normalColor.b));

            controller.PlayButton.onClick.Invoke();

            Assert.That(controller.IsPlayMenuExpanded, Is.True);
            Assert.That(controller.PlayOptionsPanel.activeSelf, Is.True);
            Assert.That(controller.PlayButton.GetComponentInChildren<Text>().text, Is.EqualTo("Play"));

            LayoutRebuilder.ForceRebuildLayoutImmediate(menuFrame);
            RectTransform campaignButtonRect = controller.PlayOptionsPanel.transform.Find("SingleCampaignButton").GetComponent<RectTransform>();
            Assert.That(campaignButtonRect.rect.width, Is.EqualTo(190f).Within(0.1f));
            Assert.That(campaignButtonRect.rect.height, Is.EqualTo(165f).Within(0.1f));

            controller.CustomMapsGroupButton.onClick.Invoke();

            Assert.That(controller.IsCustomMapsMenuExpanded, Is.True);
            Assert.That(controller.CustomMapsOptionsPanel.activeSelf, Is.True);

            controller.PlayButton.onClick.Invoke();

            Assert.That(controller.IsPlayMenuExpanded, Is.False);
            Assert.That(controller.IsCustomMapsMenuExpanded, Is.False);
            Assert.That(controller.PlayOptionsPanel.activeSelf, Is.False);
            Assert.That(controller.CustomMapsOptionsPanel.activeSelf, Is.False);

            Button optionsButton = topLevelPanel.Find("OptionsButton").GetComponent<Button>();
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);
            eventSystem.SetSelectedGameObject(optionsButton.gameObject);
            optionsButton.onClick.Invoke();

            Assert.That(controller.OptionsOverlay.activeSelf, Is.True);
            Assert.That(controller.OptionsOverlay.GetComponent<Image>().color.a, Is.GreaterThan(0.5f));
            Assert.That(controller.MenuContentCanvasGroup.interactable, Is.False);
            Assert.That(controller.MenuContentCanvasGroup.blocksRaycasts, Is.False);
            Assert.That(optionsButton.IsInteractable(), Is.False);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(controller.LanguageDropdown.gameObject));
            Assert.That(controller.LanguageDropdown.options.Count, Is.EqualTo(2));
            Assert.That(controller.LanguageDropdown.options[0].text, Is.EqualTo("English"));
            Assert.That(controller.LanguageDropdown.options[1].text, Is.EqualTo("Deutsch"));
            Assert.That(controller.LanguageDropdown.template, Is.Not.Null);
            Assert.That(controller.LanguageDropdown.itemText.transform.parent.GetComponent<Toggle>(), Is.Not.Null);
            Assert.That(controller.FullscreenToggle, Is.Not.Null);
            Assert.That(controller.FullscreenToggle.GetComponentInChildren<Text>().text, Is.EqualTo("X"));
            Assert.That(controller.FullscreenToggle.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("Fullscreen"));
            Assert.That(controller.ResolutionDropdown, Is.Not.Null);
            Assert.That(controller.ResolutionDropdown.options.Count, Is.EqualTo(controller.AvailableResolutions.Count));
            Assert.That(controller.AvailableResolutions.Count, Is.GreaterThan(0));

            Vector2Int correctedStoredResolution = new Vector2Int(
                PlayerPrefs.GetInt(ResolutionWidthPlayerPrefsKey),
                PlayerPrefs.GetInt(ResolutionHeightPlayerPrefsKey));
            Assert.That(correctedStoredResolution.x, Is.LessThan(int.MaxValue));
            Assert.That(correctedStoredResolution.y, Is.LessThan(int.MaxValue));
            Assert.That(controller.AvailableResolutions, Does.Contain(correctedStoredResolution));

            for (int i = 1; i < controller.AvailableResolutions.Count; i++)
            {
                Vector2Int previous = controller.AvailableResolutions[i - 1];
                Vector2Int current = controller.AvailableResolutions[i];
                Assert.That(current.x > previous.x || current.x == previous.x && current.y > previous.y, Is.True);
            }

            RectTransform optionsWindow = controller.OptionsOverlay.transform.Find("OptionsWindow").GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(optionsWindow);
            Assert.That(controller.LanguageDropdown.GetComponent<RectTransform>().rect.width, Is.EqualTo(360f).Within(0.1f));
            Assert.That(controller.ResolutionDropdown.GetComponent<RectTransform>().rect.width, Is.EqualTo(360f).Within(0.1f));

            Transform menuRoot = menuFrame.parent;
            AspectRatioFitter backgroundFitter = menuRoot.Find("Background").GetComponent<AspectRatioFitter>();
            Assert.That(backgroundFitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));

            Button closeButton = optionsWindow.Find("CloseButton").GetComponent<Button>();
            closeButton.onClick.Invoke();
            Assert.That(controller.OptionsOverlay.activeSelf, Is.False);
            Assert.That(controller.MenuContentCanvasGroup.interactable, Is.True);
            Assert.That(controller.MenuContentCanvasGroup.blocksRaycasts, Is.True);
            Assert.That(optionsButton.IsInteractable(), Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(optionsButton.gameObject));

            optionsButton.onClick.Invoke();

            controller.LanguageDropdown.value = 1;

            Assert.That(controller.OptionsOverlay.activeSelf, Is.True);
            Assert.That(controller.PlayButton.GetComponentInChildren<Text>().text, Is.EqualTo("Spielen"));
            Assert.That(controller.FullscreenToggle.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("Vollbild"));

            controller.LanguageDropdown.value = 0;

            Assert.That(controller.OptionsOverlay.activeSelf, Is.True);
            Assert.That(controller.PlayButton.GetComponentInChildren<Text>().text, Is.EqualTo("Play"));
            Assert.That(controller.FullscreenToggle.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("Fullscreen"));
        }

        [Test]
        public void HexagonRaycastArea_MatchesVisibleShape()
        {
            Assert.That(HexagonGraphic.ContainsNormalizedPoint(Vector2.zero), Is.True);
            Assert.That(HexagonGraphic.ContainsNormalizedPoint(new Vector2(1f, 0f)), Is.True);
            Assert.That(HexagonGraphic.ContainsNormalizedPoint(new Vector2(0.5f, 1f)), Is.True);
            Assert.That(HexagonGraphic.ContainsNormalizedPoint(new Vector2(0.51f, 1f)), Is.False);
            Assert.That(HexagonGraphic.ContainsNormalizedPoint(new Vector2(0f, 1.01f)), Is.False);
        }
    }
}
