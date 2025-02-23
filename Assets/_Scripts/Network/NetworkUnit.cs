using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUnit : NetworkBehaviour, INetworkSerializable
{
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI attackPowerText;
    [SerializeField] private Canvas canvasTransform;

    public UnitType Type { get; private set; }
    private int MaxHP { get; set; }
    public int CurrentHP { get; private set; }
    public int AttackPower { get; private set; }
    public int Cost { get; private set; }
    public Player Owner { get; private set; }

    // NetworkVariables for runtime values
    public NetworkVariable<int> currentHP = new NetworkVariable<int>();
    private NetworkVariable<int> attackPower = new NetworkVariable<int>();
    private NetworkVariable<int> maxHP = new NetworkVariable<int>();
    public NetworkVariable<Vector2Int> gridPosition = new NetworkVariable<Vector2Int>();

    public int unitID;
    public Vector3 position;

    // ---------------------------------------
    //         INIT & SETUP
    // ---------------------------------------
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref unitID);
        serializer.SerializeValue(ref position);
    }

    public static int GetCost(UnitType type)
    {
        switch (type)
        {
            case UnitType.Tank: return 4;
            case UnitType.Jeep: return 2;
            case UnitType.Soldier: return 1;
            default: return 0;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void InitializeServerRpc(UnitType type, Vector2Int pos, ulong ownerId)
    {
        Type = type;
        Owner = (ownerId == 0) ? Player.Player1 : Player.Player2;
        Debug.Log($"[Server] Initializing unit at {pos} for {Owner}");

        // Set unit stats
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

        // Removed: ResourceManager.Instance.SpendResources(Owner, Cost);
        // Resources are now spent only in SpawnUnitServerRpc before this is called

        // Sync NetVars
        maxHP.Value = MaxHP;
        currentHP.Value = GetInitialHP();
        attackPower.Value = GetInitialAttackPower();

        // Occupant references
        gridPosition.Value = pos;
        var cell = GridManager.Instance.GetCell(pos.x, pos.y);
        cell?.SetOccupyingUnit(this);

        // Re-parent under "Units"
        RequestParentServerRpc("Units");

        // **Don't** set transform.position on the server => let the client side do that via MoveUnitServerRpc / MoveUnitClientRpc

        // Visual color
        UpdateUnitVisualsClientRpc(Owner == Player.Player1 ? Color.yellow : Color.cyan,
                                   Owner == Player.Player2);

        Debug.Log($"[Server] Finalizing unit spawn at {gridPosition.Value}");

        // OPTIONAL: If you want to place it visually right away for everyone, call MoveUnitServerRpc with force.
        // This is just to drop it at the correct spot initially:
        MoveUnitServerRpc(pos, true);
    }

    [ClientRpc]
    private void UpdateUnitVisualsClientRpc(Color unitColor, bool flipSprite)
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = unitColor;

        // Flip the sprite if Player 2
        transform.localScale = flipSprite ? new Vector3(-1, 1, 1) : Vector3.one;
        canvasTransform.transform.localScale = flipSprite ? new Vector3(-1, 1, 1) : Vector3.one;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHP.Value = GetInitialHP();
            attackPower.Value = GetInitialAttackPower();
        }
        if (IsClient)
        {
            // Subscribe to changes
            gridPosition.OnValueChanged += OnGridPositionChanged;
            currentHP.OnValueChanged += OnHPChanged;
            attackPower.OnValueChanged += OnAttackPowerChanged;
        }

        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log($"[Client] Unit {unitID} despawned.");

        if (!IsServer)
        {
            gridPosition.OnValueChanged -= OnGridPositionChanged;
            currentHP.OnValueChanged -= OnHPChanged;
            attackPower.OnValueChanged -= OnAttackPowerChanged;
        }
    }

    // ---------------------------------------
    //         UI HELPER METHODS
    // ---------------------------------------
    private void UpdateUI()
    {
        if (hpSlider && maxHP.Value > 0)
            hpSlider.value = (float)currentHP.Value / maxHP.Value;

        if (attackPowerText)
            attackPowerText.text = attackPower.Value.ToString();
    }
    private void OnHPChanged(int oldHP, int newHP)
    {
        Debug.Log($"[Client] Unit {unitID} HP changed {oldHP} -> {newHP}");
        UpdateUI();
    }
    private void OnAttackPowerChanged(int oldAP, int newAP)
    {
        Debug.Log($"[Client] Unit {unitID} Attack changed {oldAP} -> {newAP}");
        AttackPower = newAP;
        UpdateUI();
    }

    // ---------------------------------------
    //          MOVEMENT & POSITION
    // ---------------------------------------
    [ServerRpc(RequireOwnership = false)]
    public void MoveForwardServerRpc()
    {
        Debug.Log($"[Server] MoveForward => {unitID} owned by {Owner}");
        // Move right for P1, left for P2
        Vector2Int newPos = gridPosition.Value;
        newPos.x += (Owner == Player.Player1 ? 1 : -1);

        if (GridManager.Instance.IsValidPosition(newPos.x, newPos.y))
        {
            var targetCell = GridManager.Instance.GetCell(newPos.x, newPos.y);
            if (targetCell != null)
            {
                if (targetCell.IsOccupied())
                {
                    // Attack if enemy
                    if (targetCell.OccupyingUnit.Owner != this.Owner)
                    {
                        Debug.Log($"[Server] Attacking occupant {targetCell.OccupyingUnit.unitID}");
                        AttackUnitServerRpc(targetCell.OccupyingUnit.NetworkObjectId);
                    }
                    else
                    {
                        Debug.Log($"[Server] Occupied by friendly; no movement.");
                    }
                }
                else
                {
                    // Free => Move
                    MoveUnitServerRpc(newPos, false);
                }
            }
        }
    }

    /// <summary>
    /// The single function that actually updates occupant references
    /// and triggers the smooth client-side movement.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void MoveUnitServerRpc(Vector2Int newPosition, bool forceMove = false)
    {
        Debug.Log($"[Server] MoveUnitServerRpc => {unitID}, newPos={newPosition}, force={forceMove}");

        // If not forced, do adjacency checks
        if (!forceMove && !IsValidMove(newPosition))
        {
            Debug.LogError($"[Server] Invalid move to {newPosition}!");
            return;
        }

        var targetCell = GridManager.Instance.GetCell(newPosition.x, newPosition.y);
        if (!forceMove && (targetCell == null || targetCell.IsOccupied()))
        {
            Debug.LogError($"[Server] Move failed => cell is occupied or invalid.");
            return;
        }

        // Clear old occupant
        var oldCell = GridManager.Instance.GetCell(gridPosition.Value.x, gridPosition.Value.y);
        oldCell?.ClearOccupant();

        // Occupy new cell
        gridPosition.Value = newPosition;
        targetCell?.SetOccupyingUnit(this);

        // Animate on clients (including Host)
        var finalPos = GridManager.Instance.GetWorldPosition(newPosition.x, newPosition.y);
        MoveUnitClientRpc(finalPos);

        Debug.Log($"[Server] Unit {unitID} -> {newPosition} OK (force={forceMove})");
    }

    [ClientRpc]
    private void MoveUnitClientRpc(Vector3 targetPos)
    {
        Debug.Log($"[Client] MoveUnitClientRpc => Lerp to {targetPos}");
        StartCoroutine(SmoothMoveRoutine(targetPos, 0.5f));
    }

    private IEnumerator SmoothMoveRoutine(Vector3 targetPos, float duration)
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        transform.position = targetPos;
    }

    // ---------------------------------------
    //        COMBAT & DAMAGE
    // ---------------------------------------
    [ServerRpc(RequireOwnership = false)]
    public void AttackUnitServerRpc(ulong targetNetworkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject obj))
        {
            NetworkUnit target = obj.GetComponent<NetworkUnit>();
            if (target != null && IsAdjacentTo(target))
            {
                int dmg = CalculateDamage(target);
                target.TakeDamageServerRpc(dmg, gridPosition.Value);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int dmg, Vector2Int attackerPos)
    {
        currentHP.Value -= dmg;
        if (currentHP.Value > 0) return;

        Debug.Log($"[Server] Unit {unitID} died. Let attacker take my spot...");

        // 1) Despawn this unit
        NetworkObject.Despawn();

        // 2) Move the attacker onto my cell
        var attackerCell = GridManager.Instance.GetCell(attackerPos.x, attackerPos.y);
        if (attackerCell != null && attackerCell.IsOccupied())
        {
            var attacker = attackerCell.OccupyingUnit;
            attacker.MoveUnitServerRpc(gridPosition.Value, true);
        }
    }

    // ---------------------------------------
    //        UTILS & HELPERS
    // ---------------------------------------
    private bool IsAdjacentTo(NetworkUnit other)
    {
        return Vector2Int.Distance(gridPosition.Value, other.gridPosition.Value) == 1;
    }

    private int CalculateDamage(NetworkUnit defender)
    {
        return attackPower.Value;
    }

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

    [ServerRpc(RequireOwnership = false)]
    public void RequestParentServerRpc(string parentObjectName)
    {
        if (!IsServer) return;
        var newParent = GameObject.Find(parentObjectName)?.transform;
        if (newParent != null) transform.SetParent(newParent);
    }

    private void OnGridPositionChanged(Vector2Int oldPos, Vector2Int newPos)
    {
        Debug.Log($"[Client] gridPosition changed from {oldPos} to {newPos}");
        // We do NOT forcibly set transform here, because we let MoveUnitClientRpc handle the movement.
    }
}
