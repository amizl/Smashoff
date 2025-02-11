using Unity.Netcode;
using UnityEngine;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager Instance { get; private set; }

    // Instead of a dictionary, use two dedicated NetworkVariables:
    private NetworkVariable<int> player1Resources = new NetworkVariable<int>(5);
    private NetworkVariable<int> player2Resources = new NetworkVariable<int>(5);

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // Helper to read the correct field
    public int GetResources(Player player)
    {
        if (player == Player.Player1)
            return player1Resources.Value;
        else
            return player2Resources.Value;
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddResourcesServerRpc(Player player, int amount)
    {
        if (!IsServer) return;

        // Update netvars for that player
        if (player == Player.Player1)
            player1Resources.Value += amount;
        else
            player2Resources.Value += amount;

        // Once done, call a client RPC so *all* clients update their UI
        UpdateResourceClientRpc();
    }

    // Tells *all* clients to refresh
    [ClientRpc]
    private void UpdateResourceClientRpc()
    {
        // NetVars now arrived => do final UI
        TurnManager.Instance.UpdateResourceUI();
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the server sets up (or resets) initial resource values
        if (IsServer)
        {
            player1Resources.Value = 5;
            player2Resources.Value = 5;
        }
    }

    public bool SpendResources(Player player, int amount)
    {
        if (!IsServer) return false; // Only the server modifies resources

        // Example: if player has enough, subtract
        if (GetResources(player) >= amount)
        {
            if (player == Player.Player1)
                player1Resources.Value -= amount;
            else
                player2Resources.Value -= amount;

            return true;
        }
        return false;
    }
}
