using UnityEngine;

public class EngineHum : MonoBehaviour
{
    public AudioSource audioSource;

    void Start() {
        if (audioSource != null) {
            audioSource.Play();
        }
    }

    public void PlaySound() {
        if (audioSource != null) {
            audioSource.Play();
        }
    }
}