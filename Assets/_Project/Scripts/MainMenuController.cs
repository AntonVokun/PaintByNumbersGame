using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Level 001")]
    public Image level001Preview;
    public Sprite level001BlackWhiteSprite;
    public Sprite level001ColorSprite;

    [Header("Level 002")]
    public Image level002Preview;
    public Sprite level002BlackWhiteSprite;
    public Sprite level002ColorSprite;

    [Header("Levels")]
    [SerializeField] private string[] levelSceneNames = { "Level_001", "Level_002" };

    [Header("Colors Per Level")]
    [SerializeField] private int colorsPerLevel = 16;

    private void Start()
    {
        UpdateLevelCards();
    }

    private void UpdateLevelCards()
    {
        bool level001Completed = PlayerPrefs.GetInt("Level_001_Completed", 0) == 1;
        bool level002Completed = PlayerPrefs.GetInt("Level_002_Completed", 0) == 1;

        if (level001Preview != null)
            level001Preview.sprite = level001Completed ? level001ColorSprite : level001BlackWhiteSprite;

        if (level002Preview != null)
            level002Preview.sprite = level002Completed ? level002ColorSprite : level002BlackWhiteSprite;
    }

    public void OpenLevel001()
    {
        LoadLevel("Level_001");
    }

    public void OpenLevel002()
    {
        LoadLevel("Level_002");
    }

    private void LoadLevel(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"❌ Сцена '{sceneName}' не найдена в Build Settings.");
        }
    }

    public void ResetProgress()
    {
        foreach (string levelName in levelSceneNames)
        {
            if (string.IsNullOrWhiteSpace(levelName))
                continue;

            PlayerPrefs.DeleteKey(levelName + "_Completed");

            for (int i = 1; i <= colorsPerLevel; i++)
            {
                PlayerPrefs.DeleteKey(levelName + "_Color_" + i + "_Painted");
            }
        }

        PlayerPrefs.Save();

        Debug.Log("🧹 Прогресс игры сброшен.");

        UpdateLevelCards();
    }
}
