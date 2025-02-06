using UnityEngine;
//local game script (to be merge into NetworkUnit.cs)
public class Unit : MonoBehaviour
{
    public UnitType Type { get; private set; }
    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int AttackPower { get; private set; }
    public int Cost { get; private set; }
    public Player Owner { get; private set; }
    
    private Vector2Int gridPosition;
    
    public void Initialize(UnitType type, Player owner)
    {
        Type = type;
        Owner = owner;
        
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
    }
    
    public void TakeDamage(int damage)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - damage);
        if (CurrentHP <= 0)
            Destroy(gameObject);
    }
    
    public void ApplyTerrainEffects(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.DefenseBonus:
                MaxHP = (int)(MaxHP * 1.2f);
                CurrentHP = MaxHP;
                break;
            case TerrainType.AttackBonus:
                AttackPower = (int)(AttackPower * 1.3f);
                break;
            case TerrainType.Healing:
                CurrentHP = Mathf.Min(MaxHP, CurrentHP + (int)(MaxHP * 0.1f));
                break;
        }
    }
}
