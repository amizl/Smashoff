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
        if (!isMyTurn.Value) return;

        isMyTurn.Value = false;

        NetworkPlayer nextPlayer = null;

        // Find the next player and assign their turn
        foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player != this)
            {
                nextPlayer = player;
                break;
            }
        }

        if (nextPlayer != null)
        {
            nextPlayer.isMyTurn.Value = true;
            nextPlayer.AddResourcesServerRpc(2); // Give new player resources
        }

        // **NEW: Sync turn updates to all clients**
        UpdateTurnClientRpc(nextPlayer != null ? nextPlayer.OwnerClientId : OwnerClientId);
    }

    [ClientRpc]
    private void UpdateTurnClientRpc(ulong newTurnClientId)
    {
        Debug.Log($"It is now Player {newTurnClientId}'s turn.");
    }


    [ServerRpc]
    private void AddResourcesServerRpc(int amount)
    {
        resources.Value += amount;
    }
}