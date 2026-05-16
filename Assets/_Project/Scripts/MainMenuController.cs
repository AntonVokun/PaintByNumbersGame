using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public void OpenLevel001()
    {
        SceneManager.LoadScene("Level_001");
    }

    public void OpenLevel002()
    {
        SceneManager.LoadScene("Level_002");
    }

    public void MarkLevel001Complete()
    {
        PlayerPrefs.SetInt("Level_001_Complete", 1);
        PlayerPrefs.Save();
    }

    public void MarkLevel002Complete()
    {
        PlayerPrefs.SetInt("Level_002_Complete", 1);
        PlayerPrefs.Save();
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey("Level_001_Complete");
        PlayerPrefs.DeleteKey("Level_002_Complete");
        PlayerPrefs.Save();
    }
}