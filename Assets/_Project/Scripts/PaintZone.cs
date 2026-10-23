using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaintZone : MonoBehaviour
{
    public int colorId;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool painted = false;

    private Coroutine paintCoroutine;
    private Coroutine hintCoroutine;
    private SpriteRenderer highlight;
    private List<ZoneFocusTargets.Target> targets;
    public Collider2D HitCollider { get; private set; }

    [SerializeField] private float paintAnimationTime = 0.25f;

    public bool IsPainted => painted;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        HitCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
        {
            Debug.LogError("❌ На объекте нет SpriteRenderer: " + gameObject.name);
            enabled = false;
            return;
        }

        originalColor = spriteRenderer.color;

        Color hiddenColor = originalColor;
        hiddenColor.a = 0f;
        spriteRenderer.color = hiddenColor;

        DisableZoneLabels();
    }

    private void DisableZoneLabels()
    {
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null)
                continue;

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
        if (value) EnsureHighlight();
        SetHintVisible(value);
    }

    public bool TryGetFocusPoint(int index, out Vector2 point, out float size)
    {
        point = transform.position; size = 0f;
        var polygon = HitCollider as PolygonCollider2D;
        if (polygon == null || !polygon.enabled) return false;
        if (targets == null) targets = ZoneFocusTargets.Build(polygon);
        if (targets.Count == 0) return false;
        int focusIndex = (index % targets.Count + targets.Count) % targets.Count;
        var target = targets[focusIndex];
        point = transform.TransformPoint(target.localPoint);
        size = target.size * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        return polygon.OverlapPoint(point);
    }

    private void EnsureHighlight()
    {
        if (highlight != null) return;
        var material = Resources.Load<Material>("SelectedZoneHighlight");
        if (material != null)
        {
            var go = new GameObject("SelectionHighlight");
            go.transform.SetParent(transform, false);
            highlight = go.AddComponent<SpriteRenderer>();
            highlight.sprite = spriteRenderer.sprite;
            highlight.flipX = spriteRenderer.flipX; highlight.flipY = spriteRenderer.flipY;
            highlight.sharedMaterial = material;
            highlight.sortingLayerID = spriteRenderer.sortingLayerID;
            highlight.sortingOrder = spriteRenderer.sortingOrder + 1;
        }
    }

    private void SetHintVisible(bool visible)
    {
        if (highlight != null) highlight.enabled = visible;
    }

    public void Paint()
    {
        if (!enabled)
            return;

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

        if (PaintController.Instance != null)
        {
            PaintController.Instance.ZonePainted(colorId);
        }
        else
        {
            Debug.LogError("❌ PaintController.Instance == null. На сцене отсутствует PaintController.");
        }
    }

    public void SetPaintedInstant()
    {
        painted = true;
        SetHintVisible(false);

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
        SetHintVisible(false);

        if (paintCoroutine != null)
            StopCoroutine(paintCoroutine);

        if (hintCoroutine != null)
            StopCoroutine(hintCoroutine);

        paintCoroutine = StartCoroutine(PaintAnimation());
    }

    private IEnumerator PaintAnimation()
    {
        if (spriteRenderer == null)
            yield break;

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

        SetHighlight(true);
        hintCoroutine = StartCoroutine(HintPulse());
    }

    public void StopHintPulse()
    {
        SetHintVisible(false);
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
            if (spriteRenderer == null)
                yield break;

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

        if (spriteRenderer == null)
            return;

        Color hiddenColor = originalColor;
        hiddenColor.a = 0f;
        spriteRenderer.color = hiddenColor;
    }

    public void PaintForReplay()
    {
        if (spriteRenderer == null)
            return;

        if (paintCoroutine != null)
            StopCoroutine(paintCoroutine);

        paintCoroutine = StartCoroutine(PaintAnimation());
    }

}
