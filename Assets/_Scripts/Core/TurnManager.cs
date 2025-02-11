using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services.Qos.V2.Models;
using System.Resources;

public class TurnManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI ResourceText;

    [SerializeField] private TextMeshProUGUI TurnPlayTimer;
    [SerializeField] private SpawnMenuUI spawnMenuUI;
    [SerializeField] private TextMeshProUGUI playerIdentityText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private Button endTurnButton;

    private bool gameStarted = false;
    private bool gameOver = false; // **Fix: Explicitly initialized to false**

    public static TurnManager Instance { get; private set; }
    public Player CurrentPlayer { get; private set; }
    public float TurnTimeLimit = 15f;
    public event Action<Player> OnTurnChanged;

    private float turnTimer;

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

        TurnPlayTimer.text = $"Time Left: {Mathf.CeilToInt(turnTimer)}s";
        turnTimer -= Time.deltaTime;
        // **If the timer reaches zero, force end turn**
        if (turnTimer <= 0)
        {
            if (IsServer)
                EndTurn();
            else
                EndTurnServerRpc(); // Client requests the server to end turn
        }
    }

    public void StartGame()
    {
        gameOver = false;
        gameStarted = true;
        turnTimer = TurnTimeLimit;
        CurrentPlayer = Player.Player1;

        if (turnText != null)
            turnText.text = $"Turn: {CurrentPlayer}";

        // **Directly update UI based on host/client**
        if (playerIdentityText != null)
            playerIdentityText.text = $"You are {(NetworkManager.Singleton.IsHost ? "Player 1" : "Player 2")}";

        //This ensures I always have exactly one listener on your button.
        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(() =>
        {
            if (IsServer)
                EndTurn();
            else
                EndTurnServerRpc();
        });
        UpdateEndTurnButton();
    }

 
    private void UpdateEndTurnButton()
    {
        bool isMyTurn = NetworkManager.Singleton.LocalClientId == (CurrentPlayer == Player.Player1 ? (ulong)0 : (ulong)1);

        if (endTurnButton != null)
            endTurnButton.interactable = isMyTurn;

        if (spawnMenuUI != null)
            spawnMenuUI.SetSpawnButtonsInteractable(isMyTurn);
    }


    public void EndTurn()
    {
        if (!IsServer) return; // Only the server should manage turns

        // Switch to the next player
        CurrentPlayer = (CurrentPlayer == Player.Player1) ? Player.Player2 : Player.Player1;
        turnTimer = TurnTimeLimit; // Reset timer for new turn

        //  Ensure only the server adds resources
        ResourceManager.Instance.AddResourcesServerRpc(CurrentPlayer, 2);

        // Debug log for testing
        Debug.Log($"[TurnManager] Turn switched. Current Player: {CurrentPlayer}");

        // Sync turn change across all clients
        UpdateTurnClientRpc(CurrentPlayer);
    }



    [ClientRpc]
    private void UpdateTurnClientRpc(Player newTurnPlayer)
    {
        CurrentPlayer = newTurnPlayer;
        turnTimer = TurnTimeLimit; // Reset timer for all clients

        if (turnText != null)
            turnText.text = $"Turn: {CurrentPlayer}";

        UpdateEndTurnButton();
        // **NEW: Refresh Resource UI**
       // UpdateResourceUI();
    }
    public void UpdateResourceUI()
    {
        if (ResourceManager.Instance == null || ResourceText == null)
            return;

        // Figure out who I am
        Player localPlayer = GetLocalPlayer();
        Player opponentPlayer = GetOpponentPlayer();

        int myResources = ResourceManager.Instance.GetResources(localPlayer);
        int oppResources = ResourceManager.Instance.GetResources(opponentPlayer);

        // Show both counts in a single TMP text
        ResourceText.text = $"You: {myResources} | Opponent: {oppResources}";
    }


    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc()
    {
        if (IsServer) // Ensures only the server processes turn logic
        {
            EndTurn();
        }
    }

    private Player GetLocalPlayer()
    {
        // Host == Player1, Connected client == Player2
        return NetworkManager.Singleton.IsHost ? Player.Player1 : Player.Player2;
    }

    private Player GetOpponentPlayer()
    {
        // If I'm Player1, opponent is Player2; else vice versa
        return GetLocalPlayer() == Player.Player1 ? Player.Player2 : Player.Player1;
    }

    public void EndGame()
    {
        gameOver = true;
        Debug.Log("Game Over!");
    }
}
