using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SpawnMenuUI : MonoBehaviour
{
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
        if (NetworkManager.Singleton.IsServer)
        {
            // Ensure a valid cell was selected
            Vector2Int selectedCell = GridManager.Instance.GetSelectedCell();
            if (selectedCell.x == -1 || selectedCell.y == -1)
            {
                Debug.LogError("No valid cell selected! Click a cell before spawning.");
                return;
            }

            ulong ownerClientId = NetworkManager.Singleton.LocalClientId;
            NetworkGameManager.Instance.SpawnUnitServerRpc(type, selectedCell, ownerClientId);
        }
    }
}
