using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TD.UI
{
    /// <summary>
    /// Shared construction helpers for the small runtime-built uGUI views.
    /// Layout composition and behavior remain owned by their controllers.
    /// </summary>
    internal static class RuntimeUiFactory
    {
        private static readonly Color PanelNormal = new Color(0.18f, 0.32f, 0.52f, 1f);
        private static readonly Color PanelInactive = new Color(0.18f, 0.18f, 0.2f, 1f);
        private static readonly Color PanelHighlighted = new Color(0.24f, 0.44f, 0.68f, 1f);
        private static readonly Color PanelPressed = new Color(0.12f, 0.24f, 0.38f, 1f);
        private static readonly Color PanelDisabled = new Color(0.12f, 0.12f, 0.14f, 1f);

        public static Font ResolveFont(Font preferredFont)
        {
            if (preferredFont != null)
                return preferredFont;

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return legacyFont != null
                ? legacyFont
                : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static Canvas EnsureCanvas(
            Canvas targetCanvas,
            string objectName,
            Vector2 referenceResolution,
            bool configureExisting,
            CanvasScaler.ScreenMatchMode screenMatchMode,
            float matchWidthOrHeight = 0.5f)
        {
            Canvas canvas = targetCanvas != null ? targetCanvas : Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return CreateCanvas(objectName, referenceResolution, screenMatchMode, matchWidthOrHeight);

            if (configureExisting)
                ConfigureCanvas(canvas, referenceResolution, screenMatchMode, matchWidthOrHeight);

            return canvas;
        }

        public static Canvas CreateCanvas(
            string objectName,
            Vector2 referenceResolution,
            CanvasScaler.ScreenMatchMode screenMatchMode,
            float matchWidthOrHeight = 0.5f)
        {
            GameObject canvasObject = new GameObject(
                objectName,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvas(canvas, referenceResolution, screenMatchMode, matchWidthOrHeight);
            return canvas;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(parent, false);
        }

        public static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Font font)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = ResolveFont(font);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateMenuButton(
            Transform parent,
            string label,
            UnityAction onClick,
            bool interactable,
            Font font)
        {
            GameObject buttonObject = CreateButtonObject(parent, label, onClick, interactable, 340f, 48f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.04f, 0.10f, 0.18f, 0.92f);
            colors.highlightedColor = new Color(0.04f, 0.48f, 0.78f, 0.96f);
            colors.pressedColor = new Color(0.52f, 0.08f, 0.42f, 0.96f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.08f, 0.08f, 0.11f, 0.76f);
            button.colors = colors;

            AddButtonLabel(buttonObject.transform, label, 18, font, Vector2.zero, Vector2.zero);
            return button;
        }

        public static Button CreatePanelButton(
            Transform parent,
            string label,
            UnityAction onClick,
            bool interactable,
            Font font,
            float preferredHeight,
            float horizontalLabelPadding = 0f)
        {
            GameObject buttonObject = CreateButtonObject(parent, label, onClick, interactable, -1f, preferredHeight);
            Image image = buttonObject.GetComponent<Image>();
            image.color = interactable ? PanelNormal : PanelInactive;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = PanelHighlighted;
            colors.pressedColor = PanelPressed;
            colors.disabledColor = PanelDisabled;
            button.colors = colors;

            AddButtonLabel(
                buttonObject.transform,
                label,
                16,
                font,
                new Vector2(horizontalLabelPadding, 0f),
                new Vector2(-horizontalLabelPadding, 0f));
            return button;
        }

        public static void CreateDivider(Transform parent, Color color)
        {
            GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.transform.SetParent(parent, false);
            divider.GetComponent<Image>().color = color;
            divider.GetComponent<LayoutElement>().preferredHeight = 1f;
        }

        private static GameObject CreateButtonObject(
            Transform parent,
            string label,
            UnityAction onClick,
            bool interactable,
            float preferredWidth,
            float preferredHeight)
        {
            GameObject buttonObject = new GameObject(
                label + "Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            if (preferredWidth >= 0f)
                layoutElement.preferredWidth = preferredWidth;
            layoutElement.preferredHeight = preferredHeight;

            Button button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            return buttonObject;
        }

        private static void ConfigureCanvas(
            Canvas canvas,
            Vector2 referenceResolution,
            CanvasScaler.ScreenMatchMode screenMatchMode,
            float matchWidthOrHeight)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = screenMatchMode;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        private static void AddButtonLabel(
            Transform parent,
            string label,
            int fontSize,
            Font font,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            Text labelText = CreateText("Label", parent, label, fontSize, TextAnchor.MiddleCenter, Color.white, font);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = offsetMin;
            labelRect.offsetMax = offsetMax;
        }
    }
}
