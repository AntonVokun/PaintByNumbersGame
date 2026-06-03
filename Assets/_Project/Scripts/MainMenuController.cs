using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("OLD LEVEL CARDS")]
    public Image level001Preview;
    public Sprite level001BlackWhiteSprite;
    public Sprite level001ColorSprite;

    public Image level002Preview;
    public Sprite level002BlackWhiteSprite;
    public Sprite level002ColorSprite;

    [Header("NEW LEVEL CARD SYSTEM")]
    [SerializeField] private LevelCardUI[] levelCards;

    [Header("Levels")]
    [SerializeField]
    private string[] levelSceneNames =
    {
        "Level_001",
        "Level_002_New"
    };

    [Header("Colors Per Level")]
    [SerializeField] private int colorsPerLevel = 16;

    private void Start()
    {
        UpdateOldCards();
        RefreshLevelCards();
    }

    private void UpdateOldCards()
    {
        bool level001Completed =
            SaveSystem.IsLevelCompleted("Level_001");

        bool level002Completed =
            SaveSystem.IsLevelCompleted("Level_002_New");

        if (level001Preview != null)
        {
            level001Preview.sprite =
                level001Completed
                ? level001ColorSprite
                : level001BlackWhiteSprite;
        }

        if (level002Preview != null)
        {
            level002Preview.sprite =
                level002Completed
                ? level002ColorSprite
                : level002BlackWhiteSprite;
        }
    }

    public void RefreshLevelCards()
    {
        if (levelCards == null)
            return;

        foreach (LevelCardUI card in levelCards)
        {
            if (card != null)
                card.Refresh();
        }
    }

    public void OpenLevel001()
    {
        LoadLevel("Level_001");
    }

    public void OpenLevel002()
    {
        LoadLevel("Level_002_New");
    }

    private void LoadLevel(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError(
                $"❌ Сцена '{sceneName}' не найдена в Build Settings."
            );
        }
    }

    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("🧹 Весь прогресс игры полностью сброшен.");

        UpdateOldCards();
        RefreshLevelCards();
    }
}