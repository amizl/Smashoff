using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer LocalInstance { get; private set; }

    private NetworkVariable<int> resources = new NetworkVariable<int>(5);
    private NetworkVariable<bool> isMyTurn = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
            SetupLocalPlayer();
        }
    }

    private void SetupLocalPlayer()
    {
        if (IsHost)
        {
            isMyTurn.Value = true; // Host goes first
        }
    }

    [ServerRpc]
    public void SpendResourcesServerRpc(int amount)
    {
        if (resources.Value >= amount)
        {
            resources.Value -= amount;
        }
    }

    [ServerRpc]
    public void EndTurnServerRpc()
    {
        if (isMyTurn.Value)
        {
            isMyTurn.Value = false;
            // Find other player and set their turn to true
            foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player != this)
                {
                    player.isMyTurn.Value = true;
                    break;
                }
            }
            AddResourcesServerRpc(2); // Add resources at turn start
        }
    }

    [ServerRpc]
    private void AddResourcesServerRpc(int amount)
    {
        resources.Value += amount;
    }
}