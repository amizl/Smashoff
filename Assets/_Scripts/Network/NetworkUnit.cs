using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUnit : NetworkBehaviour, INetworkSerializable
{
    // terrain Bonus 
    public int attackBonus = 4;
    public int defenseBonus = 3;
    public int resourceBonus = 3;
    public int healingBonus = 3;


    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI attackPowerText;
    [SerializeField] private Canvas canvasTransform;

    public UnitType Type { get; private set; }
    private int MaxHP { get; set; }       // Still your local property
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

    // ---------------------------------------------------------------
    // --- NEW FIELDS for permanent, stackable bonuses
    // ---------------------------------------------------------------
    // We'll record the "base" stats so we can keep applying new bonuses
    private int baseMaxHP;
    private int baseAttackPower;

    // We'll track how many times we've triggered defense or attack tiles
    private NetworkVariable<int> defenseStacks = new NetworkVariable<int>(0);
    private NetworkVariable<int> attackStacks = new NetworkVariable<int>(0);

    // ---------------------------------------------------------------
    //         INIT & SETUP (unchanged except new lines)
    // ---------------------------------------------------------------
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

        // --- NEW: Store the "base" stats for permanent reference
        baseMaxHP = MaxHP;
        baseAttackPower = AttackPower;
        // --------------------------------------------------------

        // Removed: ResourceManager.Instance.SpendResources(Owner, Cost);
        // Resources are now spent only in SpawnUnitServerRpc before this is called

        // Sync NetVars
        // --- CHANGED: Instead of directly using MaxHP as "getInitialHP",
        //              we'll keep your existing GetInitialHP() for continuity 
        maxHP.Value = MaxHP;               // Start with base Max HP
        currentHP.Value = MaxHP;       // e.g. 10 for Tanks, 6 for Jeeps, 3 for Soldiers
        attackPower.Value = GetInitialAttackPower();

        // Occupant references
        gridPosition.Value = pos;
        var cell = GridManager.Instance.GetCell(pos.x, pos.y);
        cell?.SetOccupyingUnit(this);

        // Re-parent under "Units"
        RequestParentServerRpc("Units");

        // Visual color
        UpdateUnitVisualsClientRpc(Owner == Player.Player1 ? Color.yellow : Color.cyan,
                                   Owner == Player.Player2);

        Debug.Log($"[Server] Finalizing unit spawn at {gridPosition.Value}");

        // OPTIONAL: place it visually right away with forced move
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
            currentHP.Value = MaxHP;
            attackPower.Value = GetInitialAttackPower();
            // --- NEW: maxHP is already set; we might force it again if you want:
            // maxHP.Value    = MaxHP; (already set in InitializeServerRpc)
        }

        if (IsClient)
        {
            // Subscribe to changes
            gridPosition.OnValueChanged += OnGridPositionChanged;
            currentHP.OnValueChanged += OnHPChanged;
            attackPower.OnValueChanged += OnAttackPowerChanged;

            // --- NEW: Also track changes to maxHP if you want to reflect it in UI
            maxHP.OnValueChanged += OnMaxHPChanged;
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

            // --- NEW: unhook from maxHP changes
            maxHP.OnValueChanged -= OnMaxHPChanged;
        }
    }

    // ---------------------------------------
    //         UI HELPER METHODS
    // ---------------------------------------
    private void UpdateUI()
    {
        // If you have 0 maxHP in netvar, skip to avoid dividing by zero
        if (hpSlider && maxHP.Value > 0)
        {
            hpSlider.value = (float)currentHP.Value / maxHP.Value;
        }

        if (attackPowerText)
        {
            attackPowerText.text = attackPower.Value.ToString();
        }
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

    // --- NEW: track changes to maxHP (similar to Attack)
    private void OnMaxHPChanged(int oldVal, int newVal)
    {
        Debug.Log($"[Client] Unit {unitID} maxHP changed {oldVal} -> {newVal}");
        MaxHP = newVal;    // store the new max in your local property
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

        // --- NEW: After occupying the new cell, apply permanent bonus if any
        ApplyTerrainBonusServerRpc();
        // --------------------------------

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
        // Calculate defense value: each defense stack reduces damage by defenseBonus.
        int defenseValue = defenseStacks.Value * defenseBonus;
        // Effective damage is reduced by defenseValue, but ensure at least 1 damage is done.
        int mitigatedDamage = Mathf.Max(dmg - defenseValue, 1);

        // Apply only the mitigated damage.
        currentHP.Value -= mitigatedDamage;

        Debug.Log($"[Server] Unit {unitID} took {mitigatedDamage} damage after mitigation (raw damage: {dmg}, defenseValue: {defenseValue}).");

        if (currentHP.Value > 0) return;

        Debug.Log($"[Server] Unit {unitID} died. Let attacker take my spot...");

        // 1) Despawn this unit.
        NetworkObject.Despawn();

        // 2) Move the attacker onto my cell.
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
        // We use the netvar "attackPower.Value" as the damage
        return attackPower.Value;
    }

    private bool IsValidMove(Vector2Int newPos)
    {
        return GridManager.Instance.IsValidPosition(newPos.x, newPos.y)
            && Vector2Int.Distance(gridPosition.Value, newPos) == 1;
    }

    //private int GetInitialHP()
    //{
    //    switch (Type)
    //    {
    //        case UnitType.Tank: return 12;
    //        case UnitType.Jeep: return 6;
    //        case UnitType.Soldier: return 3;
    //        default: return 0;
    //    }
    //}

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

    // --------------------------------------------------------------------
    // --- NEW SECTION: PERMANENT, STACKABLE TERRAIN BONUSES
    // --------------------------------------------------------------------
    /// <summary>
    ///  Called each time we land on a new cell. If that cell has 
    ///  AttackBonus, DefenseBonus, Healing, or ResourceGen, 
    ///  we apply it permanently (or one-time if healing).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ApplyTerrainBonusServerRpc()
    {
        var cell = GridManager.Instance.GetCell(gridPosition.Value.x, gridPosition.Value.y);
        if (cell == null) return;

        switch (cell.Terrain)
        {
            case TerrainType.DefenseBonus:
                // +1 defense stack => we’ll handle the logic in RecalcStats
                defenseStacks.Value++;
                RecalculateStats();
                TriggerBonusVFXClientRpc(TerrainType.DefenseBonus);
                break;

            case TerrainType.AttackBonus:
                attackStacks.Value++;
                RecalculateStats();
                TriggerBonusVFXClientRpc(TerrainType.AttackBonus);
                break;

            case TerrainType.Healing:
                // e.g. heal +20 each time
                int healing = 20;
                int newHP = currentHP.Value + healing;
                if (newHP > maxHP.Value) newHP = maxHP.Value;
                currentHP.Value = newHP;
                TriggerBonusVFXClientRpc(TerrainType.Healing);
                break;

            case TerrainType.ResourceGen:
                // e.g. +2 resources to the unit’s owner
                ResourceManager.Instance.AddResourcesServerRpc(Owner, 2);
                TriggerBonusVFXClientRpc(TerrainType.ResourceGen);
                break;

            default:
                // Normal => no effect
                break;
        }
    }

    /// <summary>
    ///  Recompute final MaxHP & AttackPower based on how many 
    ///  defenseStacks / attackStacks we’ve accumulated.
    ///  Then clamp currentHP if needed.
    /// </summary>
    private void RecalculateStats()
    {
        // Recalculate attack power based on attack stacks.
        int newAttack = baseAttackPower + (attackStacks.Value * attackBonus);

        // Set max HP to the constant MaxHP (which was set during initialization)
        maxHP.Value = MaxHP;
        attackPower.Value = newAttack;

        // Clamp current HP to not exceed the new MaxHP.
        if (currentHP.Value > MaxHP)
        {
            currentHP.Value = MaxHP;
        }
    }


    // --------------------------------------------------------------------
    // --- NEW: Just for visual feedback when you trigger a tile bonus
    // --------------------------------------------------------------------
    [ClientRpc]
    private void TriggerBonusVFXClientRpc(TerrainType terrain)
    {
        Debug.Log($"[Client] Bonus triggered: {terrain} on unit {unitID}");
        // Optionally, spawn a particle effect or do animation
    }
}
