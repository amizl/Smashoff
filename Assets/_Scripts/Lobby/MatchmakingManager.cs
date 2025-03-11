using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

// --------------------------------------------
// MATCHMAKINGMANAGER (OPTION 2, EVENT-BASED)
// --------------------------------------------
public class MatchmakingManager : MonoBehaviour
{
    public static MatchmakingManager Instance { get; private set; }

    // New! Events you can subscribe to in LobbyUI:
    public event Action OnSignInComplete; // Fired on successful sign-in
    public event Action OnSignInFailed;   // Fired if sign-in fails

    private Lobby currentLobby;
    private float heartbeatTimer;
    private const float LOBBY_HEARTBEAT_INTERVAL = 15f;
    private bool isSigningIn = false; // Guard to avoid double sign-in

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize Unity Services and attempt sign-in once
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
        // Sign in once at startup; success/fail triggers events
        await SignInAnonymously();
    }

    // -------------------------------------------------------------------
    // SIGN IN ANONYMOUSLY - Fires OnSignInComplete or OnSignInFailed
    // -------------------------------------------------------------------
    private async Task SignInAnonymously()
    {
        if (isSigningIn)
        {
            Debug.LogWarning("Already signing in!");
            return;
        }

        isSigningIn = true;

        try
        {
            Debug.Log("Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // If we get here => success
            Debug.Log($"Player Id: {AuthenticationService.Instance.PlayerId}");
            OnSignInComplete?.Invoke();  // Fire success event
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Authentication failed: {e.Message}");
            OnSignInFailed?.Invoke();    // Fire failure event
        }
        finally
        {
            isSigningIn = false;
        }
    }

    // -------------------------------------------------------------------
    // LOBBY CREATION & JOIN
    // -------------------------------------------------------------------
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
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, "Standard") }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            // Start host as Player 1 (creator)
            NetworkManager.Singleton.StartHost();  // <-- New!

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
            
            // **NEW**: Start the Netcode client
            NetworkManager.Singleton.StartClient();
            return currentLobby;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Join Lobby by ID failed: {e.Message}");
            return null;
        }
    }

    // -------------------------------------------------------------------
    // GET LOBBIES LIST - We assume you're already signed in by now!
    // -------------------------------------------------------------------
    public async Task<List<Lobby>> GetLobbiesList()
    {
        // Now that we're event-based, we do NOT sign in again here.
        // We rely on OnSignInComplete before calling this method.

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

    // -------------------------------------------------------------------
    // HELPER METHODS
    // -------------------------------------------------------------------
    private Unity.Services.Lobbies.Models.Player GetPlayer()
    {
        return new Unity.Services.Lobbies.Models.Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Public,
                    $"Player_{UnityEngine.Random.Range(0, 1000)}") }
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
