using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LevelCardUI : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private string levelSceneName = "Level_001";
    [SerializeField] private string levelDisplayName = "Уровень 1";

    [Header("UI")]
    [SerializeField] private TMP_Text levelNameText;
    [SerializeField] private Image previewImage;
    [SerializeField] private Button openButton;

    [Header("Preview Sprites")]
    [SerializeField] private Sprite blackWhitePreview;
    [SerializeField] private Sprite colorPreview;

    private void Start()
    {
        Refresh();

        if (openButton != null)
            openButton.onClick.AddListener(OpenLevel);
    }

    public void Refresh()
    {
        if (levelNameText != null)
            levelNameText.text = levelDisplayName;

        bool completed = SaveSystem.IsLevelCompleted(levelSceneName);

        if (previewImage != null)
            previewImage.sprite = completed ? colorPreview : blackWhitePreview;
    }

    public void OpenLevel()
    {
        if (string.IsNullOrWhiteSpace(levelSceneName))
        {
            Debug.LogError("❌ LevelCardUI: имя сцены не указано.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(levelSceneName))
        {
            SceneManager.LoadScene(levelSceneName);
        }
        else
        {
            Debug.LogError($"❌ Сцена '{levelSceneName}' не найдена в Build Settings.");
        }
    }
}
