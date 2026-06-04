using UnityEngine;
using TD.Combat;

/// <summary>
/// Floating health bar that follows an IDamageable target. Renders two sprite
/// rectangles (background + fill) in world space, no UI canvas needed.
///
/// Visibility rule: hidden while the target is at full health, visible as soon
/// as it has taken any damage. Color blends from green (full) over yellow
/// (around 50%) to red (low).
///
/// Sharing a single 1x1 white sprite across all instances avoids per-bar
/// texture allocations.
/// </summary>

namespace TD.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.55f, 0f);
        [SerializeField] private Vector2 size = new Vector2(0.85f, 0.14f);
        [SerializeField] private float fillInsetRatio = 0.85f;
        [SerializeField] private int backgroundSortingOrder = 100;

        private IDamageable target;
        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer fillRenderer;
        private Transform fillTransform;

        private static Sprite cachedWhiteSprite;

        /// <summary>
        /// Attaches this bar to a target. Call once after spawning the bar.
        /// </summary>
        public void Bind(IDamageable damageableTarget)
        {
            target = damageableTarget;
            EnsureVisuals();
            gameObject.SetActive(true);
            UpdateVisuals();
        }

        public void Unbind()
        {
            target = null;
            if (backgroundRenderer != null)
                backgroundRenderer.enabled = false;
            if (fillRenderer != null)
                fillRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            if (target.IsDead)
            {
                if (backgroundRenderer != null)
                    backgroundRenderer.enabled = false;
                if (fillRenderer != null)
                    fillRenderer.enabled = false;
                return;
            }

            transform.position = target.WorldPosition + offset;
            UpdateVisuals();
        }

        private void EnsureVisuals()
        {
            if (backgroundRenderer != null && fillRenderer != null)
                return;

            Sprite sprite = GetOrCreateWhiteSprite();

            GameObject backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(transform, false);
            backgroundObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = sprite;
            backgroundRenderer.color = new Color(0f, 0f, 0f, 0.7f);
            backgroundRenderer.sortingOrder = backgroundSortingOrder;

            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(transform, false);
            fillTransform = fillObject.transform;
            fillRenderer = fillObject.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = sprite;
            fillRenderer.color = Color.green;
            fillRenderer.sortingOrder = backgroundSortingOrder + 1;
        }

        private void UpdateVisuals()
        {
            if (target == null || backgroundRenderer == null || fillRenderer == null)
                return;

            float maxHealth = target.MaxHealth;
            float ratio = maxHealth > 0f ? Mathf.Clamp01(target.CurrentHealth / maxHealth) : 0f;

            bool fullHealth = ratio >= 0.999f;
            backgroundRenderer.enabled = !fullHealth;
            fillRenderer.enabled = !fullHealth;

            if (fullHealth)
                return;

            // Anchor fill at the left edge of the background by scaling along x and
            // shifting half the missing width to the left.
            float fillWidth = size.x * ratio;
            fillTransform.localScale = new Vector3(fillWidth, size.y * fillInsetRatio, 1f);
            fillTransform.localPosition = new Vector3(-(size.x - fillWidth) * 0.5f, 0f, 0f);

            fillRenderer.color = ColorForRatio(ratio);
        }

        private static Color ColorForRatio(float ratio)
        {
            if (ratio > 0.5f)
                return Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);

            return Color.Lerp(Color.red, Color.yellow, ratio * 2f);
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            if (cachedWhiteSprite != null)
                return cachedWhiteSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            cachedWhiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            return cachedWhiteSprite;
        }

        /// <summary>
        /// Convenience factory: creates a child GameObject under <paramref name="parent"/>
        /// with a HealthBar component, ready to bind.
        /// </summary>
        public static HealthBar AttachTo(Transform parent)
        {
            GameObject barObject = new GameObject("HealthBar");
            barObject.transform.SetParent(parent, false);
            return barObject.AddComponent<HealthBar>();
        }
    }
}
