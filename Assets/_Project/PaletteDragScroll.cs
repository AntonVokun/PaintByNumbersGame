using UnityEngine;
using UnityEngine.EventSystems;

public class PaletteDragScroll : MonoBehaviour, IDragHandler
{
    public RectTransform palette;
    public float minX = -300f;
    public float maxX = 0f;

    public void OnDrag(PointerEventData eventData)
    {
        if (palette == null)
            return;

        Vector2 pos = palette.anchoredPosition;
        pos.x += eventData.delta.x;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);

        palette.anchoredPosition = pos;
    }
}