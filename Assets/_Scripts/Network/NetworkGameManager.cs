using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }
    [SerializeField] private GameObject UnitsDirectory;
    [SerializeField] private GameObject[] unitPrefabs; // Tank, Jeep, Soldier prefabs

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
        if ((int)type < 0 || (int)type >= unitPrefabs.Length)
        {
            Debug.LogError("Invalid unit type index: " + (int)type);
            return;
        }

        GameObject unitPrefab = unitPrefabs[(int)type];
        if (unitPrefab == null)
        {
            Debug.LogError("Unit prefab is null for type: " + type);
            return;
        }

        // Check if cell is already occupied
        var cell = GridManager.Instance.GetCell(position.x, position.y);
        if (cell != null && cell.IsOccupied())
        {
            Debug.LogError("Cannot spawn unit on occupied cell");
            return;
        }

        // Check spawn area restrictions
        bool isPlayer1 = ownerClientId == 0;
        int col = position.x;

        // Player 1 can only spawn in columns 0 and 2
        if (isPlayer1 && !(col >= 0 && col <= 2))
        {
            Debug.LogError("Player 1 can only spawn in first and third columns");
            return;
        }
        // Player 2 can only spawn in last and third-to-last columns
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

    public void ExitGame()
    {
        Application.Quit();
    }
}