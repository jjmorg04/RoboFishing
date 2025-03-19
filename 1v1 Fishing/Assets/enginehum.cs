using UnityEngine;

public class EngineHum : MonoBehaviour
{
    public AudioSource audioSource;

    void Start() {
        if (audioSource != null) {
            audioSource.Play();  // Play the attached audio clip on this AudioSource
        }
    }

    public void PlaySound() {
        if (audioSource != null) {
            audioSource.Play();
        }
    }
}