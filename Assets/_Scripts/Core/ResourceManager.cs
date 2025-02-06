using UnityEngine;
using System.Collections.Generic;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }
    
    private Dictionary<Player, int> resources;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        resources = new Dictionary<Player, int>
        {
            { Player.Player1, 5 },
            { Player.Player2, 5 }
        };
    }
    
    public int GetResources(Player player)
    {
        return resources[player];
    }
    
    public void AddResources(Player player, int amount)
    {
        resources[player] += amount;
    }
    
    public bool SpendResources(Player player, int amount)
    {
        if (resources[player] >= amount)
        {
            resources[player] -= amount;
            return true;
        }
        return false;
    }
}
