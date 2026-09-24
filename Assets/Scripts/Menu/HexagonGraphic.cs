using UnityEngine;
using UnityEngine.UI;

namespace TD.Menu
{
    [AddComponentMenu("")]
    public sealed class HexagonGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;

            vertexHelper.AddVert(center, color, new Vector2(0.5f, 0.5f));

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float normalizedY = direction.y / 0.8660254f;
                Vector2 position = center + new Vector2(direction.x * halfWidth, normalizedY * halfHeight);
                Vector2 uv = new Vector2((direction.x + 1f) * 0.5f, (normalizedY + 1f) * 0.5f);
                vertexHelper.AddVert(position, color, uv);
            }

            for (int i = 0; i < 6; i++)
                vertexHelper.AddTriangle(0, i + 1, ((i + 1) % 6) + 1);
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera))
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
                return false;

            Rect rect = rectTransform.rect;
            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            if (halfWidth <= 0f || halfHeight <= 0f)
                return false;

            Vector2 normalized = new Vector2(
                (localPoint.x - rect.center.x) / halfWidth,
                (localPoint.y - rect.center.y) / halfHeight);

            return ContainsNormalizedPoint(normalized);
        }

        internal static bool ContainsNormalizedPoint(Vector2 point)
        {
            float x = Mathf.Abs(point.x);
            float y = Mathf.Abs(point.y);
            return y <= 1f && x <= 1f - y * 0.5f;
        }
    }
}
