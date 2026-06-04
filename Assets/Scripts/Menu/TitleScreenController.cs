using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows the title screen and fades to the menu after keyboard or mouse input.
/// </summary>
public class TitleScreenController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Splash-art sprite shown on the title screen.")]
    [SerializeField] private Sprite splashSprite;

    [Header("Settings")]
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private float pressTextBlinkSpeed = 1.4f;
    [SerializeField] private string pressAnyKeyText = "Press any key ...";

    private Canvas canvas;
    private Image splashImage;
    private Image fadeOverlay;
    private Text pressText;
    private bool transitionStarted;

    private void Awake()
    {
        BuildUi();
    }

    private void Start()
    {
        StartCoroutine(BlinkPressText());
    }

    private void Update()
    {
        if (transitionStarted)
            return;

        bool keyPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool mouseClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (keyPressed || mouseClick)
            StartTransition();
    }

    private void StartTransition()
    {
        transitionStarted = true;
        StartCoroutine(FadeToMenu());
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("TitleCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject bgObject = new GameObject("Background",
            typeof(RectTransform), typeof(Image));
        bgObject.transform.SetParent(canvasObject.transform, false);
        FillRect(bgObject.GetComponent<RectTransform>());
        bgObject.GetComponent<Image>().color = Color.black;

        GameObject splashObject = new GameObject("SplashArt",
            typeof(RectTransform), typeof(Image));
        splashObject.transform.SetParent(canvasObject.transform, false);
        FillRect(splashObject.GetComponent<RectTransform>());
        splashImage = splashObject.GetComponent<Image>();
        splashImage.sprite = splashSprite;
        splashImage.color = Color.white;
        splashImage.preserveAspect = false;

        if (splashSprite == null)
        {
            splashImage.color = new Color(0.05f, 0.06f, 0.1f);
            CreateFallbackTitle(canvasObject.transform);
        }

        GameObject pressObject = new GameObject("PressAnyKey",
            typeof(RectTransform), typeof(Text));
        pressObject.transform.SetParent(canvasObject.transform, false);

        RectTransform pressRect = pressObject.GetComponent<RectTransform>();
        pressRect.anchorMin = new Vector2(0f, 0f);
        pressRect.anchorMax = new Vector2(1f, 0f);
        pressRect.pivot = new Vector2(0.5f, 0f);
        pressRect.sizeDelta = new Vector2(0f, 80f);
        pressRect.anchoredPosition = new Vector2(0f, 60f);

        pressText = pressObject.GetComponent<Text>();
        pressText.text = pressAnyKeyText;
        pressText.font = GetFont();
        pressText.fontSize = 36;
        pressText.alignment = TextAnchor.MiddleCenter;
        pressText.color = new Color(0.92f, 0.92f, 1f);

        GameObject fadeObject = new GameObject("FadeOverlay",
            typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        FillRect(fadeObject.GetComponent<RectTransform>());
        fadeOverlay = fadeObject.GetComponent<Image>();
        fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        fadeOverlay.raycastTarget = false;
    }

    private void CreateFallbackTitle(Transform parent)
    {
        GameObject titleObject = new GameObject("FallbackTitle",
            typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(parent, false);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.35f);
        titleRect.anchorMax = new Vector2(0.9f, 0.7f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        Text titleText = titleObject.GetComponent<Text>();
        titleText.text = "V3Q\nDEFENSE";
        titleText.font = GetFont();
        titleText.fontSize = 96;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.5f, 0.85f, 1f);
    }

    private IEnumerator BlinkPressText()
    {
        while (!transitionStarted)
        {
            float alpha = Mathf.Abs(Mathf.Sin(Time.unscaledTime * pressTextBlinkSpeed * Mathf.PI));
            if (pressText != null)
            {
                Color color = pressText.color;
                color.a = Mathf.Lerp(0.35f, 1f, alpha);
                pressText.color = color;
            }

            yield return null;
        }
    }

    private IEnumerator FadeToMenu()
    {
        if (pressText != null)
            pressText.enabled = false;

        fadeOverlay.raycastTarget = true;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            fadeOverlay.color = new Color(0f, 0f, 0f, t);
            yield return null;
        }

        fadeOverlay.color = Color.black;
        SceneManager.LoadScene(menuSceneName);
    }

    private static void FillRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Font GetFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
