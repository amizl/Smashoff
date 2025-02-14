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
    public NetworkVariable<Vector2Int> gridPosition = new NetworkVariable<Vector2Int>();

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
        Owner = (ownerId == 0) ? Player.Player1 : Player.Player2;
        Debug.Log($"[Server] Initializing unit at {position} for {Owner}");

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

        ResourceManager.Instance.SpendResources(Owner, Cost);

        Debug.Log($"[Server] Setting gridPosition.Value = {position}");

        gridPosition.Value = position;
        transform.position = GridManager.Instance.GetWorldPosition(position.x, position.y);

        // Ensure ALL clients update their position
        UpdatePositionClientRpc(gridPosition.Value);

        // Ensure parent assignment
        RequestParentServerRpc("Units");

        var cell = GridManager.Instance.GetCell(position.x, position.y);
        if (cell != null)
        {
            cell.SetOccupyingUnit(this);
        }

        // Sync color and scale across all clients
        UpdateUnitVisualsClientRpc(Owner == Player.Player1 ? Color.yellow : Color.cyan, Owner == Player.Player2);
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

        if (flipSprite)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveForwardServerRpc()
    {
        Debug.Log($"Running MoveForwardServerRpc");
        Debug.Log($"[Server] Unit {unitID} owned by {Owner} moving from {gridPosition.Value}");
        Debug.Log($"[Server] MoveForwardServerRpc called for {Owner} at {gridPosition.Value}");

        Vector2Int newPos = gridPosition.Value;
        newPos.x += Owner == Player.Player1 ? 1 : -1; // Move right/left

        Debug.Log($"[Server] Attempting to move to {newPos}");
        Debug.Log($"[Server] Attempting to move unit from {gridPosition.Value} to {newPos} for {Owner}");
        if (GridManager.Instance.IsValidPosition(newPos.x, newPos.y))
        {
            var targetCell = GridManager.Instance.GetCell(newPos.x, newPos.y);
            if (targetCell != null && !targetCell.IsOccupied())
            {
                var oldCell = GridManager.Instance.GetCell(gridPosition.Value.x, gridPosition.Value.y);
                if (oldCell != null)
                {
                    oldCell.ClearOccupant();
                }

                Debug.Log($"[Server] Moving unit to {newPos}");

                gridPosition.Value = newPos; // Server updates gridPosition
                transform.position = GridManager.Instance.GetWorldPosition(newPos.x, newPos.y);

                Debug.Log($"[Server] Updated gridPosition: {gridPosition.Value}");

                // Sync movement to clients
                UpdatePositionClientRpc(newPos);

                targetCell.SetOccupyingUnit(this);
            }
            else
            {
                Debug.Log("[Server] Target cell is occupied or null.");
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

        // Update the transform position directly (the gridPosition will be synced automatically)
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
            // Subscribe to gridPosition changes so that when the value changes,
            // we update the transform accordingly.
            gridPosition.OnValueChanged += OnGridPositionChanged;

            // Force an initial update in case gridPosition already has a valid value.
            UpdateTransformPosition(gridPosition.Value);
        }
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

            // Ensure all clients receive the updated position
            UpdatePositionClientRpc(newPosition);
        }
        else
        {
            Debug.LogError($"[Server] Invalid move to {newPosition}!");
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestParentServerRpc(string parentObjectName)
    {
        if (!IsServer) return; // Ensure this runs only on the server

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
