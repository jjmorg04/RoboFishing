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
    private StartGamePlayer ownerPlayer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lineRenderer = GetComponent<LineRenderer>();

        if (IsServer)
        {
            rb.useGravity = true; // Enable physics
        }
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
        // Pull the bobber underwater
        fishBiting = true;
        transform.position = new Vector3(transform.position.x, waterHeight - 0.3f, transform.position.z);

        if (ownerPlayer != null)
        {
            // 🔹 Tell the player to start the catching minigame instead of "Press R to Reel In!"
            int requiredTaps = Random.Range(10, 50);
            ownerPlayer.StartCatchingMinigameClientRpc(requiredTaps);
        }

        // Wait 5 seconds for player to react
        yield return new WaitForSeconds(5f);

        if (!fishCaught)
        {
            // If the player failed to reel in, reset the bobber
            ResetBobber();
        }
    }
}


    [Rpc(SendTo.Server)]
public void AttemptCatchServerRpc()
{
    if (fishBiting)
    {
        fishBiting = false;
        fishCaught = true;

        if (ownerPlayer != null)
        {
            int tapsRequired = Random.Range(10, 50); // Adjust difficulty
            ownerPlayer.StartCatchingMinigameClientRpc(tapsRequired);
        }
    }
}


    void ResetBobber()
    {
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