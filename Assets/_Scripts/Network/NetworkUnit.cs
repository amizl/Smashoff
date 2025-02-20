using Unity.Netcode;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class NetworkUnit : NetworkBehaviour, INetworkSerializable
{
    public UnitType Type { get; private set; }
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int AttackPower { get; private set; }
    public int Cost { get; private set; }
    public Player Owner { get; private set; }

    public NetworkVariable<int> currentHP = new NetworkVariable<int>();
    private NetworkVariable<int> attackPower = new NetworkVariable<int>();
    public NetworkVariable<Vector2Int> gridPosition = new NetworkVariable<Vector2Int>();

    public int unitID;
    public Vector3 position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref unitID);
        serializer.SerializeValue(ref position);
    }
    // Static method to get unit cost by type (single source of truth)
    public static int GetCost(UnitType type)
    {
        switch (type)
        {
            case UnitType.Tank: return 4;
            case UnitType.Jeep: return 2;
            case UnitType.Soldier: return 1;
            default: return 0; // Shouldn’t happen due to earlier validation
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void InitializeServerRpc(UnitType type, Vector2Int position, ulong ownerId)
    {
        Type = type;
        Owner = (ownerId == 0) ? Player.Player1 : Player.Player2;
        Debug.Log($"[Server] Initializing unit at {position} for {Owner}");

        // Set unit stats based on type.
        switch (type)
        {
            case UnitType.Tank:
                MaxHP = 10;
                AttackPower = 5;
                Cost = GetCost(UnitType.Tank);
                break;
            case UnitType.Jeep:
                MaxHP = 6;
                AttackPower = 3;
                Cost = GetCost(UnitType.Jeep); 
                break;
            case UnitType.Soldier:
                MaxHP = 3;
                AttackPower = 1;
                Cost = GetCost(UnitType.Soldier); 
                break;
        }
        CurrentHP = MaxHP;

        ResourceManager.Instance.SpendResources(Owner, Cost);

        Debug.Log($"[Server] Setting gridPosition.Value = {position}");
        gridPosition.Value = position;
        transform.position = GridManager.Instance.GetWorldPosition(position.x, position.y);

        // Sync initial position to clients.
        UpdatePositionClientRpc(gridPosition.Value);

        // Set parent for organization.
        RequestParentServerRpc("Units");

        var cell = GridManager.Instance.GetCell(position.x, position.y);
        if (cell != null)
        {
            cell.SetOccupyingUnit(this);
        }

        // Sync unit visuals to clients.
        UpdateUnitVisualsClientRpc(Owner == Player.Player1 ? Color.yellow : Color.cyan, Owner == Player.Player2);

        // *** NEW: Set network variables AFTER type and stats are known ***
        currentHP.Value = GetInitialHP();
        attackPower.Value = GetInitialAttackPower();
        Debug.Log($"[Server] Finalized unit spawn at {gridPosition.Value}");
    }

    [ClientRpc]
    private void UpdateUnitVisualsClientRpc(Color unitColor, bool flipSprite)
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = unitColor;
        }
        transform.localScale = flipSprite ? new Vector3(-1, 1, 1) : Vector3.one;
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveForwardServerRpc()
    {
        Debug.Log($"Running MoveForwardServerRpc");
        Debug.Log($"[Server] Unit {unitID} owned by {Owner} moving from {gridPosition.Value}");

        // Calculate the new grid position based on the unit's owner.
        Vector2Int newPos = gridPosition.Value;
        newPos.x += Owner == Player.Player1 ? 1 : -1; // Move right for Player1, left for Player2

        Debug.Log($"[Server] Attempting to move unit from {gridPosition.Value} to {newPos} for {Owner}");

        if (GridManager.Instance.IsValidPosition(newPos.x, newPos.y))
        {
            var targetCell = GridManager.Instance.GetCell(newPos.x, newPos.y);
            if (targetCell != null)
            {
                if (targetCell.IsOccupied())
                {
                    // If occupied, check if the occupying unit is an enemy.
                    if (targetCell.OccupyingUnit.Owner != this.Owner)
                    {
                        Debug.Log($"[Server] Target cell occupied by enemy unit {targetCell.OccupyingUnit.unitID}. Attacking...");
                        // Call attack RPC, passing the enemy's NetworkObjectId.
                        AttackUnitServerRpc(targetCell.OccupyingUnit.NetworkObjectId);
                    }
                    else
                    {
                        Debug.Log("[Server] Target cell is occupied by a friendly unit.");
                    }
                    // Do not move if the cell is occupied.
                    return;
                }
                else
                {
                    // If cell is free, perform the movement.
                    var oldCell = GridManager.Instance.GetCell(gridPosition.Value.x, gridPosition.Value.y);
                    oldCell?.ClearOccupant();

                    Debug.Log($"[Server] Moving unit to {newPos}");
                    gridPosition.Value = newPos;
                    transform.position = GridManager.Instance.GetWorldPosition(newPos.x, newPos.y);
                    Debug.Log($"[Server] Updated gridPosition: {gridPosition.Value}");

                    // Sync movement to clients.
                    UpdatePositionClientRpc(newPos);
                    targetCell.SetOccupyingUnit(this);
                }
            }
            else
            {
                Debug.Log("[Server] Target cell is null.");
            }
        }
        else
        {
            Debug.Log("[Server] Invalid move position.");
        }
    }

    [ClientRpc]
    private void UpdatePositionClientRpc(Vector2Int newPos)
    {
        if (IsServer) return;
        Debug.Log($"[Client] Received position update: {newPos}");
        UpdateTransformPosition(newPos);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHP.Value = GetInitialHP();
            attackPower.Value = GetInitialAttackPower();
        }
        else
        {
            gridPosition.OnValueChanged += OnGridPositionChanged;
            currentHP.OnValueChanged += OnHPChanged;
            attackPower.OnValueChanged += OnAttackPowerChanged;
            UpdateTransformPosition(gridPosition.Value);
        }
    }

    private void OnHPChanged(int oldHP, int newHP)
    {
        Debug.Log($"[Client] Unit {unitID} HP changed from {oldHP} to {newHP}");
        // (Optional) Update your UI manager here.
    }

    private void OnAttackPowerChanged(int oldAttack, int newAttack)
    {
        AttackPower = newAttack;
        Debug.Log($"[Client] Unit {unitID} AttackPower changed from {oldAttack} to {newAttack}");
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log($"[Client] Unit {unitID} has been despawned.");
        // (Optional) Remove unit from UI.
    }

    private void OnGridPositionChanged(Vector2Int oldPos, Vector2Int newPos)
    {
        Debug.Log($"[Client] gridPosition changed from {oldPos} to {newPos}");
        UpdateTransformPosition(newPos);
    }

    private void UpdateTransformPosition(Vector2Int pos)
    {
        if (GridManager.Instance != null)
        {
            Vector3 worldPos = GridManager.Instance.GetWorldPosition(pos.x, pos.y);
            Debug.Log($"[Client] Converting grid ({pos.x}, {pos.y}) to world position {worldPos}");
            transform.position = worldPos;
        }
        else
        {
            Debug.LogWarning("[Client] GridManager instance is null!");
        }
    }

    [ServerRpc]
    public void MoveUnitServerRpc(Vector2Int newPosition)
    {
        Debug.Log($"[Server] Moving unit to {newPosition}");
        if (IsValidMove(newPosition))
        {
            gridPosition.Value = newPosition;
            transform.position = GridManager.Instance.GetWorldPosition(newPosition.x, newPosition.y);
            UpdatePositionClientRpc(newPosition);
        }
        else
        {
            Debug.LogError($"[Server] Invalid move to {newPosition}!");
        }
    }

    // Attack Unit: now logs detailed information.
    [ServerRpc(RequireOwnership = false)]
    public void AttackUnitServerRpc(ulong targetNetworkObjectId)
    {
        // Look up target using its NetworkObjectId.
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObj))
        {
            NetworkUnit target = targetObj.GetComponent<NetworkUnit>();
            if (target != null && IsAdjacentTo(target))
            {
                int damage = CalculateDamage(target);
                Debug.Log($"[Server] {Owner} unit {unitID} ({Type}) [AttackPower: {attackPower.Value}, HP: {currentHP.Value}] attacks enemy {target.Owner} unit {target.unitID} ({target.Type}) [HP: {target.currentHP.Value}] for {damage} damage.");
                target.TakeDamageServerRpc(damage);
            }
            else
            {
                Debug.LogWarning("[Server] Target unit is either null or not adjacent.");
            }
        }
        else
        {
            Debug.LogError("[Server] Target unit not found!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        currentHP.Value -= damage;
        Debug.Log($"[Server] Unit {unitID} took {damage} damage and now has {currentHP.Value} HP.");
        if (currentHP.Value <= 0)
        {
            Debug.Log($"[Server] Unit {unitID} has been destroyed.");
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
        // In this version, damage equals the unit's attack power.
        return attackPower.Value;
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestParentServerRpc(string parentObjectName)
    {
        if (!IsServer) return;
        Transform newParent = GameObject.Find(parentObjectName)?.transform;
        if (newParent != null)
        {
            transform.SetParent(newParent);
        }
        else
        {
            Debug.LogWarning($"[Server] Could not find parent object: {parentObjectName}");
        }
    }
}
