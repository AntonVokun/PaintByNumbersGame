using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

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