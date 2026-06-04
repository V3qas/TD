using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TD.Level
{
    [DisallowMultipleComponent]
    public class MapCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float keyboardPanSpeed = 6f;
        [SerializeField] private float mouseDragMultiplier = 1f;
        [SerializeField] private float smoothTime = 0.08f;
        [SerializeField] private bool enableKeyboardPan = true;
        [SerializeField] private bool enableMiddleMouseDrag = true;

        private MapCameraFrame frame;
        private Vector3 targetPosition;
        private Vector3 smoothVelocity;
        private Vector2 lastMouseScreenPosition;
        private bool hasFrame;
        private bool isDragging;

        private void Awake()
        {
            ResolveCamera();
        }

        private void LateUpdate()
        {
            if (!hasFrame || targetCamera == null || !frame.CanPan)
                return;

            Vector3 panDelta = GetKeyboardPanDelta() + GetMouseDragPanDelta();
            if (panDelta.sqrMagnitude > 0f)
                targetPosition = frame.ClampPosition(targetPosition + panDelta);

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            targetCamera.transform.position = Vector3.SmoothDamp(
                targetCamera.transform.position,
                targetPosition,
                ref smoothVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        public void Configure(Camera cameraReference, MapCameraFrame cameraFrame)
        {
            if (cameraReference != null)
                targetCamera = cameraReference;

            Configure(cameraFrame);
        }

        public void Configure(MapCameraFrame cameraFrame)
        {
            ResolveCamera();

            frame = cameraFrame;
            hasFrame = targetCamera != null && frame.IsValid;
            isDragging = false;
            smoothVelocity = Vector3.zero;

            if (!hasFrame)
                return;

            targetCamera.rect = frame.CameraRect;
            targetCamera.orthographicSize = frame.OrthographicSize;
            targetPosition = frame.ClampPosition(targetCamera.transform.position);
            targetCamera.transform.position = targetPosition;
        }

        private Vector3 GetKeyboardPanDelta()
        {
            if (!enableKeyboardPan || Keyboard.current == null || IsTextInputFocused())
                return Vector3.zero;

            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;

            if (input.sqrMagnitude > 1f)
                input.Normalize();

            return new Vector3(input.x, input.y, 0f) * keyboardPanSpeed * Time.unscaledDeltaTime;
        }

        private Vector3 GetMouseDragPanDelta()
        {
            if (!enableMiddleMouseDrag || Mouse.current == null)
                return Vector3.zero;

            Mouse mouse = Mouse.current;
            Vector2 mouseScreenPosition = mouse.position.ReadValue();

            if (mouse.middleButton.wasPressedThisFrame)
            {
                isDragging = CanStartDrag(mouseScreenPosition);
                lastMouseScreenPosition = mouseScreenPosition;
            }

            if (!mouse.middleButton.isPressed)
            {
                isDragging = false;
                return Vector3.zero;
            }

            if (!isDragging)
            {
                lastMouseScreenPosition = mouseScreenPosition;
                return Vector3.zero;
            }

            Vector2 mouseDelta = mouseScreenPosition - lastMouseScreenPosition;
            lastMouseScreenPosition = mouseScreenPosition;

            if (mouseDelta.sqrMagnitude <= 0.01f)
                return Vector3.zero;

            float pixelWidth = Mathf.Max(1f, targetCamera.pixelWidth);
            float pixelHeight = Mathf.Max(1f, targetCamera.pixelHeight);
            float worldUnitsPerPixelY = targetCamera.orthographicSize * 2f / pixelHeight;
            float worldUnitsPerPixelX = targetCamera.orthographicSize * 2f * frame.Aspect / pixelWidth;

            return new Vector3(
                -mouseDelta.x * worldUnitsPerPixelX,
                -mouseDelta.y * worldUnitsPerPixelY,
                0f) * mouseDragMultiplier;
        }

        private bool CanStartDrag(Vector2 mouseScreenPosition)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            return targetCamera != null && targetCamera.pixelRect.Contains(mouseScreenPosition);
        }

        private bool IsTextInputFocused()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
                return false;

            return EventSystem.current.currentSelectedGameObject.GetComponentInParent<InputField>() != null;
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            if (targetCamera == null)
                targetCamera = Camera.main;
        }
    }
}
