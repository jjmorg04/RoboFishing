using UnityEngine;

public class Audio : MonoBehaviour
{
    public AudioSource audioSource;

    void Start()
    {
        if (audioSource != null) {
            audioSource.Play();
        }
    }

    public void PlaySound()
    {
        if (audioSource != null) {
            audioSource.Play();
        }
    }
}
