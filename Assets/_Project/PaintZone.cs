using UnityEngine;
using TMPro;

public class PaintZone : MonoBehaviour
{
    public int colorId;
    public TMP_Text numberText;

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

        TMP_Text[] allTexts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in allTexts)
        {
            text.text = colorId.ToString();
        }

        if (numberText == null && allTexts.Length > 0)
        {
            numberText = allTexts[0];
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

        TMP_Text[] allTexts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in allTexts)
        {
            text.gameObject.SetActive(false);
        }

        PaintController.Instance.ZonePainted(colorId);
    }

    private void OnMouseDown()
    {
        Paint();
    }
}