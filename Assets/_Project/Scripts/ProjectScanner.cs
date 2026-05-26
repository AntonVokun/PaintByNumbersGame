using UnityEngine;
using System.Collections.Generic;

public class ProjectScanner : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("🔍 [SCANNER] Запускаю диагностику уровня...");

        PaintZone[] allZones = FindObjectsOfType<PaintZone>();
        Debug.Log($"📦 Всего найдено зон с компонентом PaintZone: {allZones.Length}");

        if (allZones.Length == 0)
        {
            Debug.LogError("❌ [SCANNER] Зоны не найдены! Убедись, что на всех кусочках картинки висит скрипт PaintZone.");
            return;
        }

        if (FindObjectOfType<PaintController>() == null)
            Debug.LogError("❌ [SCANNER] Не найден объект с PaintController! Счётчик прогресса не будет работать.");
        else
            Debug.Log("✅ [SCANNER] PaintController найден.");

        if (FindObjectOfType<SelectColor>() == null)
            Debug.LogError("❌ [SCANNER] Не найден объект с SelectColor! Палитра может не передавать цвет.");
        else
            Debug.Log("✅ [SCANNER] SelectColor найден.");

        Dictionary<int, int> idStats = new Dictionary<int, int>();
        int noCollider = 0;

        foreach (PaintZone zone in allZones)
        {
            if (idStats.ContainsKey(zone.colorId)) idStats[zone.colorId]++;
            else idStats.Add(zone.colorId, 1);

            if (zone.GetComponent<Collider2D>() == null) noCollider++;

            Debug.Log($"🧩 {zone.gameObject.name} | Цвет ID: {zone.colorId} | Клик: {(zone.GetComponent<Collider2D>() != null ? "Есть ✅" : "НЕТ ❌")}");
        }

        Debug.Log("📊 [SCANNER] СВОДКА ПО ЦВЕТАМ:");
        foreach (var item in idStats)
        {
            Debug.Log($"   🔹 Цвет #{item.Key}: {item.Value} зон(а/ы)");
        }

        if (noCollider > 0) Debug.LogError($"❌ Внимание: у {noCollider} зон(ы) нет Collider2D! По ним НЕЛЬЗЯ кликнуть. Добавь PolygonCollider2D!");

        Debug.Log("✅ [SCANNER] Диагностика завершена. Проверь красные сообщения выше!");
    }
}
