using UnityEngine;
using TD.Core;

namespace TD.Enemies
{
    /// <summary>
    /// Advances an enemy's movement frames based on travelled world distance.
    /// This keeps the animation in sync with normal movement and slow effects.
    /// </summary>
    [RequireComponent(typeof(Enemy), typeof(SpriteRenderer))]
    public sealed class EnemyFrameAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] movementFrames;
        [SerializeField, Min(0.1f)] private float framesPerWorldUnit = 4f;

        private Vector3 lastPosition;
        private float frameProgress;
        private int currentFrame = -1;
        private bool needsPositionBaseline;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            frameProgress = 0f;
            needsPositionBaseline = true;
            ApplyFrame(0);
        }

        private void Update()
        {
            Vector3 currentPosition = transform.position;

            // PrefabPool activates reused instances before Spawn assigns their new
            // world position. Ignore that spawn teleport and start measuring only
            // after the instance has reached its actual spawn point.
            if (needsPositionBaseline)
            {
                lastPosition = currentPosition;
                needsPositionBaseline = false;
                return;
            }

            float travelledDistance = Vector3.Distance(currentPosition, lastPosition);
            lastPosition = currentPosition;

            if (!GameplayLifecycle.CanRunCombat || travelledDistance <= Mathf.Epsilon)
                return;

            frameProgress += travelledDistance * framesPerWorldUnit;
            ApplyFrame(Mathf.FloorToInt(frameProgress));
        }

        private void ApplyFrame(int frame)
        {
            if (targetRenderer == null || movementFrames == null || movementFrames.Length == 0)
                return;

            int nextFrame = frame % movementFrames.Length;
            if (nextFrame == currentFrame || movementFrames[nextFrame] == null)
                return;

            currentFrame = nextFrame;
            targetRenderer.sprite = movementFrames[currentFrame];
        }
    }
}
