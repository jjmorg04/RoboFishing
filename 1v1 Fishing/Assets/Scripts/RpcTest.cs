using Unity.Netcode;
using UnityEngine;

public class RpcTest : NetworkBehaviour
{
   
   // this script was my follow along of the netcode tutorial
   
   public override void OnNetworkSpawn()
   {
       if (!IsServer && IsOwner) //Only send an RPC to the server on the client that owns the NetworkObject that owns this NetworkBehaviour instance
       {
           TestServerRpc(0, NetworkObjectId);
       }
   }

   [Rpc(SendTo.ClientsAndHost)]
   void TestClientRpc(int value, ulong sourceNetworkObjectId)
   {
       
       if (IsOwner) //Only send an RPC to the server on the client that owns the NetworkObject that owns this NetworkBehaviour instance
       {
           TestServerRpc(value + 1, sourceNetworkObjectId);
       }
   }

   [Rpc(SendTo.Server)]
   void TestServerRpc(int value, ulong sourceNetworkObjectId)
   {
       
       TestClientRpc(value, sourceNetworkObjectId);
   }
}
