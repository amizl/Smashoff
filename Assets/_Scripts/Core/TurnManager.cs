using System.Linq;
using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services.Qos.V2.Models;
using System.Resources;
using Unity.Multiplayer.Playmode;
using System.Collections;

public class TurnManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI ResourceText;
    [SerializeField] private TextMeshProUGUI TurnPlayTimer;
    [SerializeField] private SpawnMenuUI spawnMenuUI;
    [SerializeField] private TextMeshProUGUI playerIdentityText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button exitLobbyButton;

    private NetworkVariable<bool> player1Ready = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> player2Ready = new NetworkVariable<bool>(false);
    private bool gameOver = false;
    private bool gameStarted = false;
    private bool countdownStarted = false;
    private Coroutine playAgainCountdown;
    public static TurnManager Instance { get; private set; }
    public Player CurrentPlayer { get; private set; }
    public float TurnTimeLimit = 15f;
    private float turnTimer;
    private const float PlayAgainTimeout = 30f;
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

        if (turnTimer <= 0)
        {
            if (IsServer)
                EndTurn();
            else
                EndTurnServerRpc();
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

        // Initialize resource display
        UpdateResourceUI();


        if (playerIdentityText != null)
            playerIdentityText.text = $"You are {(NetworkManager.Singleton.IsHost ? "Player 1" : "Player 2")}";


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
        if (!IsServer) return;

        // New! Ensure both players joined before allowing actions
        if (NetworkManager.Singleton.ConnectedClients.Count < 2)
        {
            NetworkGameManager.Instance.MessageBoardTMP.gameObject.SetActive(true); // New!
            NetworkGameManager.Instance.MessageBoardTMP.text = "Waiting for Player 2 to join before starting"; // New!
            Debug.Log("Waiting for Player 2 to join before starting."); // New!
            return; // New!
        }
        else
        {
            NetworkGameManager.Instance.MessageBoardTMP.gameObject.SetActive(false); // New!
        }
        // Find all friendly units that belong to the current player.
        var friendlyUnits = FindObjectsByType<NetworkUnit>(FindObjectsSortMode.None)
                                .Where(unit => unit.Owner == CurrentPlayer)
                                .ToList();

        // Sort them based on grid position.
        // For Player1, higher x means more advanced; for Player2, lower x means more advanced.
        if (CurrentPlayer == Player.Player1)
        {
            friendlyUnits = friendlyUnits.OrderByDescending(unit => unit.gridPosition.Value.x)
                                         .ThenBy(unit => unit.gridPosition.Value.y)
                                         .ToList();
        }
        else
        {
            friendlyUnits = friendlyUnits.OrderBy(unit => unit.gridPosition.Value.x)
                                         .ThenBy(unit => unit.gridPosition.Value.y)
                                         .ToList();
        }

        // Move each unit in the sorted order.
        foreach (NetworkUnit unit in friendlyUnits)
        {
            unit.MoveForwardServerRpc();
        }
        // Check for victory condition.
        if (CheckVictoryCondition())
        {
            EndGame(CurrentPlayer);
            return;
        }
        // Switch turn and reset timer/resources.
        CurrentPlayer = (CurrentPlayer == Player.Player1) ? Player.Player2 : Player.Player1;
        turnTimer = TurnTimeLimit;
        ResourceManager.Instance.AddResourcesServerRpc(CurrentPlayer, 2);
        Debug.Log($"[TurnManager] Turn switched. Current Player: {CurrentPlayer}");
        UpdateTurnClientRpc(CurrentPlayer);
    }

