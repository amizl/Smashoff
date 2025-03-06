using System.Collections;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using UnityEngine;
using TMPro;
public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [SerializeField] private GameObject CellsDirectory; // Holds all grid cells
    [SerializeField] private GameObject UnitsDirectory; // Holds all grid cells
    [SerializeField] private GameObject[] unitPrefabs; // Tank, Jeep, Soldier prefabs
    [SerializeField] private GameObject lobbyRoomPanel;
    [SerializeField] private GameObject gameUI;
    [SerializeField] public TextMeshProUGUI MessageBoardTMP;

    private bool exitingGame = false; // Prevents multiple exits

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    private void Start()
    {
        // Register the event to detect new connections
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        // Disable MessageBoardTMP for this client when the game starts
        if (MessageBoardTMP != null)
        {
            MessageBoardTMP.gameObject.SetActive(false);
            Debug.Log($"[Client {NetworkManager.Singleton.LocalClientId}] MessageBoardTMP set to inactive on load.");
        }
    }
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected.");

        // Only the host should remove the waiting message
        if (IsServer && NetworkManager.Singleton.ConnectedClients.Count >= 2)
        {
            MessageBoardTMP.gameObject.SetActive(false);
            Debug.Log("Player 2 has joined. Removing waiting message from host.");
        }
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
            Debug.LogError("Player 1 can only spawn in first three columns");
            return;
        }
        else if (!isPlayer1 && !(col >= GridManager.Instance.columns - 3 && col < GridManager.Instance.columns))
        {
            Debug.LogError("Player 2 can only spawn in last three columns");
            return;
        }

        // Adjust resource cost based on column
        int additionalCost = AdjustedColumnCost(col, isPlayer1); // New!
        int baseCost = NetworkUnit.GetCost(type);
        int totalCost = baseCost + additionalCost;

        Player owner = isPlayer1 ? Player.Player1 : Player.Player2;
        if (!ResourceManager.Instance.SpendResources(owner, totalCost))
        {
            Debug.LogError($"Player {ownerClientId} has insufficient resources ({ResourceManager.Instance.GetResources(owner)} < {totalCost})");
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

    // New! Helper method for additional column cost
    private int AdjustedColumnCost(int col, bool isPlayer1)
    {
        if (isPlayer1)
        {
            if (col == 0) return 0;
            if (col == 1) return 1;
            if (col == 2) return 2;
        }
        else
        {
            int maxCol = GridManager.Instance.columns - 1;
            if (col == maxCol) return 0;
            if (col == maxCol - 1) return 1;
            if (col == maxCol - 2) return 2;
        }
        return 0; // default to 0, though logic should prevent reaching here
    }


    // Exit the game and clean up
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

        // Assuming the 'CurrentPlayer' is set correctly based on the player that clicked exit (e.g., Player1 or Player2)
        if (!TurnManager.Instance.CheckVictoryCondition())
        {
            string playerExiting = (TurnManager.Instance.CurrentPlayer == Player.Player1) ? "Player 1" : "Player 2";
            string opponent = (TurnManager.Instance.CurrentPlayer == Player.Player1) ? "Player 2" : "Player 1";

            // Log the server-side message for debugging
            Debug.Log($"{playerExiting} has chickened out! {opponent} wins by default. Exiting to lobby...");

            // Send the correct message to the opponent (Player 2 if Player 1 exits, and vice versa)
            InformOpponentOfChickenOutClientRpc(opponent);

            NetworkGameManager.Instance.MessageBoardTMP.text = $"{playerExiting} has chickened out! {opponent} wins by default. Exiting to lobby...";
        }
        if (IsServer)
        {
            // Server does the cleanup
            CleanUpUnitsServerRpc();
        }
        else
        {
            // Client leaves first, just disable units and cells locally and notify server
            CleanUpUnitsLocal();
            NotifyServerGameOverServerRpc();
        }

        StartCoroutine(DelayedLobbyTransition());
    }
    // ClientRpc to notify the opponent that the current player has chickened out
    [ClientRpc]
    private void InformOpponentOfChickenOutClientRpc(string opponent)
    {
        Debug.Log($"{opponent} has chickened out! You win by default. Exiting to lobby...");
        // You can show this message on the UI or display it as needed
    }
    private IEnumerator DelayedLobbyTransition()
    {
        yield return new WaitForSeconds(1f); // Wait for cleanup before transitioning to lobby
        Debug.Log("Lobby transition complete.");
    }

    // Server-side cleanup of networked units
    [ServerRpc(RequireOwnership = false)]
    private void CleanUpUnitsServerRpc()
    {
        Debug.Log("[Server] Starting unit cleanup...");

        if (CellsDirectory != null)
        {
            int count = CellsDirectory.transform.childCount;
            Debug.Log($"[Server] Cleaning up {count} Cells...");

            foreach (Transform child in CellsDirectory.transform)
            {
                    Destroy(child.gameObject); // Then destroy the object
            }
        }
        else
        {
            Debug.LogWarning("[Server] CellsDirectory is null, no cleanup performed.");
        }

        if (UnitsDirectory != null)
        {
            int count = UnitsDirectory.transform.childCount;
            Debug.Log($"[Server] Cleaning up {count} units...");

            foreach (Transform child in UnitsDirectory.transform)
            {
                NetworkObject netObj = child.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(); // Server despawns the networked object
                    Destroy(child.gameObject); // Then destroy the object
                }
            }
        }
        else
        {
            Debug.LogWarning("[Server] UnitsDirectory is null, no cleanup performed.");
        }
    }

    // Client-side cleanup of local units (non-networked)
    [ClientRpc]
    private void CleanUpUnitsClientRpc()
    {
        Debug.Log("[Client] Cleaning up local units...");
        CleanUpUnitsLocal();
    }


    private void CleanUpUnitsLocal()
    {
        // Cleanup Cells (no need to despawn, just destroy)
        if (CellsDirectory != null)
        {
            foreach (Transform child in CellsDirectory.transform)
            {
                Destroy(child.gameObject); // Cells are not network objects, just destroy them
            }
        }
        else
        {
            Debug.LogWarning("[Client] CellsDirectory is null on client.");
        }

        if (UnitsDirectory != null)
        {
            foreach (Transform child in UnitsDirectory.transform)
            {
                child.gameObject.SetActive(false); // Deactivate local units
            }
        }
        else
        {
            Debug.LogWarning("[Client] UnitsDirectory is null on client.");
        }
    }
    // Notify server when the client leaves
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerGameOverServerRpc()
    {
        // Tell the server that the game is over and perform cleanup
        Debug.Log("[Client] Notifying server that the game is over...");
        ExitToLobby();  
    }
    public void ExitGame()
    {
        Application.Quit();
    }
}
