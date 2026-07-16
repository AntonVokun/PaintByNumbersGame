using UnityEngine;
using UnityEngine.UI;

public class PaletteDragScroll : MonoBehaviour
{
    public RectTransform palette;
    public float minX = -300f;
    public float maxX = 0f;

    private ScrollRect scrollRect;

    private void Awake()
    {
        if (palette == null)
            return;

        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = gameObject.AddComponent<ScrollRect>();

        scrollRect.viewport = transform as RectTransform;
        scrollRect.content = palette;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 40f;

        HorizontalLayoutGroup layout = palette.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(40, 40, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
        }

        ContentSizeFitter fitter = palette.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = palette.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        palette.anchorMin = new Vector2(0f, 0.5f);
        palette.anchorMax = new Vector2(0f, 0.5f);
        palette.pivot = new Vector2(0f, 0.5f);

        Vector2 position = palette.anchoredPosition;
        position.x = 0f;
        palette.anchoredPosition = position;

        RefreshLayout();
    }

    public void RefreshLayout()
    {
        if (palette == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(palette);

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.horizontalNormalizedPosition =
                Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
        }
    }
}
