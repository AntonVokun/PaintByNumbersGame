using System.Collections;
using UnityEngine;

public class PaintZone : MonoBehaviour
{
    public int colorId;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool painted = false;

    private Coroutine paintCoroutine;
    private Coroutine hintCoroutine;

    [SerializeField] private float paintAnimationTime = 0.25f;

    public bool IsPainted => painted;

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

        Color newColor = originalColor;
        newColor.a = value ? 0.55f : 0f;
        spriteRenderer.color = newColor;
    }

    public void Paint()
    {
        if (painted)
            return;

        if (SelectColor.CurrentColorId == -1)
            return;

        if (SelectColor.CurrentColorId != colorId)
            return;

        SetPaintedWithAnimation();

        SelectColor.ResetHintTimer();

        if (GameSound.Instance != null)
            GameSound.Instance.PlayClick();

        PaintController.Instance.ZonePainted(colorId);
    }

    public void SetPaintedInstant()
    {
        painted = true;

        if (paintCoroutine != null)
            StopCoroutine(paintCoroutine);

        if (hintCoroutine != null)
            StopCoroutine(hintCoroutine);

        if (spriteRenderer == null)
            return;

        Color paintedColor = originalColor;
        paintedColor.a = 1f;
        spriteRenderer.color = paintedColor;
    }

    private void SetPaintedWithAnimation()
    {
        painted = true;

        if (paintCoroutine != null)
            StopCoroutine(paintCoroutine);

        if (hintCoroutine != null)
            StopCoroutine(hintCoroutine);

        paintCoroutine = StartCoroutine(PaintAnimation());
    }

    private IEnumerator PaintAnimation()
    {
        float timer = 0f;

        Color startColor = originalColor;
        startColor.a = 0f;

        Color endColor = originalColor;
        endColor.a = 1f;

        spriteRenderer.color = startColor;

        while (timer < paintAnimationTime)
        {
            timer += Time.deltaTime;
            float progress = timer / paintAnimationTime;
            spriteRenderer.color = Color.Lerp(startColor, endColor, progress);
            yield return null;
        }

        spriteRenderer.color = endColor;
    }

    public void StartHintPulse()
    {
        if (painted || spriteRenderer == null)
            return;

        if (hintCoroutine != null)
            StopCoroutine(hintCoroutine);

        hintCoroutine = StartCoroutine(HintPulse());
    }

    public void StopHintPulse()
    {
        if (hintCoroutine != null)
        {
            StopCoroutine(hintCoroutine);
            hintCoroutine = null;
        }

        if (!painted && spriteRenderer != null)
        {
            Color hiddenColor = originalColor;
            hiddenColor.a = 0f;
            spriteRenderer.color = hiddenColor;
        }
    }

    private IEnumerator HintPulse()
    {
        while (!painted)
        {
            float alpha = Mathf.PingPong(Time.time * 2f, 0.35f) + 0.55f;

            Color pulseColor = originalColor;
            pulseColor.a = alpha;

            spriteRenderer.color = pulseColor;

            yield return null;
        }
    }

    public void ResetForReplay()
    {
        StopHintPulse();

        if (paintCoroutine != null)
            StopCoroutine(paintCoroutine);

        painted = true;

        Color hiddenColor = originalColor;
        hiddenColor.a = 0f;
        spriteRenderer.color = hiddenColor;
    }

    public void PaintForReplay()
    {
        if (paintCoroutine != null)
            StopCoroutine(paintCoroutine);

        paintCoroutine = StartCoroutine(PaintAnimation());
    }

    private void OnMouseDown()
    {
        Paint();
    }
}