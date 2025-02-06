using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;

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
    
    private void Start()
    {
        SetupButtonListeners();
        ShowMainMenu();
    }
    
    private void SetupButtonListeners()
    {
        createLobbyButton.onClick.AddListener(() => ShowPanel(createLobbyPanel));
        joinLobbyButton.onClick.AddListener(() => ShowPanel(joinLobbyPanel));
        refreshLobbiesButton.onClick.AddListener(RefreshLobbyList);
        
        confirmCreateButton.onClick.AddListener(async () => {
            var lobby = await MatchmakingManager.Instance.CreateLobby(lobbyNameInput.text);
            if (lobby != null)
            {
                ShowLobbyRoom(lobby);
            }
        });
        
        confirmJoinButton.onClick.AddListener(async () => {
            var lobby = await MatchmakingManager.Instance.JoinLobbyByCode(lobbyCodeInput.text);
            if (lobby != null)
            {
                ShowLobbyRoom(lobby);
            }
        });
        
        backFromCreateButton.onClick.AddListener(() => ShowMainMenu());
        backFromJoinButton.onClick.AddListener(() => ShowMainMenu());
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
            lobbyItemUI.Initialize(lobby, async () => {
                var joinedLobby = await MatchmakingManager.Instance.JoinLobbyByCode(lobby.LobbyCode);
                if (joinedLobby != null)
                {
                    ShowLobbyRoom(joinedLobby);
                }
            });
        }
    }
    
    private void ShowLobbyRoom(Lobby lobby)
    {
        ShowPanel(lobbyRoomPanel);
        lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";
        UpdatePlayerList(lobby);
        
        startGameButton.interactable = lobby.HostId == AuthenticationService.Instance.PlayerId;
    }
    
    private void UpdatePlayerList(Lobby lobby)
    {
        string playerList = "Players:";
        foreach (var player in lobby.Players)
        {
            playerList += $"- {player.Data["PlayerName"].Value}";
        }
        playerListText.text = playerList;
    }
}
