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

    public static void ResetLevelProgress(string levelSceneName, int colorsPerLevel)
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
            return;

        PlayerPrefs.DeleteKey(LevelCompletedKey(levelSceneName));

        for (int i = 1; i <= colorsPerLevel; i++)
        {
            PlayerPrefs.DeleteKey(PaintedColorKey(levelSceneName, i));
        }

        PlayerPrefs.Save();
    }

    public static void ResetLevelsProgress(string[] levelSceneNames, int colorsPerLevel)
    {
        if (levelSceneNames == null)
            return;

        foreach (string levelSceneName in levelSceneNames)
        {
            ResetLevelProgress(levelSceneName, colorsPerLevel);
        }
    }
}
