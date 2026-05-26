using UnityEngine;

public class GameSound : MonoBehaviour
{
    public static GameSound Instance;

    public AudioSource audioSource;
    public AudioClip clickSound;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayClick()
    {
        if (audioSource == null)
            return;

        if (clickSound == null)
            return;

        audioSource.PlayOneShot(clickSound);
    }
}