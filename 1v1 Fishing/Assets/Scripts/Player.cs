using System.Collections;
using UnityEngine;
using Unity.Netcode;
using TMPro;

namespace StartGame
{
    // player script
    public class StartGamePlayer : NetworkBehaviour {
        public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>(); // player's position
        public float speed = 5f; // player movement speed
        private Camera playerCamera; // player's camera object
        public GameObject bobberPrefab; // player's bobber object
        public Transform castPoint; // player's casting point
        public float castForce = 10f; // player's cast force
        public NetworkVariable<bool> isFishing = new NetworkVariable<bool>(false); // player fishing status
        private GameObject activeBobber = null; // active bobber placeholder
        public NetworkVariable<int> tapCount = new NetworkVariable<int>(0); // player's tap count to catch fish
        private int requiredTaps = 5; // minimum taps required to catch fish
        public GameObject fishObject; // fish object holder for catch
        public TMP_Text promptText; // text to display to user
        public NetworkVariable<int> playerScore = new NetworkVariable<int>(0); // was supposed to be for leaderboard, but no longer needed
        private AudioSource audioSource; // audio for player's reeling sound
        private bool isCatching = false; // player catching status

        void Start()
        {
            audioSource = GetComponent<AudioSource>(); // start audio on start
        }

        [Rpc(SendTo.Server)]
        public void RequestFishingStateChangeServerRpc(bool state) {
            if (IsServer) {
                isFishing.Value = state;  // update the fishing state to the server
            }
        }

        // when player spawns into server
        public override void OnNetworkSpawn() {
            Position.OnValueChanged += OnStateChanged;
            isFishing.OnValueChanged += OnFishingStateChanged;

            // meant to register player for leaderboard, but that's no longer needed
            if (IsServer) {
                Leaderboard.Instance.RegisterPlayer(OwnerClientId);
            }

            // get the player's camera object and assign to player
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null) {
                playerCamera.enabled = IsOwner;
            }

            // find the player's cast point
            castPoint = transform.Find("castPoint");

            // get player's prompt text object
            if (IsOwner) {
                GameObject promptTextObject = GameObject.Find("PromptText");
                if (promptTextObject != null) {
                    promptText = promptTextObject.GetComponent<TMP_Text>();
                }
            }
        }

        [ClientRpc]
        public void ShowReelInPromptClientRpc() {
            // send prompt to user to reel in with R
            if (promptText != null) {
                promptText.text = "Press R to Reel In!";
            }
        }

        [ClientRpc]
        public void StartCatchingMinigameClientRpc(int tapsRequired) {
            // begin minigame
            isCatching = true;
            
            // prompt player to spam space bar x times
            if (promptText != null) {
                requiredTaps = tapsRequired;
                tapCount.Value = 0;
                promptText.text = $"Press Space {requiredTaps} times to catch the fish!";
            }
        }

        [Rpc(SendTo.Server)]
        public void FinishCatchingServerRpc() {
            // player done catching, reset values
            isCatching = false;
            tapCount.Value = 0;
            playerScore.Value += Random.Range(50, 200); // was supposed to be used to give players points

            // use function to spawn fish in
            ShowCaughtFishClientRpc();

            // despawn bobber after
            if (activeBobber != null) {
                activeBobber.GetComponent<NetworkObject>().Despawn(true);
                Destroy(activeBobber);
                activeBobber = null;
            }

            // change state through server
            RequestFishingStateChangeServerRpc(false);

            // clear the prompt
            ClearPromptTextClientRpc();
        }

        // make changes on despawn from network
        public override void OnNetworkDespawn() {
            Position.OnValueChanged -= OnStateChanged;
            isFishing.OnValueChanged -= OnFishingStateChanged;
        }

        void Update() {
            if (IsOwner) {
                // on update, when not fishing, check for movement
                if (!isFishing.Value) {
                    HandleMovement();
                }
                // if player presses space, perform casting action
                if (Input.GetKeyDown(KeyCode.Space) && activeBobber == null) {
                    CastBobberServerRpc();
                }

                // if player has a bobber out, and presses R, use catching action
                if (Input.GetKeyDown(KeyCode.R) && activeBobber != null) {
                    activeBobber.GetComponent<Bobber>().AttemptCatchServerRpc();
                }

                // this handles the space taps to catch fish
                if (isCatching && Input.GetKeyDown(KeyCode.Space)) {
                    if (IsServer) {
                        tapCount.Value++;
                    }

                    // throw prompt at user to press x more times
                    if (promptText != null) {
                        promptText.text = $"Press Space {requiredTaps - tapCount.Value} times to catch the fish!";
                    }

                    // once user taps x times, end process
                    if (tapCount.Value >= requiredTaps) {
                        FinishCatchingServerRpc();
                    }
                }
            }
        }

        [Rpc(SendTo.Server)]
        void CastBobberServerRpc() {
            // player is fishing
            if (IsServer && bobberPrefab != null && castPoint != null && !isFishing.Value) {
                isFishing.Value = true;
                if (audioSource != null)
                {
                    // play reeling sound
                    audioSource.Play();
                }

                activeBobber = Instantiate(bobberPrefab, castPoint.position, Quaternion.identity);
                // spawn bobber when thrown, to server
                NetworkObject bobberNetObj = activeBobber.GetComponent<NetworkObject>();
                bobberNetObj.Spawn(true);

                // use bobbers rigidbody
                Rigidbody rb = activeBobber.GetComponent<Rigidbody>();
                rb.AddForce(transform.forward * castForce + Vector3.up * 2f, ForceMode.Impulse);

                // bobber gets the bobber script to perform tasks
                Bobber bobberScript = activeBobber.GetComponent<Bobber>();
                if (bobberScript != null) {
                    bobberScript.SetRodTipServerRpc(NetworkObject, castPoint.position);
                    bobberScript.SetOwner(this);
                }
            }
        }

        void HandleMovement()
        {
            
            // get player input, and transform player and cam movement accordingly
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            Vector3 forward = playerCamera.transform.forward;
            Vector3 right = playerCamera.transform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // movement determined by direction, speed, and time
            Vector3 movement = (forward * moveZ + right * moveX) * speed * Time.deltaTime;

            // submit movement to server to make changes across all players
            if (movement != Vector3.zero) {
                transform.position += movement;
                SubmitMovementRequestServerRpc(transform.position);
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitMovementRequestServerRpc(Vector3 newPosition) {
            // submit movement request to server to make change across players
            transform.position = newPosition;
            Position.Value = newPosition;
        }

        private void OnStateChanged(Vector3 previous, Vector3 current) {
            // when state changes, change position
            if (!IsOwner) {
                transform.position = Position.Value;
            }
        }

        // prevents overlapping actions
        private void OnFishingStateChanged(bool previous, bool current) {
            if (!IsServer) return;
            if (current == false && activeBobber != null) {
                return;
            }
        
        }

        [ClientRpc]
        public void ClearPromptTextClientRpc() {
            if (promptText != null) {
                promptText.text = ""; // clear prompt, send through client side
            }
        }

        [ClientRpc]
        public void ShowCaughtFishClientRpc() {
            StartCoroutine(ShowFishCoroutine()); // send function through Rpc for client side
        }

        // once fish is caught, spawn object for 5 seconds, then destroy it
        IEnumerator ShowFishCoroutine() {
            GameObject fish = Instantiate(fishObject, transform.position + new Vector3(0, 1, 0), Quaternion.identity);
            yield return new WaitForSeconds(5f);
            Destroy(fish);
            // fish will play a win sound when it spawns, but from a separate script
        }
    }
}