[ClientRpc]
    private void UpdateTurnClientRpc(Player newTurnPlayer)
    {
        CurrentPlayer = newTurnPlayer;
        turnTimer = TurnTimeLimit;

        if (turnText != null)
            turnText.text = $"Turn: {CurrentPlayer}";

        UpdateEndTurnButton();
        UpdateResourceUI();
    }
    public void UpdateResourceUI()
    {
        if (ResourceManager.Instance == null || ResourceText == null)
            return;

        Player localPlayer = NetworkManager.Singleton.IsHost ? Player.Player1 : Player.Player2;
        Player opponentPlayer = localPlayer == Player.Player1 ? Player.Player2 : Player.Player1;

        int myResources = ResourceManager.Instance.GetResources(localPlayer);
        int oppResources = ResourceManager.Instance.GetResources(opponentPlayer);


        ResourceText.text = $"You: {myResources} | Opponent: {oppResources}";
    }


    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc()
    {
        if (IsServer)
        {
            EndTurn();
        }
    }

    private Player GetLocalPlayer()
    {

        return NetworkManager.Singleton.IsHost ? Player.Player1 : Player.Player2;
    }

    private Player GetOpponentPlayer()
    {

        return GetLocalPlayer() == Player.Player1 ? Player.Player2 : Player.Player1;
    }
    public bool CheckVictoryCondition()
    {
        int boardWidth = GridManager.Instance.columns;
        int boardHeight = GridManager.Instance.rows;

        // For Player1, a win is when a Player1 unit occupies any cell in the rightmost column.
        if (CurrentPlayer == Player.Player1)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                var cell = GridManager.Instance.GetCell(boardWidth - 1, y);
                if (cell != null && cell.IsOccupied() && cell.OccupyingUnit.Owner == Player.Player1)
                {
                    Debug.Log("[TurnManager] Victory condition met for Player1");
                    return true;
                }
            }
        }
        else // For Player2, check the leftmost column.
        {
            for (int y = 0; y < boardHeight; y++)
            {
                var cell = GridManager.Instance.GetCell(0, y);
                if (cell != null && cell.IsOccupied() && cell.OccupyingUnit.Owner == Player.Player2)
                {
                    Debug.Log("[TurnManager] Victory condition met for Player2");
                    return true;
                }
            }
        }

        return false;
    }

    public void EndGame(Player winner)
    {
        gameOver = true;
        Debug.Log($"Game Over! player {CurrentPlayer} wins");
        DisablePlayerControlsClientRpc();
        ShowGameOverOptionsClientRpc(winner);
    }

    [ClientRpc]
    private void EnablePlayerControlsClientRpc()
    {
        if (spawnMenuUI != null)
            spawnMenuUI.SetSpawnButtonsInteractable(true);

        if (endTurnButton != null)
            endTurnButton.interactable = true;
    }
    [ClientRpc]
    private void DisablePlayerControlsClientRpc()
    {
        if (spawnMenuUI != null)
            spawnMenuUI.SetSpawnButtonsInteractable(false);

        if (endTurnButton != null)
            endTurnButton.interactable = false;
    }
    [ClientRpc]
    private void ShowGameOverOptionsClientRpc(Player winner)
    {
        turnText.text = $"Player {winner} Wins!";
        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.GetComponentInChildren<TextMeshProUGUI>().text = "Play Again";
        endTurnButton.onClick.AddListener(() => RequestPlayAgain());
        exitLobbyButton.onClick.RemoveAllListeners();
        exitLobbyButton.onClick.AddListener(() => CancelPlayAgainAndExit());
    }
    private void RequestPlayAgain()
    {
        if (NetworkManager.Singleton.IsHost)
            player1Ready.Value = !player1Ready.Value; //  Toggle readiness
        else
            TogglePlayerReadyServerRpc();

        if (!countdownStarted)
        {
            countdownStarted = true;
            playAgainCountdown = StartCoroutine(PlayAgainCountdown());
        }
    }
    [ServerRpc(RequireOwnership = false)]
    private void TogglePlayerReadyServerRpc()
    {
        player2Ready.Value = !player2Ready.Value; //  Toggle readiness
        CheckRestartGame();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc()
    {
        player2Ready.Value = true;
        CheckRestartGame();
    }
    [ServerRpc]
    private void ResetGameServerRpc()
    {
        Debug.Log("Resetting Game...");
        gameOver = false;
        countdownStarted = false;
        player1Ready.Value = false;
        player2Ready.Value = false;

        GridManager.Instance.ResetBoard();
        ResourceManager.Instance.ResetResources();
        EnablePlayerControlsClientRpc();

        //  Ensure UI is fully refreshed
        turnText.text = "Turn: Player 1";
        StartGame();
    }
    private IEnumerator PlayAgainCountdown()
    {
        float timer = PlayAgainTimeout;
        while (timer > 0)
        {
            TurnPlayTimer.text = $"Restarting in {Mathf.CeilToInt(timer)}s...";
            yield return new WaitForSeconds(1f);
            timer -= 1f;
        }

        if (!player1Ready.Value || !player2Ready.Value)
        {
            Debug.Log("One or both players did not confirm Play Again. Returning to Lobby.");
            NetworkGameManager.Instance.ExitToLobby();
        }
    }
    private void CancelPlayAgainAndExit()
    {
        if (playAgainCountdown != null)
        {
            StopCoroutine(playAgainCountdown);
            playAgainCountdown = null;
        }

        player1Ready.Value = false;
        player2Ready.Value = false;
        countdownStarted = false;

        NetworkGameManager.Instance.ExitToLobby();
    }


    private void CheckRestartGame()
    {
        Debug.Log("Checking if both players are ready to restart...");
        if (player1Ready.Value && player2Ready.Value)
        {
            Debug.Log("Both players are ready. Restarting game.");
            ResetGameServerRpc();
        }
    }

}