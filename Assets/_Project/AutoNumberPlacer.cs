using UnityEngine;
using TMPro;

public class AutoNumberCreator : MonoBehaviour
{
    public float textScale = 0.5f;
    public int sortingOrder = 1000;

    [ContextMenu("Создать цифры для всех зон")]
    public void CreateNumbers()
    {
        PaintZone[] zones = FindObjectsOfType<PaintZone>();

        foreach (PaintZone zone in zones)
        {
            SpriteRenderer sr = zone.GetComponent<SpriteRenderer>();
            if (sr == null)
                continue;

            // Удаляем старую цифру, если она уже была
            if (zone.numberText != null)
            {
                DestroyImmediate(zone.numberText.gameObject);
                zone.numberText = null;
            }

            GameObject textObj = new GameObject("NumberText");

            // Ставим цифру в центр видимой зоны
            Vector3 center = sr.bounds.center;
            center.z = -1f;
            textObj.transform.position = center;

            textObj.transform.SetParent(zone.transform, true);
            textObj.transform.localScale = Vector3.one * textScale;

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();

            tmp.text = zone.colorId.ToString();
            tmp.fontSize = 5;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;

            MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
            mr.sortingLayerName = sr.sortingLayerName;
            mr.sortingOrder = sortingOrder;

            zone.numberText = tmp;
        }

        Debug.Log("✅ Цифры пересозданы и поставлены в центр зон!");
    }
}