using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TD.UI;

/// <summary>
/// Shows the title screen and fades to the menu after keyboard or mouse input.
/// </summary>

namespace TD.Menu
{
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
            canvas = RuntimeUiFactory.CreateCanvas(
                "TitleCanvas",
                new Vector2(1920f, 1080f),
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
                0.5f);
            canvas.sortingOrder = 10;
            GameObject canvasObject = canvas.gameObject;

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

            pressText = RuntimeUiFactory.CreateText(
                "PressAnyKey",
                canvasObject.transform,
                pressAnyKeyText,
                36,
                TextAnchor.MiddleCenter,
                new Color(0.92f, 0.92f, 1f),
                null);

            RectTransform pressRect = pressText.GetComponent<RectTransform>();
            pressRect.anchorMin = new Vector2(0f, 0f);
            pressRect.anchorMax = new Vector2(1f, 0f);
            pressRect.pivot = new Vector2(0.5f, 0f);
            pressRect.sizeDelta = new Vector2(0f, 80f);
            pressRect.anchoredPosition = new Vector2(0f, 60f);

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
            Text titleText = RuntimeUiFactory.CreateText(
                "FallbackTitle",
                parent,
                "V3Q\nDEFENSE",
                96,
                TextAnchor.MiddleCenter,
                new Color(0.5f, 0.85f, 1f),
                null);

            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.35f);
            titleRect.anchorMax = new Vector2(0.9f, 0.7f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
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

    }
}
