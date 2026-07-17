using System.Collections.Generic;
using UnityEngine;

public static class SaveSystem
{
    private const string SoundEnabledKey = "SoundEnabled";

    public static string LevelCompletedKey(string levelSceneName)
    {
        return levelSceneName + "_Completed";
    }

    public static string PaintedColorKey(string levelSceneName, int colorId)
    {
        return levelSceneName + "_Color_" + colorId + "_Painted";
    }

    public static string LevelColorIdsKey(string levelSceneName)
    {
        return levelSceneName + "_ColorIds";
    }

    public static string LevelResetPendingKey(string levelSceneName)
    {
        return levelSceneName + "_ResetPending";
    }

    public static bool IsLevelCompleted(string levelSceneName)
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
            return false;

        return PlayerPrefs.GetInt(LevelCompletedKey(levelSceneName), 0) == 1;
    }

    public static void SetLevelCompleted(string levelSceneName, bool completed)
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
            return;

        PlayerPrefs.SetInt(LevelCompletedKey(levelSceneName), completed ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool IsColorPainted(string levelSceneName, int colorId)
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
            return false;

        return PlayerPrefs.GetInt(PaintedColorKey(levelSceneName, colorId), 0) == 1;
    }

    public static void SetColorPainted(string levelSceneName, int colorId, bool painted)
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
            return;

        PlayerPrefs.SetInt(PaintedColorKey(levelSceneName, colorId), painted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool IsSoundEnabled()
    {
        return PlayerPrefs.GetInt(SoundEnabledKey, 1) == 1;
    }

    public static void SetSoundEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SoundEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void PrepareLevelProgress(string levelSceneName, IEnumerable<int> actualColorIds)
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
            return;

        int[] colorIds = NormalizeColorIds(actualColorIds);
        bool changed = false;

        if (PlayerPrefs.GetInt(LevelResetPendingKey(levelSceneName), 0) == 1)
        {
            PlayerPrefs.DeleteKey(LevelCompletedKey(levelSceneName));

            foreach (int colorId in colorIds)
                PlayerPrefs.DeleteKey(PaintedColorKey(levelSceneName, colorId));

            PlayerPrefs.DeleteKey(LevelResetPendingKey(levelSceneName));
            changed = true;
        }

        string serializedColorIds = string.Join(",", colorIds);
        string colorIdsKey = LevelColorIdsKey(levelSceneName);

        if (PlayerPrefs.GetString(colorIdsKey, string.Empty) != serializedColorIds)
        {
            PlayerPrefs.SetString(colorIdsKey, serializedColorIds);
            changed = true;
        }

        if (changed)
            PlayerPrefs.Save();
    }

    public static void ResetLevelProgress(string levelSceneName)
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
            return;

        PlayerPrefs.DeleteKey(LevelCompletedKey(levelSceneName));

        foreach (int colorId in GetRegisteredColorIds(levelSceneName))
            PlayerPrefs.DeleteKey(PaintedColorKey(levelSceneName, colorId));

        // Старые сохранения могли быть созданы до появления списка colorId.
        // Загруженная сцена удалит оставшиеся ключи по своим фактическим PaintZone
        // до восстановления прогресса.
        PlayerPrefs.SetInt(LevelResetPendingKey(levelSceneName), 1);

        PlayerPrefs.Save();
    }

    public static void ResetLevelsProgress(string[] levelSceneNames)
    {
        if (levelSceneNames == null)
            return;

        foreach (string levelSceneName in levelSceneNames)
            ResetLevelProgress(levelSceneName);
    }

    private static int[] GetRegisteredColorIds(string levelSceneName)
    {
        string serializedColorIds = PlayerPrefs.GetString(
            LevelColorIdsKey(levelSceneName),
            string.Empty
        );

        if (string.IsNullOrWhiteSpace(serializedColorIds))
            return new int[0];

        string[] parts = serializedColorIds.Split(',');
        List<int> colorIds = new List<int>(parts.Length);

        foreach (string part in parts)
        {
            if (int.TryParse(part, out int colorId))
                colorIds.Add(colorId);
        }

        return NormalizeColorIds(colorIds);
    }

    private static int[] NormalizeColorIds(IEnumerable<int> colorIds)
    {
        if (colorIds == null)
            return new int[0];

        HashSet<int> uniqueColorIds = new HashSet<int>();

        foreach (int colorId in colorIds)
            uniqueColorIds.Add(colorId);

        int[] result = new int[uniqueColorIds.Count];
        uniqueColorIds.CopyTo(result);
        System.Array.Sort(result);
        return result;
    }
}
