using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PaintController : MonoBehaviour
{
    public static PaintController Instance { get; private set; }

    public PaintZone[] zones;
    public SelectColor[] paletteButtons;

    public GameObject winPanel;
    public string nextLevelSceneName = "Level_002";

    private Dictionary<int, int> remainingZones = new Dictionary<int, int>();
    private int totalZonesToPaint = 0;

    private bool levelCompleted = false;
    private bool waitingForTapToShowWinPanel = false;
    private Coroutine replayCoroutine;

    private string SceneName => SceneManager.GetActiveScene().name;
    private string PaintedColorKey(int colorId) => SceneName + "_Color_" + colorId + "_Painted";
    private string LevelCompletedKey => SceneName + "_Completed";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("⚠️ В сцене найден лишний PaintController. Лишний объект отключён: " + gameObject.name);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (!enabled)
            return;

        if (winPanel != null)
            winPanel.SetActive(false);

        if (zones == null || zones.Length == 0)
            zones = FindObjectsByType<PaintZone>(FindObjectsSortMode.None);

        if (paletteButtons == null || paletteButtons.Length == 0)
            paletteButtons = FindObjectsByType<SelectColor>(FindObjectsSortMode.None);

        if (zones == null || zones.Length == 0)
            Debug.LogWarning("⚠️ На сцене не найдены PaintZone. Проверь зоны раскраски.");

        if (paletteButtons == null || paletteButtons.Length == 0)
            Debug.LogWarning("⚠️ На сцене не найдены кнопки палитры SelectColor.");

        CountZones();
        LoadProgress();
    }

    private void Update()
    {
        if (!waitingForTapToShowWinPanel)
            return;

        bool mouseClicked =
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;

        bool touched =
            Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        if (mouseClicked || touched)
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

        if (zones == null)
            return;

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
        if (zones == null)
            return;

        HashSet<int> loadedColors = new HashSet<int>();

        foreach (PaintZone zone in zones)
        {
            if (zone == null)
                continue;

            if (PlayerPrefs.GetInt(PaintedColorKey(zone.colorId), 0) == 1)
            {
                zone.SetPaintedInstant();

                if (!loadedColors.Contains(zone.colorId) && remainingZones.ContainsKey(zone.colorId))
                {
                    totalZonesToPaint -= remainingZones[zone.colorId];
                    remainingZones[zone.colorId] = 0;
                    loadedColors.Add(zone.colorId);
                }

                HidePaletteButton(zone.colorId);
            }
        }

        totalZonesToPaint = Mathf.Max(0, totalZonesToPaint);

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
        {
            Debug.LogWarning($"⚠️ Цвет {colorId} не найден в списке зон PaintController.");
            return;
        }

        int paintedCount = remainingZones[colorId];

        if (paintedCount <= 0)
            return;

        totalZonesToPaint -= paintedCount;
        totalZonesToPaint = Mathf.Max(0, totalZonesToPaint);
        remainingZones[colorId] = 0;

        PlayerPrefs.SetInt(PaintedColorKey(colorId), 1);
        PlayerPrefs.Save();

        Debug.Log($"✅ Цвет {colorId} завершён и сохранён. Осталось всего зон: {totalZonesToPaint}");

        HidePaletteButton(colorId);

        CheckLevelComplete();
    }

    private void HidePaletteButton(int colorId)
    {
        if (paletteButtons == null)
            return;

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
        if (levelCompleted)
            return;

        if (totalZonesToPaint <= 0)
        {
            levelCompleted = true;

            PlayerPrefs.SetInt(LevelCompletedKey, 1);
            PlayerPrefs.Save();

            Debug.Log("🎉 УРОВЕНЬ ПРОЙДЕН И СОХРАНЁН!");

            if (replayCoroutine != null)
                StopCoroutine(replayCoroutine);

            replayCoroutine = StartCoroutine(ShowReplayThenWaitForTap());
        }
    }

    private IEnumerator ShowReplayThenWaitForTap()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        yield return new WaitForSeconds(0.5f);

        if (zones != null)
        {
            foreach (PaintZone zone in zones)
            {
                if (zone != null)
                    zone.ResetForReplay();
            }
        }

        yield return new WaitForSeconds(0.3f);

        if (zones != null)
        {
            foreach (PaintZone zone in zones)
            {
                if (zone != null)
                {
                    zone.PaintForReplay();
                    yield return new WaitForSeconds(0.08f);
                }
            }
        }

        yield return new WaitForSeconds(1.2f);

        waitingForTapToShowWinPanel = true;

        Debug.Log("👆 Теперь можно нажать на экран.");
    }

    public void LoadNextLevel()
    {
        if (string.IsNullOrWhiteSpace(nextLevelSceneName))
        {
            Debug.LogError("❌ Имя следующей сцены не указано в PaintController.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(nextLevelSceneName))
        {
            SceneManager.LoadScene(nextLevelSceneName);
        }
        else
        {
            Debug.LogError($"❌ Сцена '{nextLevelSceneName}' не найдена в Build Settings. Добавь её в File → Build Profiles / Build Settings.");
        }
    }

    public void ResetLevelProgress()
    {
        if (zones != null)
        {
            foreach (PaintZone zone in zones)
            {
                if (zone == null)
                    continue;

                PlayerPrefs.DeleteKey(PaintedColorKey(zone.colorId));
            }
        }

        PlayerPrefs.DeleteKey(LevelCompletedKey);
        PlayerPrefs.Save();

        SceneManager.LoadScene(SceneName);
    }
}
