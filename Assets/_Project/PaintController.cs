using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PaintController : MonoBehaviour
{
    public static PaintController Instance;

    public PaintZone[] zones;
    public SelectColor[] paletteButtons;

    public GameObject winPanel;
    public string nextLevelSceneName = "Level_2";

    private Dictionary<int, int> remainingZones = new Dictionary<int, int>();
    private int totalZonesToPaint = 0;

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
    }

    private void CountZones()
    {
        remainingZones.Clear();
        totalZonesToPaint = 0;

        foreach (PaintZone zone in zones)
        {
            if (zone == null) continue;

            if (!remainingZones.ContainsKey(zone.colorId))
                remainingZones.Add(zone.colorId, 0);

            remainingZones[zone.colorId]++;
            totalZonesToPaint++;
        }

        Debug.Log($"📊 Загружено: {totalZonesToPaint} зон к покраске.");
    }

    public void ZonePainted(int colorId)
    {
        if (!remainingZones.ContainsKey(colorId))
            return;

        int paintedCount = remainingZones[colorId];

        totalZonesToPaint -= paintedCount;
        remainingZones[colorId] = 0;

        Debug.Log($"✅ Цвет {colorId} завершён. Осталось всего зон: {totalZonesToPaint}");

        HidePaletteButton(colorId);

        CheckLevelComplete();
    }

    private void HidePaletteButton(int colorId)
    {
        foreach (SelectColor button in paletteButtons)
        {
            if (button == null) continue;

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
            Debug.Log("🎉 УРОВЕНЬ ПРОЙДЕН!");

            if (winPanel != null)
                winPanel.SetActive(true);
        }
    }

    public void LoadNextLevel()
    {
        SceneManager.LoadScene(nextLevelSceneName);
    }
}