using Unity.Netcode;
using UnityEngine;

public class NetworkUnit : NetworkBehaviour, INetworkSerializable
{
    public UnitType Type { get; private set; }
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int AttackPower { get; private set; }
    public int Cost { get; private set; }
    public Player Owner { get; private set; }

    private NetworkVariable<int> currentHP = new NetworkVariable<int>();
    private NetworkVariable<int> attackPower = new NetworkVariable<int>();
    private NetworkVariable<Vector2Int> gridPosition = new NetworkVariable<Vector2Int>();
    
   
    public int unitID;
    public Vector3 position;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref unitID);
        serializer.SerializeValue(ref position);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void InitializeServerRpc(UnitType type, Vector2Int position, ulong ownerId)
    {
        Type = type;
        Owner = (ownerId == 0) ? Player.Player1 : Player.Player2; // Assign the player

        // Set unit stats
        switch (type)
        {
            case UnitType.Tank:
                MaxHP = 10;
                AttackPower = 5;
                Cost = 4;
                break;
            case UnitType.Jeep:
                MaxHP = 6;
                AttackPower = 3;
                Cost = 2;
                break;
            case UnitType.Soldier:
                MaxHP = 3;
                AttackPower = 1;
                Cost = 1;
                break;
        }
        CurrentHP = MaxHP;

        // Deduct resources when spawning the unit
        ResourceManager.Instance.SpendResources(Owner, Cost);

        // Store grid position and place unit in the world
        gridPosition.Value = position;
        transform.position = GridManager.Instance.GetWorldPosition(position.x, position.y);

        // Mark the cell as occupied
        var cell = GridManager.Instance.GetCell(position.x, position.y);
        if (cell != null)
        {
            cell.SetOccupyingUnit(this);
        }
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
    public void RequestParentClientRpc(string parentObjectName)
    {
        if (!IsServer) return; // **Only the server can reparent**

        Transform newParent = GameObject.Find(parentObjectName)?.transform;
        if (newParent != null)
        {
            transform.SetParent(newParent);
        }
    }

}
