using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PaintController : MonoBehaviour
{
    public static PaintController Instance;

    public PaintZone[] zones;
    public SelectColor[] paletteButtons;

    public GameObject winPanel;
    public string nextLevelSceneName = "Level_002";

    private Dictionary<int, int> remainingZones = new Dictionary<int, int>();
    private int totalZonesToPaint = 0;

    private bool levelCompleted = false;
    private bool waitingForTapToShowWinPanel = false;

    private string SceneName => SceneManager.GetActiveScene().name;
    private string PaintedColorKey(int colorId) => SceneName + "_Color_" + colorId + "_Painted";
    private string LevelCompletedKey => SceneName + "_Completed";

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        if (zones == null || zones.Length == 0)
            zones = FindObjectsByType<PaintZone>(FindObjectsSortMode.None);

        if (paletteButtons == null || paletteButtons.Length == 0)
            paletteButtons = FindObjectsByType<SelectColor>(FindObjectsSortMode.None);

        CountZones();
        LoadProgress();
    }

    private void Update()
    {
        if (!waitingForTapToShowWinPanel)
            return;

        if (Input.GetMouseButtonDown(0))
            ShowWinPanel();

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            ShowWinPanel();
    }

    private void OnGUI()
    {
        if (!waitingForTapToShowWinPanel)
            return;

        if (Event.current.type == EventType.MouseDown)
            ShowWinPanel();
    }

    private void ShowWinPanel()
    {
        waitingForTapToShowWinPanel = false;

        Debug.Log("✅ Показываем WinPanel.");

        if (winPanel != null)
            winPanel.SetActive(true);
        else
            Debug.LogError("❌ WinPanel не назначен в PaintController!");
    }

    private void CountZones()
    {
        remainingZones.Clear();
        totalZonesToPaint = 0;

        foreach (PaintZone zone in zones)
        {
            if (zone == null)
                continue;

            if (!remainingZones.ContainsKey(zone.colorId))
                remainingZones.Add(zone.colorId, 0);

            remainingZones[zone.colorId]++;
            totalZonesToPaint++;
        }

        Debug.Log($"📊 Загружено: {totalZonesToPaint} зон к покраске.");
    }

    private void LoadProgress()
    {
        foreach (PaintZone zone in zones)
        {
            if (zone == null)
                continue;

            if (PlayerPrefs.GetInt(PaintedColorKey(zone.colorId), 0) == 1)
            {
                zone.SetPaintedInstant();

                if (remainingZones.ContainsKey(zone.colorId))
                {
                    totalZonesToPaint -= remainingZones[zone.colorId];
                    remainingZones[zone.colorId] = 0;
                }

                HidePaletteButton(zone.colorId);
            }
        }

        if (PlayerPrefs.GetInt(LevelCompletedKey, 0) == 1)
        {
            levelCompleted = true;
            Debug.Log("✅ Этот уровень уже был пройден ранее.");
        }

        Debug.Log($"💾 Прогресс загружен. Осталось зон: {totalZonesToPaint}");
    }

    public void ZonePainted(int colorId)
    {
        if (levelCompleted)
            return;

        if (!remainingZones.ContainsKey(colorId))
            return;

        int paintedCount = remainingZones[colorId];

        totalZonesToPaint -= paintedCount;
        remainingZones[colorId] = 0;

        PlayerPrefs.SetInt(PaintedColorKey(colorId), 1);
        PlayerPrefs.Save();

        Debug.Log($"✅ Цвет {colorId} завершён и сохранён. Осталось всего зон: {totalZonesToPaint}");

        HidePaletteButton(colorId);

        CheckLevelComplete();
    }

    private void HidePaletteButton(int colorId)
    {
        foreach (SelectColor button in paletteButtons)
        {
            if (button == null)
                continue;

            if (button.colorId == colorId)
            {
                button.HideButton();
                break;
            }
        }
    }

    private void CheckLevelComplete()
    {
        if (totalZonesToPaint <= 0)
        {
            levelCompleted = true;

            PlayerPrefs.SetInt(LevelCompletedKey, 1);
            PlayerPrefs.Save();

            Debug.Log("🎉 УРОВЕНЬ ПРОЙДЕН И СОХРАНЁН!");

            StartCoroutine(ShowReplayThenWaitForTap());
        }
    }

    private IEnumerator ShowReplayThenWaitForTap()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        yield return new WaitForSeconds(0.5f);

        foreach (PaintZone zone in zones)
        {
            if (zone != null)
                zone.ResetForReplay();
        }

        yield return new WaitForSeconds(0.3f);

        foreach (PaintZone zone in zones)
        {
            if (zone != null)
            {
                zone.PaintForReplay();
                yield return new WaitForSeconds(0.08f);
            }
        }

        yield return new WaitForSeconds(1.2f);

        waitingForTapToShowWinPanel = true;

        Debug.Log("👆 Теперь можно нажать на экран.");
    }

    public void LoadNextLevel()
    {
        SceneManager.LoadScene(nextLevelSceneName);
    }

    public void ResetLevelProgress()
    {
        foreach (PaintZone zone in zones)
        {
            if (zone == null)
                continue;

            PlayerPrefs.DeleteKey(PaintedColorKey(zone.colorId));
        }

        PlayerPrefs.DeleteKey(LevelCompletedKey);
        PlayerPrefs.Save();

        SceneManager.LoadScene(SceneName);
    }
}