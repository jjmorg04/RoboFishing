using UnityEngine;

public class EngineHum : MonoBehaviour
{
    public AudioSource audioSource;  // Reference to AudioSource component attached to the GameObject

    void Start()
    {
        // Play the sound when the game starts
        if (audioSource != null)
        {
            audioSource.Play();  // Play the attached audio clip on this AudioSource
        }
        else
        {
            Debug.LogError("AudioSource is not assigned on " + gameObject.name);
        }
    }

    // Function to play the sound on command
    public void PlaySound()
    {
        if (audioSource != null)
        {
            audioSource.Play();  // Play the attached audio clip
        }
        else
        {
            Debug.LogError("AudioSource is not assigned on " + gameObject.name);
        }
    }
}