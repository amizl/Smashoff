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
        if (!IsServer) return; // Only the server modifies resources

        resources[player].Value += amount;
        Debug.Log($"[ResourceManager] {player} gained {amount} resources. New total: {resources[player].Value}");
    }

    public bool SpendResources(Player player, int amount)
    {
        if (!IsServer) return false; // Only the server modifies resources

        if (resources[player].Value >= amount)
        {
            resources[player].Value -= amount;
            return true;
        }
        return false;
    }
}
