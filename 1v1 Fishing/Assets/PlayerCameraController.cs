using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    public float sensitivity = 2f; // Mouse sensitivity
    public Transform playerBody;   // Assign the player's transform
    private float xRotation = 0f;  // Store up/down rotation

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // Lock cursor for FPS-style look
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        xRotation -= mouseY; // Invert Y-axis rotation for natural look
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Limit camera tilt

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Apply vertical rotation
        playerBody.Rotate(Vector3.up * mouseX); // Rotate player horizontally
    }
}

