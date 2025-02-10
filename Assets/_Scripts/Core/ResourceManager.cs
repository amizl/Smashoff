using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager Instance { get; private set; }

    private Dictionary<Player, NetworkVariable<int>> resources;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        resources = new Dictionary<Player, NetworkVariable<int>>
        {
            { Player.Player1, new NetworkVariable<int>(5) },
            { Player.Player2, new NetworkVariable<int>(5) }
        };
    }

    public int GetResources(Player player)
    {
        return resources[player].Value;
    }


    [ServerRpc(RequireOwnership = false)]
    public void AddResourcesServerRpc(Player player, int amount)
    {
        if (!IsServer) return;

        if (resources.ContainsKey(player))
        {
            resources[player].Value += amount;
            Debug.Log($"[ResourceManager] {player} gained {amount} resources. New total: {resources[player].Value}");

            // *** Broadcast to *all* clients now. ***
            UpdateResourceForPlayerClientRpc(resources[player].Value, player);
        }
    }

    [ClientRpc]
    private void UpdateResourceForPlayerClientRpc(int newResourceAmount, Player updatedPlayer)
    {
        // Figure out which player *I* am (Host == Player1, Client == Player2)
        Player localPlayer = (NetworkManager.Singleton.LocalClientId == 0)
            ? Player.Player1
            : Player.Player2;

        // If this update is for me, update my UI.
        if (localPlayer == updatedPlayer)
        {
            TurnManager.Instance.UpdateResourceUI(newResourceAmount);
        }
    }

    private ulong GetClientIdForPlayer(Player player)
    {
        return (ulong)(player == Player.Player1 ? 0 : 1); // Cast int to ulong
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the server sets up initial values
        if (IsServer)
        {
            // Each player starts with 5 resources, for example
            resources[Player.Player1].Value = 5;
            resources[Player.Player2].Value = 5;
        }
    }




    public bool SpendResources(Player player, int amount)
    {
        if (!IsServer) return false; // Only the server modifies resources

        if (resources[player].Value >= amount)
        {
            resources[player].Value -= amount;
            Debug.Log($"[ResourceManager] {player} spent {amount} resources. Remaining: {resources[player].Value}");
            return true;
        }
        return false;
    }
}
