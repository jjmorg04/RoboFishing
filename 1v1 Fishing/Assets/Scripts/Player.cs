using System.Collections;
using UnityEngine;
using Unity.Netcode;
using TMPro;

namespace StartGame
{
    public class StartGamePlayer : NetworkBehaviour
    {
        public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
        public float speed = 5f;
        private Camera playerCamera;

        public GameObject bobberPrefab;
        public Transform castPoint;
        public float castForce = 10f;
        public NetworkVariable<bool> isFishing = new NetworkVariable<bool>(false);
        private GameObject activeBobber = null;
        public NetworkVariable<int> tapCount = new NetworkVariable<int>(0);  // NetworkVariable for tap count
        private int requiredTaps = 5;
        public GameObject fishObject;
        public TMP_Text promptText;
        public NetworkVariable<int> playerScore = new NetworkVariable<int>(0);

        private AudioSource audioSource;
private bool isCatching = false; // Add this line to define the isCatching variable

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
        }

        [Rpc(SendTo.Server)]
public void RequestFishingStateChangeServerRpc(bool state)
{
    if (IsServer)
    {
        isFishing.Value = state;  // Update the fishing state on the server
    }
}


        public override void OnNetworkSpawn()
        {
            Position.OnValueChanged += OnStateChanged;
            isFishing.OnValueChanged += OnFishingStateChanged;

            if (IsServer)
            {
                Leaderboard.Instance.RegisterPlayer(OwnerClientId);
            }

            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                playerCamera.enabled = IsOwner;
            }

            castPoint = transform.Find("castPoint");
            if (castPoint == null)
            {
                Debug.LogError("CastPoint not found! Make sure it's a child of the Player.");
            }

            if (IsOwner)
            {
                GameObject promptTextObject = GameObject.Find("PromptText");
                if (promptTextObject != null)
                {
                    promptText = promptTextObject.GetComponent<TMP_Text>();
                }
                else
                {
                    Debug.LogError("PromptText not found! Make sure it exists in the scene.");
                }
            }
        }

        [ClientRpc]
        public void ShowReelInPromptClientRpc()
        {
            if (promptText != null)
            {
                promptText.text = "Press R to Reel In!";
            }
        }

        [ClientRpc]
        public void StartCatchingMinigameClientRpc(int tapsRequired)
        {
            isCatching = true;
            if (promptText != null)
            {
                requiredTaps = tapsRequired; // Set the required taps for this round
                tapCount.Value = 0; // Reset the tap count
                promptText.text = $"Press Space {requiredTaps} times to catch the fish!";
            }
            else


            {
                Debug.LogError("PromptText is not set in StartGamePlayer.");
            }
        }

        [Rpc(SendTo.Server)]
        public void FinishCatchingServerRpc()
        {
            isCatching = false;
            tapCount.Value = 0;
            playerScore.Value += Random.Range(50, 200);

            ShowCaughtFishClientRpc();

            if (activeBobber != null)
            {
                activeBobber.GetComponent<NetworkObject>().Despawn(true);
                Destroy(activeBobber);
                activeBobber = null;
            }

            RequestFishingStateChangeServerRpc(false);

            ClearPromptTextClientRpc();
        }

        public override void OnNetworkDespawn()
        {
            Position.OnValueChanged -= OnStateChanged;
            isFishing.OnValueChanged -= OnFishingStateChanged;
        }

        void Update()
        {
            if (IsOwner)
            {
                if (!isFishing.Value)
                {
                    HandleMovement();
                }

                if (Input.GetKeyDown(KeyCode.Space) && activeBobber == null)
                {
                    CastBobberServerRpc();
                }

                if (Input.GetKeyDown(KeyCode.R) && activeBobber != null)
                {
                    activeBobber.GetComponent<Bobber>().AttemptCatchServerRpc();
                }

                // Handle tapping for catching the fish
                if (isCatching && Input.GetKeyDown(KeyCode.Space))
                {
                    if (IsServer) // Only update tapCount on the server (host)
                    {
                        tapCount.Value++; // Update on server
                    }

                    if (promptText != null)
                    {
                        promptText.text = $"Press Space {requiredTaps - tapCount.Value} times to catch the fish!";
                    }

                    if (tapCount.Value >= requiredTaps)
                    {
                        FinishCatchingServerRpc();
                    }
                }
            }
        }

        [Rpc(SendTo.Server)]
        void CastBobberServerRpc()
        {
            if (IsServer && bobberPrefab != null && castPoint != null && !isFishing.Value)
            {
                isFishing.Value = true;
                if (audioSource != null)
                {
                    audioSource.Play();
                }

                activeBobber = Instantiate(bobberPrefab, castPoint.position, Quaternion.identity);
                NetworkObject bobberNetObj = activeBobber.GetComponent<NetworkObject>();
                bobberNetObj.Spawn(true);

                Rigidbody rb = activeBobber.GetComponent<Rigidbody>();
                rb.AddForce(transform.forward * castForce + Vector3.up * 2f, ForceMode.Impulse);

                Bobber bobberScript = activeBobber.GetComponent<Bobber>();
                if (bobberScript != null)
                {
                    bobberScript.SetRodTipServerRpc(NetworkObject, castPoint.position);
                    bobberScript.SetOwner(this);
                }
                else
                {
                    Debug.LogError("Bobber script not found on bobberPrefab!");
                }
            }
        }

        void HandleMovement()
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            Vector3 forward = playerCamera.transform.forward;
            Vector3 right = playerCamera.transform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 movement = (forward * moveZ + right * moveX) * speed * Time.deltaTime;

            if (movement != Vector3.zero)
            {
                transform.position += movement;
                SubmitMovementRequestServerRpc(transform.position);
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitMovementRequestServerRpc(Vector3 newPosition)
        {
            transform.position = newPosition;
            Position.Value = newPosition;
        }

        private void OnStateChanged(Vector3 previous, Vector3 current)
        {
            if (!IsOwner)
            {
                transform.position = Position.Value;
            }
        }

        private void OnFishingStateChanged(bool previous, bool current)
        {
            if (!IsServer) return;

            if (current == false && activeBobber != null)
            {
                Debug.Log("[Server] Delaying fishing state reset. Bobber is still active.");
                return;
            }
            else if (current == false)
            {
                Debug.Log("[Server] Fishing state reset after catch.");
            }
        }

        // New method to clear prompt text on all clients
        [ClientRpc]
        public void ClearPromptTextClientRpc()
        {
            if (promptText != null)
            {
                promptText.text = ""; // Clear the prompt text
            }
            else
            {
                Debug.LogError("PromptText is not set in StartGamePlayer.");
            }
        }

        [ClientRpc]
        public void ShowCaughtFishClientRpc()
        {
            StartCoroutine(ShowFishCoroutine());
        }

        IEnumerator ShowFishCoroutine()
        {
            GameObject fish = Instantiate(fishObject, transform.position + new Vector3(0, 1, 0), Quaternion.identity);
            yield return new WaitForSeconds(5f);
            Destroy(fish);
        }
    }
}
