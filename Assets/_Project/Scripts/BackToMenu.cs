using UnityEngine;
using UnityEngine.SceneManagement;

public class BackToMenu : MonoBehaviour
{
    public void GoToMenu()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu")
            Application.Quit();
        else
            LevelTransition.Load("MainMenu");
    }
}
