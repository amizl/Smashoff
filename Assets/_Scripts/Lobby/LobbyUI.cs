using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;

public class LobbyUI : MonoBehaviour
{
    //[Header("Players List")]
    //[SerializeField] private Transform playersListContent;     // The ScrollView Content
    //[SerializeField] private GameObject playerNameItemPrefab;  // Prefab with a TextMeshProUGUI

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

    private float pollTimer;
    private const float POLL_INTERVAL = 5f; // Adjust as needed
    private Lobby currentLobby; // Ensure this is updated when you create/join a lobby

    private async void Update()
    {
       
        // Only poll if we have an active lobby (and if this is the host, if needed)
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
                    Debug.Log("is this working?");
                    currentLobby = updatedLobby;
                    UpdatePlayerList(currentLobby);
                }
            }
        }
    }

    private void Start()
    {
        SetupButtonListeners();
        ShowMainMenu();
    }
    //public void AddPlayerName(string playerName)
    //{
    //    // Instantiate a new item under the playersListContent transform
    //    GameObject newPlayerItem = Instantiate(playerNameItemPrefab, playersListContent);

    //    // Grab the TextMeshProUGUI component on the newly spawned item
    //    TextMeshProUGUI textComponent = newPlayerItem.GetComponentInChildren<TextMeshProUGUI>();
    //    if (textComponent != null)
    //    {
    //        textComponent.text = playerName;
    //    }
    //}

    private void SetupButtonListeners()
    {
        createLobbyButton.onClick.AddListener(() => ShowPanel(createLobbyPanel));
        joinLobbyButton.onClick.AddListener(() => ShowPanel(joinLobbyPanel));
        refreshLobbiesButton.onClick.AddListener(RefreshLobbyList);

        confirmCreateButton.onClick.AddListener(async () =>
        {
            var lobby = await MatchmakingManager.Instance.CreateLobby(lobbyNameInput.text);
            if (lobby != null)
            {
                ShowLobbyRoom(lobby);
            }
        });

        confirmJoinButton.onClick.AddListener(async () =>
        {
            var lobby = await MatchmakingManager.Instance.JoinLobbyById(lobbyCodeInput.text);
            if (lobby != null)
            {
                ShowLobbyRoom(lobby);
            }
        });


        backFromCreateButton.onClick.AddListener(() => ShowMainMenu());
        backFromJoinButton.onClick.AddListener(() => ShowMainMenu());
        // **New Listener for Leave Lobby Button:**
        leaveLobbyButton.onClick.AddListener(() => LeaveLobby());
    }
    private void LeaveLobby()
    {
        //Todo Optionally, if you have any cleanup in MatchmakingManager, call it here.
        // For example:
        // MatchmakingManager.Instance.LeaveLobby(); // if you have such a method

        // Then, simply show the main menu
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
        RefreshLobbyList();
    }

    private async void RefreshLobbyList()
    {
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        var lobbies = await MatchmakingManager.Instance.GetLobbiesList();
        foreach (var lobby in lobbies)
        {
            var lobbyItem = Instantiate(lobbyListItemPrefab, lobbyListContent);
            var lobbyItemUI = lobbyItem.GetComponent<LobbyListItem>();
            lobbyItemUI.Initialize(lobby, async () =>
            {
                //var joinedLobby = await MatchmakingManager.Instance.JoinLobbyByCode(lobby.LobbyCode);
                var joinedLobby = await MatchmakingManager.Instance.JoinLobbyById(lobby.Id);

                if (joinedLobby != null)
                {
                    ShowLobbyRoom(joinedLobby);
                }
            });
        }
    }

    private void ShowLobbyRoom(Lobby lobby)
    {
        currentLobby = lobby; // Assign the lobby so you can poll it later
        ShowPanel(lobbyRoomPanel);
        lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";
        UpdatePlayerList(lobby);

        startGameButton.interactable = lobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private void UpdatePlayerList(Lobby lobby)
    {
        // Build a single string with each player's name on a new line
        string playersString = "Players:\n";
        foreach (var player in lobby.Players)
        {
            string playerName = player.Data["PlayerName"].Value;
            playersString += $"- {playerName}\n";
        }

        // Assign it to your TextMeshProUGUI
        playerListText.text = playersString;
    }
}
