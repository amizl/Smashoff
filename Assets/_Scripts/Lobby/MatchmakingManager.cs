using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;

public class MatchmakingManager : MonoBehaviour
{
    public static MatchmakingManager Instance { get; private set; }
    
    private Lobby currentLobby;
    private float heartbeatTimer;
    private readonly float lobbyUpdateTimer;
    private const float LOBBY_HEARTBEAT_INTERVAL = 15f;
    private const float LOBBY_UPDATE_INTERVAL = 1.5f;
    private bool isSigningIn = false; // New!

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            await InitializeUnityServices();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private async Task InitializeUnityServices()
    {
        await UnityServices.InitializeAsync();
        await SignInAnonymously();

        Debug.Log($"Player Id: {AuthenticationService.Instance.PlayerId}");
    }

    private async Task<bool> SignInAnonymously()
    {
        if (isSigningIn)
        {
            Debug.LogWarning("Already signing in!");
            return false; // New!
        }

        isSigningIn = true;

        try
        {
            Debug.Log("Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Player Id: {AuthenticationService.Instance.PlayerId}");
            return true; // Success! New!
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Authentication failed: {e.Message}");
            return false; // Failure! New!
        }
        finally
        {
            isSigningIn = false;
        }
    }



    public async Task<Lobby> CreateLobby(string lobbyName, int maxPlayers = 2)
    {
        try
        {
            CreateLobbyOptions options = new()
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    {"GameMode", new DataObject(DataObject.VisibilityOptions.Public, "Standard")}
                }
            };
            
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            StartLobbyHeartbeat();
            return currentLobby;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Create Lobby failed: {e.Message}");
            return null;
        }
    }
    
    public async Task<Lobby> JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer()
            };
            
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            return currentLobby;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Join Lobby failed: {e.Message}");
            return null;
        }
    }
    public async Task<Lobby> JoinLobbyById(string lobbyId)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };

            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            return currentLobby;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Join Lobby by ID failed: {e.Message}");
            return null;
        }
    }

    public async Task<List<Lobby>> GetLobbiesList()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("Not signed in! Attempting to sign in again...");

            bool signInSuccess = await SignInAnonymously(); // New!

            if (!signInSuccess || !AuthenticationService.Instance.IsSignedIn) // New!
            {
                Debug.LogError("Failed to sign in. Cannot get lobbies."); // New!
                return new List<Lobby>(); // New!
            }
        }

        try
        {
            QueryLobbiesOptions options = new()
            {
                Count = 25,
                Filters = new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "1", QueryFilter.OpOptions.GE)
            }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Get Lobbies failed: {e.Message}");
            return new List<Lobby>();
        }
    }




    private Unity.Services.Lobbies.Models.Player GetPlayer()
    {
        return new Unity.Services.Lobbies.Models.Player
        {
            Data = new Dictionary<string, PlayerDataObject>
        {
            {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, $"Player_{Random.Range(0, 1000)}")}
        }
        };
    }


    private async void StartLobbyHeartbeat()
    {
        while (currentLobby != null)
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= LOBBY_HEARTBEAT_INTERVAL)
            {
                heartbeatTimer = 0;
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
            await Task.Yield();
        }
    }
}
