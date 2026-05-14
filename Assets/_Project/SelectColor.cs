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
    }

    public void Select()
    {
        if (GameSound.Instance != null)
            GameSound.Instance.PlayClick();

        // Возвращаем прошлую кнопку к обычному размеру
        if (selectedButton != null)
        {
            selectedButton.transform.localScale =
                selectedButton.normalScale;
        }

        // Запоминаем новую выбранную кнопку
        selectedButton = this;

        // Увеличиваем выбранную кнопку
        transform.localScale = selectedScale;

        // Сохраняем выбранный цвет
        CurrentColorId = colorId;

        // Подсвечиваем нужные зоны
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