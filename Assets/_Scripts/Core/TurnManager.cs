using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services.Qos.V2.Models;

public class TurnManager : NetworkBehaviour
{
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

        // Sync turn change across all clients
        UpdateTurnClientRpc(CurrentPlayer);

        // Add resources at the start of the turn
        ResourceManager.Instance.AddResources(CurrentPlayer, 2);
    }


    [ClientRpc]
    private void UpdateTurnClientRpc(Player newTurnPlayer)
    {
        CurrentPlayer = newTurnPlayer;
        turnTimer = TurnTimeLimit; // Reset timer for all clients

        if (turnText != null)
            turnText.text = $"Turn: {CurrentPlayer}";

        UpdateEndTurnButton();
    }


    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc()
    {
        EndTurn();
    }


    public void EndGame()
    {
        gameOver = true;
        Debug.Log("Game Over!");
    }
}
