using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }
    
    [SerializeField] private GameObject[] unitPrefabs; // Tank, Jeep, Soldier prefabs with NetworkObject component
    
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
        GameObject unitPrefab = unitPrefabs[(int)type];
        GameObject unit = Instantiate(unitPrefab, GridManager.Instance.GetWorldPosition(position.x, position.y), Quaternion.identity);
        
        NetworkObject networkObject = unit.GetComponent<NetworkObject>();
        networkObject.SpawnWithOwnership(ownerClientId);
        
        NetworkUnit networkUnit = unit.GetComponent<NetworkUnit>();
        networkUnit.InitializeServerRpc(type, position);
    }
    public void ExitGame()
    {
        Application.Quit();
    }
}
