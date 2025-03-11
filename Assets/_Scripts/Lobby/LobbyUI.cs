using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;
using Unity.Netcode;

public class LobbyUI : MonoBehaviour
{
    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private Button refreshLobbiesButton;
    [SerializeField] private Transform lobbyListContent;
    [SerializeField] private GameObject lobbyListItemPrefab;

    [Header("Create Lobby")]
    [SerializeField] private GameObject createLobbyPanel;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private Button confirmCreateButton;
    [SerializeField] private Button backFromCreateButton;

    [Header("Join Lobby")]
    [SerializeField] private GameObject joinLobbyPanel;
    [SerializeField] private TMP_InputField lobbyCodeInput;
    [SerializeField] private Button confirmJoinButton;
    [SerializeField] private Button backFromJoinButton;

    [Header("Lobby Room")]
    [SerializeField] private GameObject lobbyRoomPanel;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private TextMeshProUGUI playerListText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;

    // Polling fields for refreshing the current Lobby state
    private float pollTimer;
    private const float POLL_INTERVAL = 5f;
    private Lobby currentLobby;

    private void Start()
    {
        // 1) Set up UI button listeners
        SetupButtonListeners();

        // 2) Subscribe to sign-in events from MatchmakingManager (event-based sign-in approach)
        MatchmakingManager.Instance.OnSignInComplete += HandleSignInComplete;
        MatchmakingManager.Instance.OnSignInFailed += HandleSignInFailed;

        // IMPORTANT: We do NOT call ShowMainMenu() or RefreshLobbyList() yet.
        // We'll wait for OnSignInComplete to fire after the user is really signed in.
        startGameButton.onClick.AddListener(OnClickStartGame); // <--- New
    }
    private void OnClickStartGame()
    {
        // 1) Confirm I'm the Netcode Host
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("Only the host can start the game!");
            return;
        }

        // 2) Load the "SmashOff" scene for ALL connected players
        Debug.Log("Loading SmashOff scene via Netcode for all players...");
        NetworkManager.Singleton.SceneManager.LoadScene("SmashOff", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
    private void OnDestroy()
    {
        // Always unsubscribe from events to avoid memory leaks / null refs
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.OnSignInComplete -= HandleSignInComplete;
            MatchmakingManager.Instance.OnSignInFailed -= HandleSignInFailed;
        }
    }

    // ---------------------------------------------------------------------------
    // EVENT CALLBACKS FOR SIGN-IN
    // ---------------------------------------------------------------------------
    private void HandleSignInComplete()
    {
        // We are now definitely signed in => safe to show main menu and query lobbies
        ShowMainMenu();
        RefreshLobbyList();
    }

    private void HandleSignInFailed()
    {
        Debug.LogError("Sign in failed! Show an error UI or let the user retry.");
        // You could display a message or show a 'Retry' button.
    }

    // ---------------------------------------------------------------------------
    // UPDATE: Poll the current lobby for changes (optional)
    // ---------------------------------------------------------------------------
    private async void Update()
    {
        // Only poll if we have an active lobby
        if (currentLobby != null)
        {
            pollTimer += Time.deltaTime;
            if (pollTimer >= POLL_INTERVAL)
            {
                pollTimer = 0;
                // Refresh the current lobby from the Lobby Service
                var updatedLobby = await Unity.Services.Lobbies.LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                if (updatedLobby != null)
                {
                    Debug.Log("Lobby poll succeeded, updating player list...");
                    currentLobby = updatedLobby;
                    UpdatePlayerList(currentLobby);
                }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // UI BUTTON SETUP
    // ---------------------------------------------------------------------------
    private void SetupButtonListeners()
    {
        startGameButton.onClick.AddListener(OnClickStartGame);
        createLobbyButton.onClick.AddListener(() => ShowPanel(createLobbyPanel));
        joinLobbyButton.onClick.AddListener(() => ShowPanel(joinLobbyPanel));
        refreshLobbiesButton.onClick.AddListener(RefreshLobbyList);

        confirmCreateButton.onClick.AddListener(async () =>
        {
            // Create a lobby
            var lobby = await MatchmakingManager.Instance.CreateLobby(lobbyNameInput.text);
            if (lobby != null)
            {
                ShowLobbyRoom(lobby);
            }
        });

        confirmJoinButton.onClick.AddListener(async () =>
        {
            // Join a lobby by ID (or code)
            var lobby = await MatchmakingManager.Instance.JoinLobbyById(lobbyCodeInput.text);
            if (lobby != null)
            {
                ShowLobbyRoom(lobby);
            }
        });

        backFromCreateButton.onClick.AddListener(() => ShowMainMenu());
        backFromJoinButton.onClick.AddListener(() => ShowMainMenu());

        // Hook up the "Leave Lobby" button
        leaveLobbyButton.onClick.AddListener(() => LeaveLobby());
    }

    // ---------------------------------------------------------------------------
    // LOBBY NAVIGATION
    // ---------------------------------------------------------------------------
    private void LeaveLobby()
    {
        // If you have a specific "LeaveLobby()" method in MatchmakingManager, call it here.
        // e.g. MatchmakingManager.Instance.LeaveLobby();

        // Return to main menu
        ShowMainMenu();
    }

    private void ShowPanel(GameObject panel)
    {
        mainMenuPanel.SetActive(false);
        createLobbyPanel.SetActive(false);
        joinLobbyPanel.SetActive(false);
        lobbyRoomPanel.SetActive(false);

        panel.SetActive(true);
    }

    private void ShowMainMenu()
    {
        ShowPanel(mainMenuPanel);
        // We *can* call RefreshLobbyList here if you like,
        // but we also do it in HandleSignInComplete() so you don't need it twice.
    }

    // ---------------------------------------------------------------------------
    // LOBBY REFRESH
    // ---------------------------------------------------------------------------
    private async void RefreshLobbyList()
    {
        // Clear out previous items
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        // Grab the list of lobbies from MatchmakingManager
        var lobbies = await MatchmakingManager.Instance.GetLobbiesList();

        // Instantiate UI items for each lobby
        foreach (var lobby in lobbies)
        {
            var lobbyItem = Instantiate(lobbyListItemPrefab, lobbyListContent);
            var lobbyItemUI = lobbyItem.GetComponent<LobbyListItem>();

            // Clicking Join => join that lobby
            lobbyItemUI.Initialize(lobby, async () =>
            {
                var joinedLobby = await MatchmakingManager.Instance.JoinLobbyById(lobby.Id);
                if (joinedLobby != null)
                {
                    ShowLobbyRoom(joinedLobby);
                }
            });
        }
    }

    // ---------------------------------------------------------------------------
    // SHOW LOBBY ROOM UI
    // ---------------------------------------------------------------------------
    private void ShowLobbyRoom(Lobby lobby)
    {
        currentLobby = lobby; // Keep track so we can poll for changes
        ShowPanel(lobbyRoomPanel);

        lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";
        UpdatePlayerList(lobby);

        // Enable "Start Game" button only if I'm the host
        startGameButton.interactable = (lobby.HostId == AuthenticationService.Instance.PlayerId);
    }

    private void UpdatePlayerList(Lobby lobby)
    {
        // Build a single string
        string playersString = "Players:\n";
        foreach (var player in lobby.Players)
        {
            string playerName = player.Data["PlayerName"].Value;
            playersString += $"- {playerName}\n";
        }
        playerListText.text = playersString;
    }
    public void ExitGame()
    {
        Application.Quit();
    }
}
