using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class ProjectScanner : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("🔍 [SCANNER] Запускаю диагностику уровня...");

        // 1. Ищем все зоны
        PaintZone[] allZones = FindObjectsOfType<PaintZone>();
        Debug.Log($"📦 Всего найдено зон с компонентом PaintZone: {allZones.Length}");

        if (allZones.Length == 0)
        {
            Debug.LogError("❌ [SCANNER] Зоны не найдены! Убедись, что на всех кусочках картинки висит скрипт PaintZone.");
            return;
        }

        // 2. Проверяем наличие менеджера и палитры
        if (FindObjectOfType<PaintController>() == null)
            Debug.LogError("❌ [SCANNER] Не найден объект с PaintController! Счётчик прогресса не будет работать.");
        else
            Debug.Log("✅ [SCANNER] PaintController найден.");

        if (FindObjectOfType<SelectColor>() == null)
            Debug.LogError("❌ [SCANNER] Не найден объект с SelectColor! Палитра может не передавать цвет.");
        else
            Debug.Log("✅ [SCANNER] SelectColor найден.");

        // 3. Детальный разбор каждой зоны
        Dictionary<int, int> idStats = new Dictionary<int, int>();
        int noText = 0;
        int noCollider = 0;

        foreach (PaintZone zone in allZones)
        {
            // Считаем, сколько зон на каждый ID
            if (idStats.ContainsKey(zone.colorId)) idStats[zone.colorId]++;
            else idStats.Add(zone.colorId, 1);

            // Проверяем текст номера
            if (zone.numberText == null) noText++;

            // Проверяем коллайдер (БЕЗ НЕГО КЛИК НЕ РАБОТАЕТ!)
            if (zone.GetComponent<Collider2D>() == null) noCollider++;

            // Логируем каждую зону (удобно для отладки)
            Debug.Log($"🧩 {zone.gameObject.name} | Цвет ID: {zone.colorId} | Номер: {(zone.numberText != null ? "Привязан ✅" : "Пусто ❌")} | Клик: {(zone.GetComponent<Collider2D>() != null ? "Есть ✅" : "НЕТ ❌")}");
        }

        // 4. Итоговая сводка
        Debug.Log("📊 [SCANNER] СВОДКА ПО ЦВЕТАМ:");
        foreach (var item in idStats)
        {
            Debug.Log($"   🔹 Цвет #{item.Key}: {item.Value} зон(а/ы)");
        }

        if (noText > 0) Debug.LogWarning($"⚠️ Внимание: у {noText} зон(ы) не привязан Text для цифры. Цифры не исчезнут!");
        if (noCollider > 0) Debug.LogError($"❌ Внимание: у {noCollider} зон(ы) нет Collider2D! По ним НЕЛЬЗЯ кликнуть. Добавь PolygonCollider2D!");

        Debug.Log("✅ [SCANNER] Диагностика завершена. Проверь красные и жёлтые сообщения выше!");
    }
}