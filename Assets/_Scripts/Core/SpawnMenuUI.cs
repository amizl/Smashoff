using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SpawnMenuUI : MonoBehaviour
{
    [SerializeField] private SpawnMenuUI spawnMenuUI;
    [SerializeField] private Button spawnTankButton;
    [SerializeField] private Button spawnJeepButton;
    [SerializeField] private Button spawnSoldierButton;
    [SerializeField] private VerticalLayoutGroup verticalLayoutGroup;
    private void Start()
    {
        // If this is NOT the host => we assume it's Player 2
        if (!NetworkManager.Singleton.IsHost)
        {
            verticalLayoutGroup.childAlignment = TextAnchor.UpperRight;
        }
        else
        {
            // If you want to ensure Player 1 is always UpperLeft:
            verticalLayoutGroup.childAlignment = TextAnchor.UpperLeft;
        }
    }
    private void Awake()
    {
        spawnTankButton.onClick.AddListener(() => SpawnUnit(UnitType.Tank));
        spawnJeepButton.onClick.AddListener(() => SpawnUnit(UnitType.Jeep));
        spawnSoldierButton.onClick.AddListener(() => SpawnUnit(UnitType.Soldier));
    }

    private void SpawnUnit(UnitType type)
    {
        Debug.Log("ConnectedClients.Count=" + NetworkManager.Singleton.ConnectedClients.Count);
        if (NetworkManager.Singleton.ConnectedClients.Count <2) 
        {
            NetworkGameManager.Instance.MessageBoardTMP.gameObject.SetActive(true); 
            NetworkGameManager.Instance.MessageBoardTMP.text = "Waiting for Player 2 to join before starting"; 
            Debug.Log("Waiting for Player 2 to join before starting.");
            return; // Prevents Player 1 from starting actions
        }
        NetworkGameManager.Instance.MessageBoardTMP.gameObject.SetActive(false);

        
        // **NEW: Let all clients request spawn via ServerRpc**
        Vector2Int selectedCell = GridManager.Instance.GetSelectedCell();
        if (selectedCell.x == -1 || selectedCell.y == -1)
        {
            Debug.LogError("No valid cell selected! Click a cell before spawning.");
            return;
        }

        ulong ownerClientId = NetworkManager.Singleton.LocalClientId;
        RequestSpawnUnitServerRpc(type, selectedCell, ownerClientId);
    }

    [ServerRpc]
    private void RequestSpawnUnitServerRpc(UnitType type, Vector2Int cell, ulong clientId)
    {Debug.Log("RequestSpawnUnitServerRpc Selected cell =" +cell + "ClientId="+clientId+" Unit type="+type);
        NetworkGameManager.Instance.SpawnUnitServerRpc(type, cell, clientId);
    }
    public void SetSpawnButtonsInteractable(bool state)
    {
        spawnTankButton.interactable = state;
        spawnJeepButton.interactable = state;
        spawnSoldierButton.interactable = state;
    }
}
