using UnityEngine;

public class SelectColor : MonoBehaviour
{
    public int colorId;

    public static int CurrentColorId { get; private set; } = -1;

    private static SelectColor selectedButton;

    private Vector3 normalScale;
    private Vector3 selectedScale;

    private void Awake()
    {
        normalScale = transform.localScale;
        selectedScale = normalScale * 1.2f;

        DisablePaletteLabels();
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

        selectedButton = this;

        transform.localScale = selectedScale;

        CurrentColorId = colorId;

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
            {
                zone.SetHighlight(true);
            }
            else
            {
                zone.SetHighlight(false);
            }
        }
    }

    public void HideButton()
    {
        gameObject.SetActive(false);
    }
}
