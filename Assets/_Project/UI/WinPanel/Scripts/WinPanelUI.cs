using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WinPanelUI : MonoBehaviour
{
    public void LoadNextLevel()
    {
        PaintController controller = PaintController.Instance;

        if (controller == null)
            controller = FindFirstObjectByType<PaintController>(FindObjectsInactive.Include);

        if (controller != null)
        {
            controller.LoadNextLevel();
            return;
        }

        Debug.LogError("WinPanelUI: PaintController was not found.");
    }

    public void GoToMenu()
    {
        BackToMenu backToMenu = FindFirstObjectByType<BackToMenu>(FindObjectsInactive.Include);

        if (backToMenu != null)
        {
            backToMenu.GoToMenu();
            return;
        }

        SceneManager.LoadScene("MainMenu");
    }
}
