using UnityEngine;
using UnityEngine.UI;

public class SoundToggleButton : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    private const string SoundKey = "SoundEnabled";

    private bool soundEnabled = true;

    private void Start()
    {
        soundEnabled = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        ApplySoundState();
    }

    public void ToggleSound()
    {
        soundEnabled = !soundEnabled;

        PlayerPrefs.SetInt(SoundKey, soundEnabled ? 1 : 0);
        PlayerPrefs.Save();

        ApplySoundState();
    }

    private void ApplySoundState()
    {
        AudioListener.volume = soundEnabled ? 1f : 0f;

        if (iconImage != null)
        {
            iconImage.sprite = soundEnabled ? soundOnSprite : soundOffSprite;
        }
    }
}