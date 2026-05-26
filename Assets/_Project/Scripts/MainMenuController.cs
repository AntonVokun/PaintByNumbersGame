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
        SceneManager.LoadScene("Level_001");
    }

    public void OpenLevel002()
    {
        SceneManager.LoadScene("Level_002");
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey("Level_001_Completed");
        PlayerPrefs.DeleteKey("Level_002_Completed");

        PlayerPrefs.DeleteKey("Level_001_Color_1_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_2_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_3_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_4_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_5_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_6_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_7_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_8_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_9_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_10_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_11_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_12_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_13_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_14_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_15_Painted");
        PlayerPrefs.DeleteKey("Level_001_Color_16_Painted");

        PlayerPrefs.DeleteKey("Level_002_Color_1_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_2_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_3_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_4_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_5_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_6_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_7_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_8_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_9_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_10_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_11_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_12_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_13_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_14_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_15_Painted");
        PlayerPrefs.DeleteKey("Level_002_Color_16_Painted");

        PlayerPrefs.Save();
        UpdateLevelCards();
    }
}