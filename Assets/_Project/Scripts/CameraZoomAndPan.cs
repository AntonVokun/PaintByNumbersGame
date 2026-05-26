using UnityEngine;
using UnityEngine.InputSystem;

public class CameraZoomAndPan : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float minZoom = 2.5f;
    public float maxZoom = 11f;
    public float zoomSpeed = 0.03f;
    public float mouseZoomSpeed = 8f;

    [Header("Pan Settings")]
    public float panSpeed = 1f;

    [Header("Camera Bounds")]
    public bool useBounds = true;

    public float minX = -2.5f;
    public float maxX = 2.5f;

    public float minY = -4f;
    public float maxY = 4f;

    private Camera cam;

    private Vector2 lastPanPosition;
    private bool isPanning;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        HandleMouse();
        HandleTouch();
    }

    private void HandleMouse()
    {
        if (Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            ZoomCamera(-scroll * mouseZoomSpeed * Time.deltaTime);
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            lastPanPosition = mousePosition;
            isPanning = true;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isPanning = false;
        }

        if (isPanning && Mouse.current.rightButton.isPressed)
        {
            PanCamera(mousePosition);
        }
    }

    private void HandleTouch()
    {
        if (Touchscreen.current == null)
            return;

        var touch0 = Touchscreen.current.touches[0];
        var touch1 = Touchscreen.current.touches[1];

        bool touch0Pressed = touch0.press.isPressed;
        bool touch1Pressed = touch1.press.isPressed;

        // Движение одним пальцем
        if (touch0Pressed && !touch1Pressed)
        {
            Vector2 position = touch0.position.ReadValue();

            if (touch0.press.wasPressedThisFrame)
            {
                lastPanPosition = position;
            }
            else
            {
                PanCamera(position);
            }
        }

        // Zoom двумя пальцами
        if (touch0Pressed && touch1Pressed)
        {
            Vector2 pos0 = touch0.position.ReadValue();
            Vector2 pos1 = touch1.position.ReadValue();

            Vector2 prevPos0 = pos0 - touch0.delta.ReadValue();
            Vector2 prevPos1 = pos1 - touch1.delta.ReadValue();

            float previousDistance =
                Vector2.Distance(prevPos0, prevPos1);

            float currentDistance =
                Vector2.Distance(pos0, pos1);

            float difference =
                previousDistance - currentDistance;

            ZoomCamera(difference * zoomSpeed);
        }
    }

    private void PanCamera(Vector2 newPanPosition)
    {
        Vector3 oldWorldPosition =
            cam.ScreenToWorldPoint(lastPanPosition);

        Vector3 newWorldPosition =
            cam.ScreenToWorldPoint(newPanPosition);

        Vector3 difference =
            oldWorldPosition - newWorldPosition;

        transform.position += difference * panSpeed;

        ClampCameraPosition();

        lastPanPosition = newPanPosition;
    }

    private void ZoomCamera(float increment)
    {
        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize + increment,
            minZoom,
            maxZoom
        );

        ClampCameraPosition();
    }

    private void ClampCameraPosition()
    {
        if (!useBounds)
            return;

        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }
}