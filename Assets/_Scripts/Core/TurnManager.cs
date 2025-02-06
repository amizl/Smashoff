using UnityEngine;
using System;

public class TurnManager : MonoBehaviour
{
    private bool gameStarted = false;
    public static TurnManager Instance { get; private set; }
    
    public Player CurrentPlayer { get; private set; }
    public float TurnTimeLimit = 15f;
    public event Action<Player> OnTurnChanged;
    
    private float turnTimer;
    private bool gameOver;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        CurrentPlayer = Player.Player1;
        turnTimer = TurnTimeLimit;
    }
    
    private void Update()
    {
        if (gameOver || !gameStarted) return;
        
        turnTimer -= Time.deltaTime;
        if (turnTimer <= 0)
            EndTurn();
    }
    public void StartGame()
    {
        gameStarted = true;
        turnTimer = TurnTimeLimit;
        CurrentPlayer = Player.Player1;
    }
    public void EndTurn()
    {
        CurrentPlayer = (CurrentPlayer == Player.Player1) ? Player.Player2 : Player.Player1;
        turnTimer = TurnTimeLimit;
        OnTurnChanged?.Invoke(CurrentPlayer);
        
        // Add resources at start of turn
        ResourceManager.Instance.AddResources(CurrentPlayer, 2);
    }
}
