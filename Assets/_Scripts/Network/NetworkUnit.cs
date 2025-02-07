using Unity.Netcode;
using UnityEngine;

public class NetworkUnit : NetworkBehaviour, INetworkSerializable
{
    private NetworkVariable<int> currentHP = new NetworkVariable<int>();
    private NetworkVariable<int> attackPower = new NetworkVariable<int>();
    private NetworkVariable<Vector2Int> gridPosition = new NetworkVariable<Vector2Int>();
    
    public UnitType Type { get; private set; }
    public int unitID;
    public Vector3 position;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref unitID);
        serializer.SerializeValue(ref position);
    }
    [ServerRpc]
    public void InitializeServerRpc(UnitType type, Vector2Int position)
    {
        // 1) Assign the type so GetInitialHP()/GetInitialAttackPower() knows which stats to use
        Type = type;

        // 2) Store the grid position in our NetworkVariable
        gridPosition.Value = position;

        // 3) Position the unit in the world
        transform.position = GridManager.Instance.GetWorldPosition(position.x, position.y);

        // 4) Mark the Cell's occupant so we know something is here
        var cell = GridManager.Instance.GetCell(position.x, position.y);
        if (cell != null)
        {
            cell.OccupyingUnit = this;
        }

        // (Optional) If you want to initialize HP/attack now instead of OnNetworkSpawn, you can do:
        // currentHP.Value = GetInitialHP();
        // attackPower.Value = GetInitialAttackPower();
        // 
        // But typically, it's fine to let OnNetworkSpawn() handle that on the server side.
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHP.Value = GetInitialHP();
            attackPower.Value = GetInitialAttackPower();
        }
    }
    
    [ServerRpc]
    public void MoveUnitServerRpc(Vector2Int newPosition)
    {
        if (IsValidMove(newPosition))
        {
            gridPosition.Value = newPosition;
            transform.position = GridManager.Instance.GetWorldPosition(newPosition.x, newPosition.y);
        }
    }
    
    [ServerRpc]
    public void AttackUnitServerRpc(NetworkUnit target)
    {
        if (IsAdjacentTo(target))
        {
            int damage = CalculateDamage(target);
            target.TakeDamageServerRpc(damage);
        }
    }
    
    [ServerRpc]
    public void TakeDamageServerRpc(int damage)
    {
        currentHP.Value -= damage;
        if (currentHP.Value <= 0)
        {
            NetworkObject.Despawn();
        }
    }
    
    private bool IsValidMove(Vector2Int newPos)
    {
        return GridManager.Instance.IsValidPosition(newPos.x, newPos.y) &&
               Vector2Int.Distance(gridPosition.Value, newPos) == 1;
    }
    
    private bool IsAdjacentTo(NetworkUnit other)
    {
        return Vector2Int.Distance(gridPosition.Value, other.gridPosition.Value) == 1;
    }
    
    private int CalculateDamage(NetworkUnit defender)
    {
        return Mathf.Max(1, attackPower.Value - defender.currentHP.Value);
    }
    
    private int GetInitialHP()
    {
        switch (Type)
        {
            case UnitType.Tank: return 10;
            case UnitType.Jeep: return 6;
            case UnitType.Soldier: return 3;
            default: return 0;
        }
    }
    
    private int GetInitialAttackPower()
    {
        switch (Type)
        {
            case UnitType.Tank: return 5;
            case UnitType.Jeep: return 3;
            case UnitType.Soldier: return 1;
            default: return 0;
        }
    }
    [ClientRpc]
    public void RequestParentClientRpc(string parentName)
    {
        GameObject parentObject = GameObject.Find(parentName);
        if (parentObject != null)
        {
            transform.SetParent(parentObject.transform);
        }
    }
}
