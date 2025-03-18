using System.Collections;
using Unity.Netcode;
using UnityEngine;
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
        private NetworkVariable<bool> isFishing = new NetworkVariable<bool>(false);
        private GameObject activeBobber = null;
        private bool isCatching = false;
        private int tapCount = 0;
        private int requiredTaps = 5;
        public GameObject fishObject;
        public TMP_Text promptText;
        public NetworkVariable<int> playerScore = new NetworkVariable<int>(0);

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

                if (isCatching && Input.GetKeyDown(KeyCode.Space))
                {
                    tapCount++;
                    if (tapCount >= requiredTaps)
                    {
                        FinishCatchingServerRpc();
                    }
                }
            }
        }

        
[Rpc(SendTo.Server)]
void CastBobberServerRpc()
{
    if (IsServer && bobberPrefab != null && castPoint != null && !isFishing.Value) // ✅ Only the server modifies isFishing.Value
    {
        isFishing.Value = true;

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

        //UpdatePromptTextClientRpc("Waiting for a fish to bite... Press R to reel in.");
    }
}




        [Rpc(SendTo.ClientsAndHost)]
        public void ShowReelInPromptClientRpc()
        {
            if (promptText != null)
            {
                promptText.text = "Press R to Reel In!";
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
public void StartCatchingMinigameClientRpc(int tapsRequired)
{
    isCatching = true;
    tapCount = 0;
    requiredTaps = tapsRequired;
    //UpdatePromptText($"Press Space {requiredTaps} times to catch the fish!");
}


        [Rpc(SendTo.Server)]
        public void FinishCatchingServerRpc()
        {
            isCatching = false;
            tapCount = 0;
            playerScore.Value += Random.Range(50, 200);

            ShowCaughtFishClientRpc();

            if (activeBobber != null)
            {
                Destroy(activeBobber);
                activeBobber = null;
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
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
        void SubmitMovementRequestServerRpc(Vector3 newPosition, RpcParams rpcParams = default)
        {
            transform.position = newPosition;
            Position.Value = newPosition;
        }

        public void OnStateChanged(Vector3 previous, Vector3 current)
        {
            if (!IsOwner)
            {
                transform.position = Position.Value;
            }
        }

        private void OnFishingStateChanged(bool previous, bool current)
        {
            isFishing.Value = current;
        }
    }
} 