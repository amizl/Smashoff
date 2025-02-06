using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Lobbies.Models;
using System;

public class LobbyListItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button joinButton;
    
    public void Initialize(Lobby lobby, Action onJoinClicked)
    {
        lobbyNameText.text = lobby.Name;
        playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        joinButton.onClick.AddListener(() => onJoinClicked?.Invoke());
    }
}
