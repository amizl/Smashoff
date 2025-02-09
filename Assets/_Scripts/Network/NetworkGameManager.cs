using Unity.Multiplayer.Tools.NetStats;
using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
   
    public static NetworkGameManager Instance { get; private set; }
    [SerializeField] private GameObject UnitsDirectory;
    
    [SerializeField] private GameObject[] unitPrefabs; // Tank, Jeep, Soldier prefabs with NetworkObject component
    private Vector2Int selectedCell = new Vector2Int(-1, -1); // Default: No cell selected

    public void SetSelectedCell(int row, int col)
    {
        selectedCell = new Vector2Int(row, col);
    }

    public Vector2Int GetSelectedCell()
    {
        return selectedCell;
    }
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

        // Instantiate unit at selected cell position
        GameObject unit = Instantiate(unitPrefab, GridManager.Instance.GetWorldPosition(position.x, position.y), Quaternion.identity);

        // Get NetworkObject and spawn it
        NetworkObject networkObject = unit.GetComponent<NetworkObject>();
        networkObject.SpawnWithOwnership(ownerClientId);

        // Ensure Units GameObject exists before setting parent
        if (UnitsDirectory == null)
        {
            UnitsDirectory = new GameObject("Units");
        }

        // Send a ClientRpc to reparent it on clients
        unit.GetComponent<NetworkUnit>().RequestParentClientRpc(UnitsDirectory.transform.name);

        // Initialize unit on server
        NetworkUnit networkUnit = unit.GetComponent<NetworkUnit>();
        networkUnit.InitializeServerRpc(type, position, ownerClientId);



        Debug.Log($"Spawned {type} at {position}, Parent: {unit.transform.parent}");
    }


    public void ExitGame()
    {
        Application.Quit();
    }
}
