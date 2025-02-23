using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUnit : NetworkBehaviour, INetworkSerializable
{
    [SerializeField] private Slider hpSlider;               // HP slider in World-Space Canvas
    [SerializeField] private TextMeshProUGUI attackPowerText; // Attack power text in World-Space Canvas
    [SerializeField] private Canvas canvasTransform; // to flip the UI for Player2 
    public UnitType Type { get; private set; }
    private int MaxHP { get; set; }
    public int CurrentHP { get; private set; }
    public int AttackPower { get; private set; }
    public int Cost { get; private set; }
    public Player Owner { get; private set; }

    // NetworkVariables that hold runtime values
    public NetworkVariable<int> currentHP = new NetworkVariable<int>();
    private NetworkVariable<int> attackPower = new NetworkVariable<int>();
    private NetworkVariable<int> maxHP = new NetworkVariable<int>();
    public NetworkVariable<Vector2Int> gridPosition = new NetworkVariable<Vector2Int>();

    // Misc tracking
    public int unitID;
    public Vector3 position;

    //------------------------------------------------------
    //                    INIT & SETUP
    //------------------------------------------------------

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref unitID);
        serializer.SerializeValue(ref position);
    }

    /// <summary>Returns the resource cost of a given UnitType.</summary>
    public static int GetCost(UnitType type)
    {
        switch (type)
        {
            case UnitType.Tank: return 4;
            case UnitType.Jeep: return 2;
            case UnitType.Soldier: return 1;
            default: return 0; // Should never happen
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void InitializeServerRpc(UnitType type, Vector2Int position, ulong ownerId)
    {
        Type = type;
        Owner = (ownerId == 0) ? Player.Player1 : Player.Player2;
        Debug.Log($"[Server] Initializing unit at {position} for {Owner}");

        // Set unit stats based on type
        switch (type)
        {
            case UnitType.Tank:
                MaxHP = 12;
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

        // Spend resources
        ResourceManager.Instance.SpendResources(Owner, Cost);

        // Then do this to keep NetworkVariable sync'd with your real MaxHP:
        maxHP.Value = MaxHP;         // <-- Add this line
        currentHP.Value = GetInitialHP();
        attackPower.Value = GetInitialAttackPower();

        // Position the unit
        Debug.Log($"[Server] Setting gridPosition.Value = {position}");
        gridPosition.Value = position;
        transform.position = GridManager.Instance.GetWorldPosition(position.x, position.y);

        // Sync initial position to clients
        UpdatePositionClientRpc(gridPosition.Value);

        // Attach under "Units" parent
        RequestParentServerRpc("Units");

        // Mark the cell as occupied
        var cell = GridManager.Instance.GetCell(position.x, position.y);
        if (cell != null)
        {
            cell.SetOccupyingUnit(this);
        }

        // Visual color sync
        UpdateUnitVisualsClientRpc(
            Owner == Player.Player1 ? Color.yellow : Color.cyan,
            Owner == Player.Player2
        );

        // Initialize the network variables for HP/Attack
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
        // Flip the sprite if it's Player 2
        transform.localScale = flipSprite ? new Vector3(-1, 1, 1) : Vector3.one;
        canvasTransform.transform.localScale = flipSprite ? new Vector3(-1, 1, 1) : Vector3.one;
    }

    public override void OnNetworkSpawn()
    {
        // On the server, set initial HP & Attack if needed
        if (IsServer)
        {
            currentHP.Value = GetInitialHP();
            attackPower.Value = GetInitialAttackPower();
        }
        if (IsClient)
        {
            // On clients, subscribe to changes
            gridPosition.OnValueChanged += OnGridPositionChanged;
            currentHP.OnValueChanged += OnHPChanged;
            attackPower.OnValueChanged += OnAttackPowerChanged;

            // Immediately update local transform to match server
            UpdateTransformPosition(gridPosition.Value);
        }

        // (Optional) call this once so the UI is correct from the start
        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log($"[Client] Unit {unitID} has been despawned.");
        // Unsubscribe from events if needed
        if (!IsServer)
        {
            gridPosition.OnValueChanged -= OnGridPositionChanged;
            currentHP.OnValueChanged -= OnHPChanged;
            attackPower.OnValueChanged -= OnAttackPowerChanged;
        }
    }

    //------------------------------------------------------
    //                  UI HELPER METHODS
    //------------------------------------------------------

    /// <summary>
    /// Updates the local HP slider and attack power text
    /// based on the current values of currentHP and attackPower.
    /// </summary>
    private void UpdateUI()
    {
        // Safety check if references are assigned
        if (hpSlider != null && maxHP.Value > 0)
        {
            hpSlider.value = (float)currentHP.Value / maxHP.Value;
        }

        if (attackPowerText != null)
        {
            attackPowerText.text = attackPower.Value.ToString();
        }
    }

    private void OnHPChanged(int oldHP, int newHP)
    {
        Debug.Log($"[Client] Unit {unitID} HP changed from {oldHP} to {newHP}");
        UpdateUI();
    }

    private void OnAttackPowerChanged(int oldAttack, int newAttack)
    {
        Debug.Log($"[Client] Unit {unitID} AttackPower changed from {oldAttack} to {newAttack}");
        AttackPower = newAttack; // Keep your local AttackPower in sync
        UpdateUI();
    }

    //------------------------------------------------------
    //               MOVEMENT & POSITION
    //------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void MoveForwardServerRpc()
    {
        Debug.Log($"Running MoveForwardServerRpc");
        Debug.Log($"[Server] Unit {unitID} owned by {Owner} moving from {gridPosition.Value}");

        // Move right for Player1, left for Player2
        Vector2Int newPos = gridPosition.Value;
        newPos.x += (Owner == Player.Player1 ? 1 : -1);

        Debug.Log($"[Server] Attempting to move unit from {gridPosition.Value} to {newPos} for {Owner}");

        if (GridManager.Instance.IsValidPosition(newPos.x, newPos.y))
        {
            var targetCell = GridManager.Instance.GetCell(newPos.x, newPos.y);
            if (targetCell != null)
            {
                if (targetCell.IsOccupied())
                {
                    if (targetCell.OccupyingUnit.Owner != this.Owner)
                    {
                        // Attack enemy
                        Debug.Log($"[Server] Target cell occupied by enemy unit {targetCell.OccupyingUnit.unitID}. Attacking...");
                        AttackUnitServerRpc(targetCell.OccupyingUnit.NetworkObjectId);
                    }
                    else
                    {
                        Debug.Log("[Server] Target cell is occupied by a friendly unit. Movement halted.");
                    }
                    return; // Don’t move into an occupied cell
                }
                else
                {
                    // Clear old occupant
                    var oldCell = GridManager.Instance.GetCell(gridPosition.Value.x, gridPosition.Value.y);
                    oldCell?.ClearOccupant();

                    // Set new occupant
                    gridPosition.Value = newPos;
                    transform.position = GridManager.Instance.GetWorldPosition(newPos.x, newPos.y);
                    targetCell.SetOccupyingUnit(this);

                    Debug.Log($"[Server] Moved unit to {newPos} (Updated gridPosition: {gridPosition.Value})");
                    UpdatePositionClientRpc(newPos);
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
            Debug.Log($"[Client] Converting grid({pos.x}, {pos.y}) => world {worldPos}");
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

    //------------------------------------------------------
    //               COMBAT & DAMAGE
    //------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void AttackUnitServerRpc(ulong targetNetworkObjectId)
    {
        // Look up target by ID
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObj))
        {
            NetworkUnit target = targetObj.GetComponent<NetworkUnit>();
            if (target != null && IsAdjacentTo(target))
            {
                int damage = CalculateDamage(target);
                Debug.Log($"[Server] {Owner} unit {unitID} ({Type}) [AP: {attackPower.Value}, HP: {currentHP.Value}] attacks enemy {target.Owner} unit {target.unitID} ({target.Type}) [HP: {target.currentHP.Value}] for {damage} dmg.");
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
        Debug.Log($"[Server] Unit {unitID} took {damage} damage => {currentHP.Value} HP left.");

        if (currentHP.Value <= 0)
        {
            Debug.Log($"[Server] Unit {unitID} has been destroyed.");
            NetworkObject.Despawn();
        }
    }

    private bool IsAdjacentTo(NetworkUnit other)
    {
        return Vector2Int.Distance(gridPosition.Value, other.gridPosition.Value) == 1;
    }

    private int CalculateDamage(NetworkUnit defender)
    {
        // For now, damage = this unit's AttackPower
        return attackPower.Value;
    }

    //------------------------------------------------------
    //              UTIL & HELPERS
    //------------------------------------------------------

    private bool IsValidMove(Vector2Int newPos)
    {
        return GridManager.Instance.IsValidPosition(newPos.x, newPos.y)
               && Vector2Int.Distance(gridPosition.Value, newPos) == 1;
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

    /// <summary>
    /// Re-parents this unit under a specific GameObject in the scene by name.
    /// </summary>
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
