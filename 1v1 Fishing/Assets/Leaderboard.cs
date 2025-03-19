using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class Leaderboard : NetworkBehaviour
{
    // this was supposed to be the leaderboard script, but ended up scrapping it
    
    public static Leaderboard Instance;
    public TMP_Text leaderboardText;

    private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();

    void Awake() {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn() {
        if (IsServer) {
            RegisterPlayer(NetworkManager.Singleton.LocalClientId);
        }
        RequestLeaderboardUpdateServerRpc();
    }

    public void RegisterPlayer(ulong playerId) {
        if (IsServer && !playerScores.ContainsKey(playerId)) {
            playerScores[playerId] = 0;
            UpdateLeaderboardClientRpc(playerScores[NetworkManager.Singleton.LocalClientId], playerScores.ContainsKey(1) ? playerScores[1] : 0);
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdateScoreServerRpc(ulong playerId, int score) {
        if (IsServer && playerScores.ContainsKey(playerId)) {
            playerScores[playerId] += score;
            UpdateLeaderboardClientRpc(playerScores[NetworkManager.Singleton.LocalClientId], playerScores.ContainsKey(1) ? playerScores[1] : 0);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestLeaderboardUpdateServerRpc() {
        if (IsServer) {
            UpdateLeaderboardClientRpc(playerScores[NetworkManager.Singleton.LocalClientId], playerScores.ContainsKey(1) ? playerScores[1] : 0);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateLeaderboardClientRpc(int hostScore, int clientScore) {
        if (leaderboardText == null) return;

        leaderboardText.text = "----------------\n";
        leaderboardText.text += $"Host: {hostScore} pts\n";
        leaderboardText.text += $"Client: {clientScore} pts\n";
    }
}
