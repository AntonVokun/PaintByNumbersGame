using UnityEngine;

public class PaintZone : MonoBehaviour
{
    public int colorId;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool painted = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;

            Color hiddenColor = originalColor;
            hiddenColor.a = 0f;
            spriteRenderer.color = hiddenColor;
        }

        DisableZoneLabels();
    }

    private void DisableZoneLabels()
    {
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
        {
            string typeName = component.GetType().FullName;
            if (typeName != null && typeName.StartsWith("TMPro."))
                component.gameObject.SetActive(false);
        }
    }

    public void SetHighlight(bool value)
    {
        if (painted || spriteRenderer == null)
            return;

        if (value)
        {
            Color highlightColor = originalColor;
            highlightColor.a = 0.35f;
            spriteRenderer.color = highlightColor;
        }
        else
        {
            Color hiddenColor = originalColor;
            hiddenColor.a = 0f;
            spriteRenderer.color = hiddenColor;
        }
    }

    public void Paint()
    {
        if (painted)
            return;

        if (SelectColor.CurrentColorId == -1)
            return;

        if (SelectColor.CurrentColorId != colorId)
            return;

        Color paintColor = originalColor;
        paintColor.a = 1f;

        spriteRenderer.color = paintColor;
        painted = true;

        if (GameSound.Instance != null)
            GameSound.Instance.PlayClick();

        PaintController.Instance.ZonePainted(colorId);
    }

    private void OnMouseDown()
    {
        Paint();
    }
}
