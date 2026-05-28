using UnityEngine;

public class GameSound : MonoBehaviour
{
    public static GameSound Instance { get; private set; }

    public AudioSource audioSource;
    public AudioClip clickSound;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("⚠️ Найден лишний GameSound: " + gameObject.name);
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

    public void PlayClick()
    {
        if (!enabled)
            return;

        if (audioSource == null)
        {
            Debug.LogWarning("⚠️ AudioSource не назначен в GameSound.");
            return;
        }

        if (clickSound == null)
        {
            Debug.LogWarning("⚠️ ClickSound не назначен в GameSound.");
            return;
        }

        audioSource.PlayOneShot(clickSound);
    }
}
