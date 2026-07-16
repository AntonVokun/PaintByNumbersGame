using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    private bool touch0StartedOverUi;
    private bool touch1StartedOverUi;
    private bool singleTouchActive;
    private bool initialFitCompleted;
    private bool artworkBoundsReady;

    private readonly List<RaycastResult> uiRaycastResults =
        new List<RaycastResult>();

    private int fittedScreenWidth;
    private int fittedScreenHeight;
    private float fittedOrthographicSize;
    private Bounds artworkBounds;

    private const int BackgroundOnlyLayer = 31;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        CreateBackgroundCamera();
        cam.pixelRect = Rect.zero;
        TryUpdateCameraViewport();
    }

    private void CreateBackgroundCamera()
    {
        SpriteRenderer backgroundRenderer = null;
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(
            FindObjectsSortMode.None
        );

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.name == "Background")
            {
                backgroundRenderer = renderer;
                break;
            }
        }

        if (backgroundRenderer == null)
            return;

        int backgroundMask = 1 << BackgroundOnlyLayer;
        backgroundRenderer.gameObject.layer = BackgroundOnlyLayer;

        GameObject backgroundCameraObject = new GameObject(
            "ViewportBackgroundCamera"
        );
        backgroundCameraObject.transform.SetPositionAndRotation(
            transform.position,
            transform.rotation
        );

        Camera backgroundCamera = backgroundCameraObject.AddComponent<Camera>();
        backgroundCamera.CopyFrom(cam);
        backgroundCamera.cullingMask = backgroundMask;
        backgroundCamera.depth = cam.depth - 1f;
        backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.useOcclusionCulling = false;

        cam.cullingMask &= ~backgroundMask;
        cam.clearFlags = CameraClearFlags.Depth;
    }

    private IEnumerator Start()
    {
        const int warningAfterAttempts = 120;
        int attempts = 0;
        bool warningShown = false;

        while (!initialFitCompleted)
        {
            initialFitCompleted = TryFitArtworkToAvailableArea();

            if (initialFitCompleted)
                yield break;

            attempts++;

            if (!warningShown && attempts >= warningAfterAttempts)
            {
                Debug.LogWarning(
                    "CameraZoomAndPan: artwork could not be fitted after " +
                    $"{warningAfterAttempts} frames. Retrying until the " +
                    "level hierarchy and renderer bounds are ready."
                );
                warningShown = true;
            }

            yield return null;
        }
    }

    private void Update()
    {
        if (initialFitCompleted &&
            (Screen.width != fittedScreenWidth ||
             Screen.height != fittedScreenHeight))
        {
            TryFitArtworkToAvailableArea();
        }

        HandleMouse();
        HandleTouch();

    }


    private void HandleMouse()
    {
        if (Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        bool pointerBlocked = IsPointerBlockedForCamera(mousePosition, -1);

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (!pointerBlocked && Mathf.Abs(scroll) > 0.01f)
        {
            ZoomCamera(-scroll * mouseZoomSpeed * Time.deltaTime);
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            lastPanPosition = mousePosition;
            isPanning = !pointerBlocked;
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

        if (touch0.press.wasPressedThisFrame)
        {
            touch0StartedOverUi = IsPointerBlockedForCamera(
                touch0.position.ReadValue(),
                touch0.touchId.ReadValue()
            );
        }

        if (touch1.press.wasPressedThisFrame)
        {
            touch1StartedOverUi = IsPointerBlockedForCamera(
                touch1.position.ReadValue(),
                touch1.touchId.ReadValue()
            );
        }

        if (touch0Pressed && !touch1Pressed && !touch0StartedOverUi)
        {
            Vector2 position = touch0.position.ReadValue();

            if (!singleTouchActive || touch0.press.wasPressedThisFrame)
            {
                lastPanPosition = position;
                singleTouchActive = true;
            }
            else
            {
                PanCamera(position);
            }
        }
        else
        {
            singleTouchActive = false;
        }

        if (touch0Pressed && touch1Pressed &&
            !touch0StartedOverUi && !touch1StartedOverUi)
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

        if (touch0.press.wasReleasedThisFrame)
            touch0StartedOverUi = false;

        if (touch1.press.wasReleasedThisFrame)
            touch1StartedOverUi = false;
    }

    private bool IsPointerOverBlockingUi(
        Vector2 screenPosition,
        int pointerId
    )
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = screenPosition,
                pointerId = pointerId
            };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults)
        {
            Transform current = result.gameObject.transform;

            while (current != null)
            {
                if (current.name == "PaletteWindow" ||
                    current.name == "TopBar")
                {
                    return true;
                }

                current = current.parent;
            }
        }

        return false;
    }

    private bool IsPointerBlockedForCamera(
        Vector2 screenPosition,
        int pointerId
    )
    {
        return !cam.pixelRect.Contains(screenPosition) ||
            IsPointerOverBlockingUi(screenPosition, pointerId);
    }

    private bool TryFitArtworkToAvailableArea()
    {
        if (cam == null || Screen.width <= 0 || Screen.height <= 0)
            return false;

        if (!TryUpdateCameraViewport())
            return false;

        Transform levelRoot = GameObject.Find("LevelRoot")?.transform;
        Transform whitePaper =
            levelRoot != null
                ? levelRoot.Find("Background/WhitePaper")
                : null;
        Transform art =
            levelRoot != null
                ? levelRoot.Find("Art")
                : null;

        if (whitePaper == null || art == null)
            return false;

        Renderer paperRenderer = whitePaper.GetComponent<Renderer>();
        Renderer[] artRenderers =
            art.GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds measuredArtworkBounds = new Bounds();

        if (paperRenderer != null && paperRenderer.enabled)
        {
            measuredArtworkBounds = paperRenderer.bounds;
            hasBounds = true;
        }

        foreach (Renderer renderer in artRenderers)
        {
            if (!renderer.enabled)
                continue;

            if (!hasBounds)
            {
                measuredArtworkBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                measuredArtworkBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return false;

        artworkBounds = measuredArtworkBounds;
        artworkBoundsReady = true;

        float referenceScale = Screen.width / 1080f;
        float horizontalMargin = 40f * referenceScale;
        float availableWidth = Mathf.Max(
            1f,
            cam.pixelWidth - horizontalMargin * 2f
        );
        float availableAspect =
            availableWidth / Mathf.Max(1f, cam.pixelHeight);

        float sizeForWidth =
            artworkBounds.size.x / (2f * availableAspect);
        float sizeForHeight = artworkBounds.size.y * 0.5f;

        float fittedSize = Mathf.Max(sizeForWidth, sizeForHeight) * 1.01f;

        cam.orthographicSize = fittedSize;
        maxZoom = Mathf.Max(maxZoom, fittedSize);
        fittedOrthographicSize = fittedSize;

        Vector3 position = transform.position;
        position.x = artworkBounds.center.x;
        position.y = artworkBounds.center.y;
        transform.position = position;

        ClampCameraPosition();

        fittedScreenWidth = Screen.width;
        fittedScreenHeight = Screen.height;

        return true;
    }

    private bool TryUpdateCameraViewport()
    {
        Canvas.ForceUpdateCanvases();

        GameObject topBar = GameObject.Find("TopBar");
        GameObject paletteWindow = GameObject.Find("PaletteWindow");
        RectTransform topBarRect =
            topBar != null
                ? topBar.GetComponent<RectTransform>()
                : null;
        RectTransform paletteRect =
            paletteWindow != null
                ? paletteWindow.GetComponent<RectTransform>()
                : null;

        if (topBarRect == null || paletteRect == null)
            return false;

        if (!TryGetVisibleTopBarBottom(
                topBar,
                topBarRect,
                out float topBarBottom) ||
            !TryGetScreenVerticalEdges(
                paletteRect,
                out _,
                out float paletteTop))
        {
            return false;
        }

        float viewportBottom = Mathf.Clamp(
            paletteTop,
            0f,
            Screen.height
        );
        float viewportTop = Mathf.Clamp(
            topBarBottom,
            0f,
            Screen.height
        );
        float viewportHeight = viewportTop - viewportBottom;

        if (viewportHeight <= 1f)
            return false;

        cam.pixelRect = new Rect(
            0f,
            viewportBottom,
            Screen.width,
            viewportHeight
        );

        return cam.pixelWidth > 0 && cam.pixelHeight > 0;
    }

    private bool TryGetVisibleTopBarBottom(
        GameObject topBar,
        RectTransform fallbackRect,
        out float bottom
    )
    {
        Image topBarImage = null;
        Image[] images = topBar.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image.name == "TopBarImage" && image.sprite != null)
            {
                topBarImage = image;
                break;
            }
        }

        if (topBarImage == null)
        {
            return TryGetScreenVerticalEdges(
                fallbackRect,
                out bottom,
                out _
            );
        }

        Sprite sprite = topBarImage.sprite;
        if (sprite.rect.height <= 0f)
        {
            return TryGetScreenVerticalEdges(
                fallbackRect,
                out bottom,
                out _
            );
        }

        const float transparentBottomPixels = 335f;
        float normalizedBottom = Mathf.Clamp01(
            transparentBottomPixels / sprite.rect.height
        );
        Rect imageRect = topBarImage.rectTransform.rect;
        float localBottom = Mathf.Lerp(
            imageRect.yMin,
            imageRect.yMax,
            normalizedBottom
        );
        Vector3 worldBottom = topBarImage.rectTransform.TransformPoint(
            new Vector3(0f, localBottom, 0f)
        );

        Canvas canvas = topBarImage.canvas;
        Camera uiCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        bottom = RectTransformUtility.WorldToScreenPoint(
            uiCamera,
            worldBottom
        ).y;

        return !float.IsInfinity(bottom) && !float.IsNaN(bottom);
    }

    private bool TryGetScreenVerticalEdges(
        RectTransform rectTransform,
        out float bottom,
        out float top
    )
    {
        bottom = float.PositiveInfinity;
        top = float.NegativeInfinity;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera uiCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

        foreach (Vector3 corner in corners)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                corner
            );
            bottom = Mathf.Min(bottom, screenPoint.y);
            top = Mathf.Max(top, screenPoint.y);
        }

        return !float.IsInfinity(bottom) &&
            !float.IsInfinity(top) &&
            !float.IsNaN(bottom) &&
            !float.IsNaN(top);
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
        float zoomOutLimit = initialFitCompleted
            ? fittedOrthographicSize
            : maxZoom;

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize + increment,
            minZoom,
            zoomOutLimit
        );

        ClampCameraPosition();
    }

    private void ClampCameraPosition()
    {
        if (!useBounds || !artworkBoundsReady)
            return;

        Vector3 pos = transform.position;
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        if (artworkBounds.size.x > halfWidth * 2f)
        {
            pos.x = Mathf.Clamp(
                pos.x,
                artworkBounds.min.x + halfWidth,
                artworkBounds.max.x - halfWidth
            );
        }
        else
        {
            pos.x = artworkBounds.center.x;
        }

        if (artworkBounds.size.y > halfHeight * 2f)
        {
            pos.y = Mathf.Clamp(
                pos.y,
                artworkBounds.min.y + halfHeight,
                artworkBounds.max.y - halfHeight
            );
        }
        else
        {
            pos.y = artworkBounds.center.y;
        }

        transform.position = pos;
    }
}
