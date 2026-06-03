using UnityEngine;
using UnityEngine.UI;

public class SoundToggleButton : MonoBehaviour
{
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    private bool isSoundOn = true;

    private void Start()
    {
        isSoundOn = AudioListener.volume > 0f;
        UpdateIcon();
    }

    public void ToggleSound()
    {
        isSoundOn = !isSoundOn;
        AudioListener.volume = isSoundOn ? 1f : 0f;
        UpdateIcon();
    }

    private void UpdateIcon()
    {
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        buttonImage.sprite = isSoundOn ? soundOnSprite : soundOffSprite;
    }
}