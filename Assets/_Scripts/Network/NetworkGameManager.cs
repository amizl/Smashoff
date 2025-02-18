using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [SerializeField] private GameObject CellsDirectory; // Holds all grid cells
    [SerializeField] private GameObject[] unitPrefabs; // Tank, Jeep, Soldier prefabs
    [SerializeField] private GameObject lobbyRoomPanel;
    [SerializeField] private GameObject gameUI;

    private bool exitingGame = false; // Prevents multiple exits

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnUnitServerRpc(UnitType type, Vector2Int position, ulong ownerClientId)
    {
        Debug.Log($"[Server] Spawning unit at {position} for Player {ownerClientId}");

        // Validate unit type
        if (type < 0 || type >= (UnitType)unitPrefabs.Length)
        {
            Debug.LogError($"Invalid unit type index: {type}");
            return;
        }

        GameObject unitPrefab = unitPrefabs[(int)type];
        if (unitPrefab == null)
        {
            Debug.LogError($"Unit prefab is null for type: {type}");
            return;
        }

        // Check if cell is occupied
        var cell = GridManager.Instance.GetCell(position.x, position.y);
        if (cell != null && cell.IsOccupied())
        {
            Debug.LogError("Cannot spawn unit on occupied cell");
            return;
        }

        // Check spawn area restrictions
        bool isPlayer1 = ownerClientId == 0;
        int col = position.x;

        if (isPlayer1 && !(col >= 0 && col <= 2))
        {
            Debug.LogError("Player 1 can only spawn in first and third columns");
            return;
        }
        else if (!isPlayer1 && !(col >= GridManager.Instance.columns - 3 && col < GridManager.Instance.columns))
        {
            Debug.LogError("Player 2 can only spawn in last and third-to-last columns");
            return;
        }

        // Spawn the unit
        GameObject unit = Instantiate(unitPrefab);
        unit.transform.position = GridManager.Instance.GetWorldPosition(position.x, position.y);

        NetworkObject networkObject = unit.GetComponent<NetworkObject>();
        networkObject.SpawnWithOwnership(ownerClientId);

        NetworkUnit networkUnit = unit.GetComponent<NetworkUnit>();
        networkUnit.gridPosition.Value = position; // Ensure position syncs
        networkUnit.InitializeServerRpc(type, position, ownerClientId);
    }

    public void ExitToLobby()
    {
        if (exitingGame) return; // Prevent multiple calls
        exitingGame = true;

        Debug.Log("Returning to Lobby...");

        // Disable game UI
        if (gameUI != null)
            gameUI.SetActive(false);

        // Enable lobby UI
        if (lobbyRoomPanel != null)
            lobbyRoomPanel.SetActive(true);

        // Cleanup on both Server and Clients
        if (IsServer)
        {
            CleanUpUnitsServerRpc();
        }

        CleanUpUnitsLocal(); // Clients clean up immediately

        StartCoroutine(DelayedLobbyTransition());
    }

    private IEnumerator DelayedLobbyTransition()
    {
        yield return new WaitForSeconds(1f); // Ensure cleanup before UI transition
        Debug.Log("Lobby transition complete.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void CleanUpUnitsServerRpc()
    {
        Debug.Log("[Server] Starting unit cleanup...");

        if (CellsDirectory != null)
        {
            int count = CellsDirectory.transform.childCount;
            Debug.Log($"[Server] Cleaning up {count} units...");

            foreach (Transform child in CellsDirectory.transform)
            {
                NetworkObject netObj = child.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn();

                Destroy(child.gameObject);
            }
        }
        else
        {
            Debug.LogWarning("[Server] CellsDirectory is null, no cleanup performed.");
        }

        // Notify all clients to clean up local objects
        CleanUpUnitsClientRpc();
    }

    [ClientRpc]
    private void CleanUpUnitsClientRpc()
    {
        Debug.Log("[Client] Cleaning up local units...");
        CleanUpUnitsLocal();
    }

    private void CleanUpUnitsLocal()
    {
        if (CellsDirectory != null)
        {
            foreach (Transform child in CellsDirectory.transform)
            {
                Destroy(child.gameObject); // Client-only cleanup
            }
        }
        else
        {
            Debug.LogWarning("[Client] CellsDirectory is null on client.");
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
