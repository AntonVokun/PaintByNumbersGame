using UnityEngine;
using UnityEngine.UI;

public class LevelPreviewCard : MonoBehaviour
{
    public string levelCompleteKey;

    public Image blackWhitePreview;
    public Image colorPreview;

    private void Start()
    {
        bool isComplete = PlayerPrefs.GetInt(levelCompleteKey, 0) == 1;

        blackWhitePreview.gameObject.SetActive(!isComplete);
        colorPreview.gameObject.SetActive(isComplete);
    }
}