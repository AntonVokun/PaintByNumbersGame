using System.Collections;
using UnityEngine;

public class SelectColor : MonoBehaviour
{
    public int colorId;

    public static int CurrentColorId { get; private set; } = -1;

    private static SelectColor selectedButton;

    private Vector3 normalScale;
    private Vector3 selectedScale;

    private float hintTimer = 0f;
    private float hintDelay = 10f;
    private bool hintStarted = false;

    private void Awake()
    {
        normalScale = transform.localScale;
        selectedScale = normalScale * 1.2f;

        DisablePaletteLabels();
    }

    private void Update()
    {
        if (selectedButton != this)
            return;

        if (CurrentColorId == -1)
            return;

        if (hintStarted)
            return;

        hintTimer += Time.deltaTime;

        if (hintTimer >= hintDelay)
        {
            hintStarted = true;
            StartHint(CurrentColorId);
        }
    }

    private void DisablePaletteLabels()
    {
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
        {
            string typeName = component.GetType().FullName;
            if (typeName != null && typeName.StartsWith("TMPro."))
                component.gameObject.SetActive(false);
        }
    }

    public void Select()
    {
        if (GameSound.Instance != null)
            GameSound.Instance.PlayClick();

        if (selectedButton != null)
        {
            selectedButton.transform.localScale =
                selectedButton.normalScale;
        }

        StopAllHints();

        selectedButton = this;
        transform.localScale = selectedScale;

        CurrentColorId = colorId;

        hintTimer = 0f;
        hintStarted = false;

        HighlightZones(colorId);

        Debug.Log("Выбран цвет: " + colorId);
    }

    private void HighlightZones(int selectedColorId)
    {
        PaintZone[] zones = FindObjectsByType<PaintZone>(
            FindObjectsSortMode.None
        );

        foreach (PaintZone zone in zones)
        {
            if (zone.colorId == selectedColorId)
                zone.SetHighlight(true);
            else
                zone.SetHighlight(false);
        }
    }

    private void StartHint(int selectedColorId)
    {
        PaintZone[] zones = FindObjectsByType<PaintZone>(
            FindObjectsSortMode.None
        );

        foreach (PaintZone zone in zones)
        {
            if (zone.colorId == selectedColorId)
                zone.StartHintPulse();
        }
    }

    private void StopAllHints()
    {
        PaintZone[] zones = FindObjectsByType<PaintZone>(
            FindObjectsSortMode.None
        );

        foreach (PaintZone zone in zones)
        {
            zone.StopHintPulse();
        }
    }

    public static void ResetHintTimer()
    {
        if (selectedButton == null)
            return;

        selectedButton.hintTimer = 0f;
        selectedButton.hintStarted = false;

        selectedButton.StopAllHints();
        selectedButton.HighlightZones(CurrentColorId);
    }

    public void HideButton()
    {
        StartCoroutine(HideAnimation());
    }

    private IEnumerator HideAnimation()
    {
        Vector3 startScale = transform.localScale;
        Vector3 endScale = Vector3.zero;

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        float duration = 0.25f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float progress = timer / duration;

            transform.localScale =
                Vector3.Lerp(startScale, endScale, progress);

            canvasGroup.alpha =
                Mathf.Lerp(1f, 0f, progress);

            yield return null;
        }

        gameObject.SetActive(false);
    }
}