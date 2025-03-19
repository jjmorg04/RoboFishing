using System.Collections;
using Unity.Netcode;
using UnityEngine;
using StartGame;

public class Bobber : NetworkBehaviour
{
    public float waterHeight = 0.5f; // water height for bobber to fall on
    public NetworkVariable<Vector3> rodTipPosition = new NetworkVariable<Vector3>(); // position of the tip of rods to be shared
    private Rigidbody rb; // rigib body variable
    private LineRenderer lineRenderer; // line renderer variable for fishing line
    private Transform playerRodTip; // transform for the players' rod tips
    private bool hasLanded = false; // if bobber landed at waterHeight
    private bool fishBiting = false; // if fish is biting aka start minigame
    private bool fishCaught = false; // if fish is caught yet
    private float biteDelay; // randomized fish bite timer
    private StartGamePlayer ownerPlayer; // player object for host/client
    private AudioSource bobberSound; // bobber under water sound
    
    // get rigidbody and line renderer for respective variables
    // when the bobber spawns and server is started, gravity is turned on for it to fall to waterHeight
    void Start() {
        rb = GetComponent<Rigidbody>();
        lineRenderer = GetComponent<LineRenderer>();
        
        if (IsServer) {
            rb.useGravity = true;
        }
        bobberSound = GetComponent<AudioSource>();
    }

    // keep checking if bobber needs to stop falling to plop in the water
    // keep updating the fishing rods' line(s)
    void Update() {
        
        if (!hasLanded && transform.position.y <= waterHeight) {
            StopBobber();
        }

        if (playerRodTip != null) {
            rodTipPosition.Value = playerRodTip.position;
        }

        if (lineRenderer != null) {
            lineRenderer.SetPosition(0, rodTipPosition.Value); // line starting at rod tip
            lineRenderer.SetPosition(1, transform.position); // line ending at bobber
        }
    }

    void StopBobber() {
        // bobber has landed
        hasLanded = true;
        // stop the bobber's movement
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        // bobber is set, set a quick bite timer, then start the fish bite process
        if (IsServer) {
            biteDelay = Random.Range(3f, 10f);
            StartCoroutine(FishBiteCoroutine());
        }
    }

    IEnumerator FishBiteCoroutine() {
        yield return new WaitForSeconds(biteDelay);

        if (!fishCaught) {
            // fish is on
            fishBiting = true;
            // move bobber down to act as fish pulling
            transform.position = new Vector3(transform.position.x, waterHeight - 0.3f, transform.position.z);
            // play a sound for bobber going under
            if (bobberSound != null) {
                bobberSound.Play();
            }
            // call prompt function to tell player to reel in 
            if (ownerPlayer != null) {
                ownerPlayer.ShowReelInPromptClientRpc();
            }

            // give player 5 seconds to start reeling
            yield return new WaitForSeconds(5f);
            
            // reset bobber if fish not caught
            if (!fishCaught) {
                ResetBobber();
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void AttemptCatchServerRpc()
    {
        // if 
        if (!fishBiting) {
            return;
        }

        fishBiting = false;
        fishCaught = true;

        if (ownerPlayer != null)
        {
            int tapsRequired = Random.Range(10, 50); // set a random number of space presses required for catch
            ownerPlayer.StartCatchingMinigameClientRpc(tapsRequired); // call the players minigame function
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
