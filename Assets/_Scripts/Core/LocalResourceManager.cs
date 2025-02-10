using UnityEngine;

/// <summary>
/// Simple MonoBehaviour that tracks only the local player's resources.
/// No Netcode, no Rpcs, just a static reference for "LocalResourceManager.Instance".
/// </summary>
public class LocalResourceManager : MonoBehaviour
{
    public static LocalResourceManager Instance { get; private set; }

    // Starting resources for your local player
    [SerializeField] private int localResources = 5;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public int GetCurrentResources()
    {
        return localResources;
    }

    public void AddResources(int amount)
    {
        localResources += amount;
    }

    /// <summary>
    /// Returns true if we successfully spend the cost, else false.
    /// </summary>
    public bool SpendResources(int cost)
    {
        if (localResources >= cost)
        {
            localResources -= cost;
            return true;
        }
        return false;
    }
}
