using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

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

    [Header("Viewport References")]
    [SerializeField] private RectTransform gameplayViewport;
    [SerializeField] private SpriteRenderer viewportBackgroundRenderer;

    private Camera cam;

    private Vector2 lastPanPosition;
    private bool isPanning;
    private bool singleTouchActive;
    private bool touchGestureActive;
    private int gestureTouchId;
    private readonly HashSet<int> blockedTouches = new HashSet<int>();

    // Screen-space slop scaled for high-density Android displays. Painting commits on release.
    private const float TapMovementPixels = 12f;
    private const float MaxTapSeconds = 0.5f;
    private sealed class TapGesture
    {
        public bool active, cancelled;
        public Vector2 start;
        public float time;
        public PaintZone zone;
        public void Clear() { active = false; cancelled = false; zone = null; }
    }
    private readonly TapGesture mouseTap = new TapGesture();
    private readonly TapGesture touchTap = new TapGesture();
    private bool initialFitCompleted;
    private bool artworkBoundsReady;
    private bool viewportReferenceErrorReported;

    private readonly List<RaycastResult> uiRaycastResults =
        new List<RaycastResult>();

    private int fittedScreenWidth;
    private int fittedScreenHeight;
    private float fittedOrthographicSize;
    private Bounds artworkBounds;
    private float targetZoom, zoomVelocity;
    private Vector2 zoomAnchor;
    private bool hasZoomAnchor;
    private Coroutine focusRoutine;
    private int searchColor = -1, searchIndex;

    private const string ViewportBackgroundLayerName =
        "ViewportBackground";
    private const string ViewportBackgroundCameraName =
        "ViewportBackgroundCamera";

    private void Awake()
    {
        cam = GetComponent<Camera>();
        CreateBackgroundCamera();
        cam.pixelRect = Rect.zero;
        TryUpdateCameraViewport();
    }

    private void CreateBackgroundCamera()
    {
        int backgroundLayer = LayerMask.NameToLayer(
            ViewportBackgroundLayerName
        );

        if (backgroundLayer < 0)
        {
            Debug.LogError(
                $"CameraZoomAndPan: layer '{ViewportBackgroundLayerName}' " +
                $"is not configured. Scene: '{gameObject.scene.name}', " +
                $"object: '{name}'. Background camera was not created.",
                this
            );
            return;
        }

        CameraZoomAndPan[] controllers =
            FindObjectsByType<CameraZoomAndPan>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        int controllersInScene = 0;

        foreach (CameraZoomAndPan controller in controllers)
        {
            if (controller.gameObject.scene == gameObject.scene)
                controllersInScene++;
        }

        if (controllersInScene > 1)
        {
            Debug.LogWarning(
                $"CameraZoomAndPan: found {controllersInScene} controllers " +
                $"in scene '{gameObject.scene.name}'. Only one " +
                $"'{ViewportBackgroundCameraName}' will be used.",
                this
            );
        }

        SpriteRenderer backgroundRenderer =
            ResolveViewportBackgroundRenderer();

        if (backgroundRenderer == null)
            return;

        if (backgroundRenderer.gameObject.scene != gameObject.scene)
        {
            Debug.LogError(
                "CameraZoomAndPan: viewportBackgroundRenderer belongs to " +
                $"scene '{backgroundRenderer.gameObject.scene.name}', not " +
                $"'{gameObject.scene.name}'. Background camera was not " +
                "created.",
                this
            );
            return;
        }

        int backgroundMask = 1 << backgroundLayer;
        backgroundRenderer.gameObject.layer = backgroundLayer;
        cam.cullingMask &= ~backgroundMask;
        cam.clearFlags = CameraClearFlags.Depth;

        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        int existingCameraCount = 0;

        foreach (Camera camera in cameras)
        {
            if (camera.gameObject.scene == gameObject.scene &&
                camera.name == ViewportBackgroundCameraName)
            {
                existingCameraCount++;
            }
        }

        if (existingCameraCount > 0)
        {
            if (existingCameraCount > 1)
            {
                Debug.LogWarning(
                    $"CameraZoomAndPan: found {existingCameraCount} " +
                    $"'{ViewportBackgroundCameraName}' cameras in scene " +
                    $"'{gameObject.scene.name}'. No additional camera was " +
                    "created.",
                    this
                );
            }

            return;
        }

        GameObject backgroundCameraObject = new GameObject(
            ViewportBackgroundCameraName
        );
        SceneManager.MoveGameObjectToScene(
            backgroundCameraObject,
            gameObject.scene
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
    }

    private SpriteRenderer ResolveViewportBackgroundRenderer()
    {
        if (viewportBackgroundRenderer != null)
            return viewportBackgroundRenderer;

        Transform levelRoot = FindSceneRoot("LevelRoot");

        if (levelRoot == null)
        {
            Debug.LogError(
                $"CameraZoomAndPan: LevelRoot was not found in scene " +
                $"'{gameObject.scene.name}'. Assign " +
                "viewportBackgroundRenderer explicitly.",
                this
            );
            return null;
        }

        SpriteRenderer[] renderers =
            levelRoot.GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> candidates = new List<SpriteRenderer>();

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.name == "Background")
                candidates.Add(renderer);
        }

        if (candidates.Count != 1)
        {
            Debug.LogError(
                $"CameraZoomAndPan: expected exactly one Background " +
                $"SpriteRenderer inside LevelRoot in scene " +
                $"'{gameObject.scene.name}', but found {candidates.Count}. " +
                "Assign viewportBackgroundRenderer explicitly.",
                this
            );
            return null;
        }

        viewportBackgroundRenderer = candidates[0];
        return viewportBackgroundRenderer;
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
            {
                SelectColor.CreateSearchButton(this);
                yield break;
            }

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

        bool touchInput = HandleTouch();
        HandleMouse(touchInput);

    }


    private void HandleMouse(bool touchInput)
    {
        if (Mouse.current == null)
            return;

        // A touch-generated mouse stream must not become a second paint action.
        if (touchInput || touchGestureActive)
        {
            mouseTap.Clear();
            isPanning = false;
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        bool pointerBlocked = IsPointerBlockedForCamera(mousePosition, -1);

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (!pointerBlocked && Mathf.Abs(scroll) > 0.01f)
        {
            CancelFocus();
            ZoomCamera(-scroll * mouseZoomSpeed * .001f);
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelFocus();
            mouseTap.cancelled = true;
            lastPanPosition = mousePosition;
            isPanning = !pointerBlocked;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isPanning = false;
        }

        if (isPanning && Mouse.current.rightButton.isPressed)
        {
            if (!pointerBlocked)
                PanCamera(mousePosition);
            else
                isPanning = false;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && !Mouse.current.rightButton.isPressed)
            BeginTap(mouseTap, mousePosition, -1);
        if (mouseTap.active)
            TrackTap(mouseTap, mousePosition, -1);
        if (Mouse.current.leftButton.wasReleasedThisFrame)
            FinishTap(mouseTap, mousePosition, -1);
    }

    private bool HandleTouch()
    {
        if (Touchscreen.current == null)
            return false;

        TouchControl first = null, second = null, tracked = null;
        int count = 0;
        bool inputThisFrame = false;
        foreach (var touch in Touchscreen.current.touches)
        {
            int id = touch.touchId.ReadValue();
            if (touchGestureActive && id == gestureTouchId)
                tracked = touch;
            if (touch.press.wasPressedThisFrame)
            {
                CancelFocus();
                blockedTouches.Remove(id);
                if (IsPointerBlockedForCamera(touch.position.ReadValue(), id))
                    blockedTouches.Add(id);
            }
            inputThisFrame |= touch.press.isPressed || touch.press.wasReleasedThisFrame;
            if (!touch.press.isPressed)
                continue;
            if (count == 0) first = touch;
            if (count == 1) second = touch;
            count++;
        }

        if (!touchGestureActive && count > 0)
        {
            touchGestureActive = true;
            gestureTouchId = first.touchId.ReadValue();
            tracked = first;
            lastPanPosition = first.position.ReadValue();
            singleTouchActive = true;
            BeginTap(touchTap, lastPanPosition, gestureTouchId);
            if (!first.press.wasPressedThisFrame)
                touchTap.cancelled = true;
        }

        if (count > 1)
        {
            // Cancellation is sticky until every finger lifts, including the last finger after a pinch.
            touchTap.cancelled = true;
            singleTouchActive = false;
            Vector2 pos0 = first.position.ReadValue(), pos1 = second.position.ReadValue();
            if (count == 2 && !blockedTouches.Contains(first.touchId.ReadValue()) &&
                !blockedTouches.Contains(second.touchId.ReadValue()) &&
                !first.press.wasPressedThisFrame && !second.press.wasPressedThisFrame &&
                !IsPointerBlockedForCamera(pos0, first.touchId.ReadValue()) &&
                !IsPointerBlockedForCamera(pos1, second.touchId.ReadValue()))
            {
                float previous = Vector2.Distance(pos0 - first.delta.ReadValue(), pos1 - second.delta.ReadValue());
                float distance = Vector2.Distance(pos0, pos1);
                if (previous > 8f && distance > 8f)
                {
                    // Relative distance is independent of device DPI; damp fast changes.
                    float ratio = Mathf.Clamp(previous / distance, .85f, 1.18f);
                    targetZoom = Mathf.Clamp(targetZoom * Mathf.Pow(ratio, .55f), minZoom, fittedOrthographicSize);
                    zoomAnchor = (pos0 + pos1) * .5f;
                    hasZoomAnchor = true;
                }
            }
        }
        else if (count == 1)
        {
            Vector2 position = first.position.ReadValue();
            int id = first.touchId.ReadValue();
            if (id != gestureTouchId || blockedTouches.Contains(id))
                touchTap.cancelled = true;
            bool wasCancelled = touchTap.cancelled;
            TrackTap(touchTap, position, id);
            if (!wasCancelled && touchTap.cancelled)
                lastPanPosition = touchTap.start + (position-touchTap.start).normalized * Mathf.Sqrt(TapSlopSquared);
            if (!singleTouchActive)
            {
                lastPanPosition = position;
                singleTouchActive = true;
            }
            if (!blockedTouches.Contains(id) && !IsPointerBlockedForCamera(position, id) &&
                (touchTap.cancelled || (position - touchTap.start).sqrMagnitude > TapSlopSquared))
                PanCamera(position);
        }
        else if (touchGestureActive)
        {
            if (tracked != null && tracked.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended)
                FinishTap(touchTap, tracked.position.ReadValue(), gestureTouchId);
            touchTap.Clear();
            touchGestureActive = false;
            singleTouchActive = false;
            blockedTouches.Clear();
        }
        return inputThisFrame;
    }

    private float TapSlopSquared
    {
        get
        {
            float pixels = TapMovementPixels * Mathf.Max(1f, Screen.dpi / 160f);
            return pixels * pixels;
        }
    }

    private void BeginTap(TapGesture tap, Vector2 position, int pointerId)
    {
        tap.Clear();
        tap.active = true;
        tap.start = position;
        tap.time = Time.unscaledTime;
        tap.cancelled = IsPointerBlockedForCamera(position, pointerId);
        var controller = PaintController.Instance;
        if (tap.cancelled || controller == null || controller.zones == null)
            return;
        foreach (var zone in controller.zones)
        {
            if (zone == null || !zone.isActiveAndEnabled || zone.IsPainted ||
                zone.colorId != SelectColor.CurrentColorId)
                continue;
            if (HitsZone(zone, position, pointerId >= 0))
            {
                tap.zone = zone;
                break;
            }
        }
    }

    private void TrackTap(TapGesture tap, Vector2 position, int pointerId)
    {
        if (tap.active && ((position - tap.start).sqrMagnitude > TapSlopSquared ||
            IsPointerBlockedForCamera(position, pointerId)))
            tap.cancelled = true;
    }

    private void FinishTap(TapGesture tap, Vector2 position, int pointerId)
    {
        TrackTap(tap, position, pointerId);
        if (tap.active && !tap.cancelled && tap.zone != null &&
            Time.unscaledTime - tap.time <= MaxTapSeconds &&
            HitsZone(tap.zone, position, pointerId >= 0))
            tap.zone.Paint();
        tap.Clear();
    }

    private bool HitsZone(PaintZone zone, Vector2 screen, bool assist)
    {
        var collider = zone.HitCollider;
        if (collider == null || !collider.enabled || zone.IsPainted || zone.colorId != SelectColor.CurrentColorId) return false;
        Vector2 world = cam.ScreenToWorldPoint(screen);
        if (collider.OverlapPoint(world)) return true;
        if (!assist) return false;
        float radius = 12f * Mathf.Clamp(Screen.dpi / 160f, 1f, 3f);
        Vector2 nearest = cam.WorldToScreenPoint(collider.ClosestPoint(world));
        return (nearest - screen).sqrMagnitude <= radius * radius;
    }

    public bool FindSelectedColor()
    {
        var controller = PaintController.Instance;
        if (controller == null || controller.zones == null || controller.winPanel != null && controller.winPanel.activeInHierarchy) return false;
        int color = SelectColor.CurrentColorId;
        if (searchColor != color) { searchColor = color; searchIndex = 0; }
        foreach (var zone in controller.zones)
        {
            if (zone == null || zone.IsPainted || zone.colorId != color) continue;
            if (!zone.TryGetFocusPoint(searchIndex, out Vector2 point, out float size)) continue;
            searchIndex++;
            zone.SetHighlight(true);
            zone.StartHintPulse();
            CancelFocus();
            focusRoutine = StartCoroutine(FocusOn(point, Mathf.Min(cam.orthographicSize, Mathf.Clamp(size*1.25f,minZoom,fittedOrthographicSize))));
            return true;
        }
        return false;
    }

    private IEnumerator FocusOn(Vector2 point, float size)
    {
        Vector3 start = transform.position;
        Vector3 end = new Vector3(point.x, point.y, start.z);
        float initial = cam.orthographicSize;
        for (float t=0f;t<.4f;t+=Time.unscaledDeltaTime)
        {
            float f = Mathf.SmoothStep(0f,1f,t/.4f);
            cam.orthographicSize = Mathf.Lerp(initial,size,f);
            transform.position = Vector3.Lerp(start,end,f);
            ClampCameraPosition();
            targetZoom = cam.orthographicSize;
            yield return null;
        }
        cam.orthographicSize = targetZoom = size;
        transform.position = end; ClampCameraPosition();
        focusRoutine = null;
    }

    private void CancelFocus()
    {
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = null;
    }

    private void LateUpdate()
    {
        if (!initialFitCompleted || focusRoutine != null || Mathf.Abs(targetZoom-cam.orthographicSize)<.00001f) return;
        Vector3 anchorBefore = hasZoomAnchor ? cam.ScreenToWorldPoint(zoomAnchor) : Vector3.zero;
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize,targetZoom,ref zoomVelocity,.09f,Mathf.Infinity,Time.unscaledDeltaTime);
        if (hasZoomAnchor) transform.position += anchorBefore-cam.ScreenToWorldPoint(zoomAnchor);
        ClampCameraPosition();
    }

    private void OnDisable()
    {
        CancelFocus();
        mouseTap.Clear();
        touchTap.Clear();
        touchGestureActive = false;
        singleTouchActive = false;
        isPanning = false;
        blockedTouches.Clear();
        if (cam != null) targetZoom = cam.orthographicSize;
        zoomVelocity = 0f;
        hasZoomAnchor = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            OnDisable();
    }

    private bool IsPointerOverBlockingUi(
        Vector2 screenPosition,
        int pointerId
    )
    {
        if (PaintController.Instance != null && PaintController.Instance.winPanel != null &&
            PaintController.Instance.winPanel.activeInHierarchy)
            return true;
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
            // Ignore world/physics raycasters and the gameplay viewport container itself.
            if (result.module is UnityEngine.UI.GraphicRaycaster && result.gameObject.transform != gameplayViewport)
                return true;
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

        Transform levelRoot = FindSceneRoot("LevelRoot");
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
            if (!renderer.enabled || renderer is LineRenderer)
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

        float availableWidth = Mathf.Max(1f, cam.pixelWidth);
        float availableAspect =
            availableWidth / Mathf.Max(1f, cam.pixelHeight);

        float sizeForWidth =
            artworkBounds.size.x / (2f * availableAspect);
        float sizeForHeight = artworkBounds.size.y * 0.5f;

        float fittedSize = Mathf.Max(sizeForWidth, sizeForHeight) * 1.01f;

        cam.orthographicSize = fittedSize;
        targetZoom = fittedSize;
        zoomVelocity = 0f;
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

        if (gameplayViewport == null)
        {
            ReportViewportReferenceError(
                "GameplayViewport reference is not assigned"
            );
            ApplyFullScreenViewportFallback();
            return cam.pixelWidth > 0 && cam.pixelHeight > 0;
        }

        if (!TryGetClampedScreenRect(gameplayViewport, out Rect viewportRect))
        {
            ReportViewportReferenceError(
                "GameplayViewport produced an invalid screen Rect"
            );
            ApplyFullScreenViewportFallback();
            return cam.pixelWidth > 0 && cam.pixelHeight > 0;
        }

        cam.pixelRect = viewportRect;

        return cam.pixelWidth > 0 && cam.pixelHeight > 0;
    }

    private void ReportViewportReferenceError(string reason)
    {
        if (viewportReferenceErrorReported)
            return;

        Debug.LogError(
            $"CameraZoomAndPan: {reason}. Scene: " +
            $"'{gameObject.scene.name}', object: '{name}'. Falling back " +
            "to the full screen viewport.",
            this
        );
        viewportReferenceErrorReported = true;
    }

    private void ApplyFullScreenViewportFallback()
    {
        cam.pixelRect = new Rect(
            0f,
            0f,
            Mathf.Max(1f, Screen.width),
            Mathf.Max(1f, Screen.height)
        );
    }

    private bool TryGetClampedScreenRect(
        RectTransform rectTransform,
        out Rect screenRect
    )
    {
        screenRect = Rect.zero;
        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity;
        float top = float.NegativeInfinity;

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
            left = Mathf.Min(left, screenPoint.x);
            right = Mathf.Max(right, screenPoint.x);
            bottom = Mathf.Min(bottom, screenPoint.y);
            top = Mathf.Max(top, screenPoint.y);
        }

        if (float.IsInfinity(left) ||
            float.IsInfinity(right) ||
            float.IsInfinity(bottom) ||
            float.IsInfinity(top) ||
            float.IsNaN(left) ||
            float.IsNaN(right) ||
            float.IsNaN(bottom) ||
            float.IsNaN(top))
        {
            return false;
        }

        left = Mathf.Clamp(left, 0f, Screen.width);
        right = Mathf.Clamp(right, 0f, Screen.width);
        bottom = Mathf.Clamp(bottom, 0f, Screen.height);
        top = Mathf.Clamp(top, 0f, Screen.height);

        if (right - left <= 1f || top - bottom <= 1f)
            return false;

        screenRect = Rect.MinMaxRect(left, bottom, right, top);
        return true;
    }

    private Transform FindSceneRoot(string rootName)
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name == rootName)
                return root.transform;
        }

        return null;
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

        targetZoom = Mathf.Clamp(
            targetZoom + increment,
            minZoom,
            zoomOutLimit
        );

        hasZoomAnchor = false;
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
