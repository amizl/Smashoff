using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SpawnMenuUI : MonoBehaviour
{
    [SerializeField] private SpawnMenuUI spawnMenuUI;
    [SerializeField] private Button spawnTankButton;
    [SerializeField] private Button spawnJeepButton;
    [SerializeField] private Button spawnSoldierButton;

    private void Awake()
    {
        spawnTankButton.onClick.AddListener(() => SpawnUnit(UnitType.Tank));
        spawnJeepButton.onClick.AddListener(() => SpawnUnit(UnitType.Jeep));
        spawnSoldierButton.onClick.AddListener(() => SpawnUnit(UnitType.Soldier));
    }

    private void SpawnUnit(UnitType type)
    {
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
    {
        NetworkGameManager.Instance.SpawnUnitServerRpc(type, cell, clientId);
    }
    public void SetSpawnButtonsInteractable(bool state)
    {
        spawnTankButton.interactable = state;
        spawnJeepButton.interactable = state;
        spawnSoldierButton.interactable = state;
    }
}
