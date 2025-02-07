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
            Vector2Int position = new Vector2Int(4, 7);
            ulong ownerClientId = NetworkManager.Singleton.LocalClientId;
            NetworkGameManager.Instance.SpawnUnitServerRpc(type, position, ownerClientId);
        }
    }
}
