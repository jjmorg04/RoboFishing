using System.Collections;
using Unity.Netcode;
using UnityEngine;
using StartGame;

public class Bobber : NetworkBehaviour
{
    public float waterHeight = 0.5f; // Water surface level
    public NetworkVariable<Vector3> rodTipPosition = new NetworkVariable<Vector3>(); // Sync rod tip position

    private Rigidbody rb;
    private LineRenderer lineRenderer;
    private Transform playerRodTip;
    private bool hasLanded = false; // Track if the bobber has stopped
    private bool fishBiting = false;
    private bool fishCaught = false;
    private float biteDelay;
    private StartGamePlayer ownerPlayer; // Reference to the player who owns this bobber
    private AudioSource bobberSound;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lineRenderer = GetComponent<LineRenderer>();

        if (IsServer)
        {
            rb.useGravity = true; // Enable physics
        }

        
        bobberSound = GetComponent<AudioSource>();
        
    }

    void Update()
    {
        // Keep the bobber floating at water level
        if (!hasLanded && transform.position.y <= waterHeight)
        {
            StopBobber();
        }

        // Continuously update the rod tip position so the line follows the player
        if (playerRodTip != null)
        {
            rodTipPosition.Value = playerRodTip.position;
        }

        // Ensure the line is drawn correctly for all players
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, rodTipPosition.Value); // Start of the line (rod tip)
            lineRenderer.SetPosition(1, transform.position);   // End of the line (bobber)
        }
    }

    void StopBobber()
    {
        hasLanded = true; // Mark as stopped
        rb.linearVelocity = Vector3.zero; // Stop movement
        rb.angularVelocity = Vector3.zero; // Stop rotation
        rb.useGravity = false; // Disable gravity to prevent sinking
        rb.isKinematic = true; // Prevent further physics interactions

        // Start the random fish bite timer
        if (IsServer)
        {
            biteDelay = Random.Range(3f, 10f);
            StartCoroutine(FishBiteCoroutine());
        }
    }

    IEnumerator FishBiteCoroutine()
    {
        yield return new WaitForSeconds(biteDelay);

        if (!fishCaught)
        {
            // Set fishBiting to true after a delay
            fishBiting = true;
            transform.position = new Vector3(transform.position.x, waterHeight - 0.3f, transform.position.z);
            if (bobberSound != null)
        {
            bobberSound.Play();  // Play the attached audio clip on this AudioSource
        }
            if (ownerPlayer != null)
            {
                ownerPlayer.ShowReelInPromptClientRpc(); // Call the method on the owner player to show the prompt
            }

            // Wait for the player to attempt to reel in
            yield return new WaitForSeconds(5f);

            if (!fishCaught)
            {
                // Only reset the bobber after the fishing process is finished
                ResetBobber();
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void AttemptCatchServerRpc()
    {
        Debug.Log($"[Server] Player {OwnerClientId} attempted to reel in.");

        if (!fishBiting)
        {
            Debug.LogError($"[Server] Player {OwnerClientId} tried to reel in, but no fish is biting!");
            return;
        }

        fishBiting = false;
        fishCaught = true;

        if (ownerPlayer != null)
        {
            int tapsRequired = Random.Range(10, 50); // Adjust difficulty
            ownerPlayer.StartCatchingMinigameClientRpc(tapsRequired); // Call the minigame on the owner player
            Debug.Log($"[Server] Started minigame for Player {ownerPlayer.OwnerClientId} with {tapsRequired} taps!");
        }
        else
        {
            Debug.LogError("[Server] AttemptCatchServerRpc() -> OwnerPlayer is NULL! Cannot start minigame.");
        }
    }

    void ResetBobber()
    {
        if (ownerPlayer != null)
        {
            // Only reset bobber when needed
            ownerPlayer.isFishing.Value = false; // Allow player to cast again
        }

        // Reset bobber position and flags
        transform.position = new Vector3(transform.position.x, waterHeight, transform.position.z);
        fishBiting = false;
        hasLanded = true;
        fishCaught = false;

        if (IsServer)
        {
            biteDelay = Random.Range(3f, 10f);
            StartCoroutine(FishBiteCoroutine());
        }
    }

    public void SetOwner(StartGamePlayer player)
    {
        ownerPlayer = player;

        if (ownerPlayer != null)
        {
            Debug.Log($"Bobber assigned to Player {ownerPlayer.OwnerClientId}");
        }
        else
        {
            Debug.LogError("SetOwner() -> OwnerPlayer is NULL! The bobber has no valid owner.");
        }
    }

    [Rpc(SendTo.Server)]
    public void SetRodTipServerRpc(NetworkObjectReference player, Vector3 rodTipPos)
    {
        rodTipPosition.Value = rodTipPos;

        // Find the player object and get their rod tip transform
        if (player.TryGet(out NetworkObject playerObject))
        {
            playerRodTip = playerObject.transform.Find("castPoint"); // Ensure castPoint exists on the player
        }
    }
}
