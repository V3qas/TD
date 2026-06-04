using UnityEngine;

namespace TD.Towers
{
    public class RangeIndicator : MonoBehaviour
    {
        [SerializeField] private int segments = 96;
        [SerializeField] private float lineWidth = 0.04f;

        private LineRenderer lineRenderer;

        private void Awake()
        {
            EnsureLineRenderer();
            Hide();
        }

        public void Show(Vector3 center, float radius, Color color)
        {
            EnsureLineRenderer();

            transform.position = center;
            lineRenderer.positionCount = segments + 1;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            for (int segmentIndex = 0; segmentIndex <= segments; segmentIndex++)
            {
                float angle = (segmentIndex / (float)segments) * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                lineRenderer.SetPosition(segmentIndex, point);
            }

            lineRenderer.enabled = true;
        }

        public void Hide()
        {
            EnsureLineRenderer();
            lineRenderer.enabled = false;
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer != null)
                return;

            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = gameObject.AddComponent<LineRenderer>();

            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.sortingOrder = 50;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
