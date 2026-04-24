using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class GameNetwork : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameController gameController;
    [SerializeField] private UnityTransport transport;

    [Header("Network State - Synced to all clients")]
    public NetworkVariable<int> NetworkCurrentPlayer = new NetworkVariable<int>(1,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> NetworkPlacementCounter = new NetworkVariable<int>(0);
    public NetworkVariable<int> NetworkPlayer1Pieces = new NetworkVariable<int>(12);
    public NetworkVariable<int> NetworkPlayer2Pieces = new NetworkVariable<int>(12);
    public NetworkVariable<GamePhase> NetworkPhase = new NetworkVariable<GamePhase>(GamePhase.Placing);
    public NetworkList<int> NetworkBoard = new NetworkList<int>();
    public NetworkVariable<bool> NetworkGameEnded = new NetworkVariable<bool>(false);
    public NetworkVariable<int> NetworkWinner = new NetworkVariable<int>(0);

    [Header("Ready State - Synced to all clients")]
    public NetworkList<bool> NetworkPlayerReady = new NetworkList<bool>();

    [Header("Lobby Settings - Synced to all clients")]
    public NetworkVariable<string> NetworkGameType = new NetworkVariable<string>("12 Men's Morris",
    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<string> NetworkGameTime = new NetworkVariable<string>("10:00",
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event Action<string> OnGameTypeChanged;
    public event Action<string> OnGameTimeChanged;
    public event Action<int> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int[]> OnBoardUpdated;
    public event Action<int, int> OnPieceCountChanged;
    public event Action<int> OnGameOver;
    public event Action<bool> OnGameEnded;
    public event Action<int, bool> OnPlayerReadyChanged;
    public event Action OnGameStarted;

    private int _localPlayerNumber;
    private string _currentLobbyCode;

    public string GetLobbyCode() => _currentLobbyCode;
    public static GameNetwork Instance { get; private set; }
    public bool IsPersistent { get; private set; } = false;

    // ===== UNITY MESSAGES =====

    void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
        }

        if (Instance == null)
        {
            Instance = this;
            if (!IsPersistent)
            {
                DontDestroyOnLoad(gameObject);
                IsPersistent = true;
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        RegisterNetworkCallbacks();
    }

    void RegisterNetworkCallbacks()
    {
        NetworkCurrentPlayer.OnValueChanged += (oldVal, newVal) => OnTurnChanged?.Invoke(newVal);
        NetworkPhase.OnValueChanged += (oldVal, newVal) => OnPhaseChanged?.Invoke(newVal);

        NetworkBoard.OnListChanged += (NetworkListEvent<int> changeEvent) =>
        {
            OnBoardUpdated?.Invoke(GetBoardArray());
        };

        NetworkPlayer1Pieces.OnValueChanged += (oldVal, newVal) => OnPieceCountChanged?.Invoke(1, newVal);
        NetworkPlayer2Pieces.OnValueChanged += (oldVal, newVal) => OnPieceCountChanged?.Invoke(2, newVal);
        NetworkWinner.OnValueChanged += (oldVal, newVal) => { if (newVal > 0) OnGameOver?.Invoke(newVal); };
        NetworkGameEnded.OnValueChanged += (oldVal, newVal) => OnGameEnded?.Invoke(newVal);
        NetworkGameType.OnValueChanged += (oldVal, newVal) => OnGameTypeChanged?.Invoke(newVal);
        NetworkGameTime.OnValueChanged += (oldVal, newVal) => OnGameTimeChanged?.Invoke(newVal);

        NetworkPlayerReady.OnListChanged += (NetworkListEvent<bool> changeEvent) =>
        {
            if (changeEvent.Index < NetworkPlayerReady.Count)
            {
                int playerNum = changeEvent.Index + 1;
                bool isReady = NetworkPlayerReady[changeEvent.Index];
                OnPlayerReadyChanged?.Invoke(playerNum, isReady);
            }
        };
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeBoard();
            InitializeReadyList(2);
        }
        base.OnNetworkSpawn();
    }

    // ===== BOARD & GAME STATE =====

    private void InitializeBoard()
    {
        NetworkBoard.Clear();
        for (int i = 0; i < 25; i++)
        {
            NetworkBoard.Add(0);
        }
    }

    public void InitializeReadyList(int playerCount)
    {
        NetworkPlayerReady.Clear();
        for (int i = 0; i < playerCount; i++)
        {
            NetworkPlayerReady.Add(false);
        }
    }

    private int[] GetBoardArray()
    {
        int[] board = new int[Mathf.Max(NetworkBoard.Count, 25)];
        for (int i = 0; i < NetworkBoard.Count && i < board.Length; i++)
        {
            board[i] = NetworkBoard[i];
        }
        return board;
    }

    public T GetNetworkValue<T>(NetworkVariable<T> variable) => variable.Value;

    // ===== MOVE REQUESTS (Client → Server) =====

    public void RequestMove(int slotNumber)
    {
        if (!IsClient) return;
        RequestMoveServerRpc(slotNumber, NetworkManager.Singleton.LocalClient.ClientId);
    }

    [ServerRpc]
    private void RequestMoveServerRpc(int slotNumber, ulong clientId)
    {
        int playerNumber = GetPlayerNumber(clientId);

        if (playerNumber != NetworkCurrentPlayer.Value)
        {
            Debug.LogWarning($"Invalid turn: Player {playerNumber} tried to move during Player {NetworkCurrentPlayer.Value}'s turn");
            return;
        }

        if (NetworkGameEnded.Value)
        {
            Debug.LogWarning("Game has already ended");
            return;
        }

        int[] boardArray = GetBoardArray();

        if (!gameController.IsValidMoveForNetwork(slotNumber, playerNumber,
            NetworkPhase.Value, boardArray, NetworkPlacementCounter.Value,
            NetworkPlayer1Pieces.Value, NetworkPlayer2Pieces.Value))
        {
            Debug.LogWarning($"Invalid move: slot {slotNumber}");
            return;
        }

        ExecuteMoveOnServer(slotNumber, playerNumber);
    }

    private void ExecuteMoveOnServer(int slotNumber, int player)
    {
        int[] boardArray = GetBoardArray();

        var result = gameController.ExecuteMoveForNetwork(
            slotNumber, player, NetworkPhase.Value, boardArray,
            NetworkPlacementCounter.Value, NetworkPlayer1Pieces.Value, NetworkPlayer2Pieces.Value);

        // Update NetworkList
        for (int i = 0; i < result.NewBoardState.Length && i < NetworkBoard.Count; i++)
        {
            NetworkBoard[i] = result.NewBoardState[i];
        }

        NetworkPlacementCounter.Value = result.NewPlacementCounter;
        NetworkPlayer1Pieces.Value = result.NewPlayer1Pieces;
        NetworkPlayer2Pieces.Value = result.NewPlayer2Pieces;
        NetworkPhase.Value = result.NewPhase;

        if (result.IsGameOver)
        {
            NetworkWinner.Value = result.Winner;
            NetworkGameEnded.Value = true;
            Debug.Log($"GAME OVER: Player {result.Winner} wins!");
            return;
        }

        if (result.NewPhase != GamePhase.Capturing)
        {
            NetworkCurrentPlayer.Value = (player == 1) ? 2 : 1;
        }
    }

    // ===== READY STATE (Client → Server) =====

    [ServerRpc]
    public void SetReadyServerRpc(int playerNumber, bool isReady)
    {
        if (playerNumber < 1 || playerNumber > NetworkPlayerReady.Count)
            return;

        int index = playerNumber - 1;
        if (index < NetworkPlayerReady.Count)
        {
            NetworkPlayerReady[index] = isReady;
            Debug.Log($"[Server] Player {playerNumber} ready: {isReady}");
        }
    }

    public void SetLocalPlayerReady(bool isReady)
    {
        if (!IsClient) return;
        int playerNum = GetLocalPlayerNumber();
        SetReadyServerRpc(playerNum, isReady);
    }

    public bool AreAllPlayersReady()
    {
        if (NetworkPlayerReady.Count < 2) return false;

        for (int i = 0; i < NetworkPlayerReady.Count; i++)
        {
            if (!NetworkPlayerReady[i])
                return false;
        }
        return true;
    }

    // ===== GAME START (Host → All Clients) =====

    [ServerRpc]
    public void StartGameServerRpc()
    {
        if (!IsServer) return;
        Debug.Log("Server: Game starting!");
        StartGameClientRpc();
    }

    [ClientRpc]
    public void StartGameClientRpc()
    {
        Debug.Log("Client: Game starting signal received");
        OnGameStarted?.Invoke();
    }

    // ===== LOBBY CREATION (Host) =====

    public async Task<bool> CreateLobbyAsync(string lobbyName = "Morabaraba")
    {
        try
        {
            Debug.Log("[CreateLobby] Starting lobby creation...");

            await UnityServices.InitializeAsync();
            Debug.Log("[CreateLobby] Unity Services initialized");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("[CreateLobby] Authenticated as: " + AuthenticationService.Instance.PlayerId);
            }

            Debug.Log("[CreateLobby] Creating Relay allocation...");
            var relayAllocation = await RelayService.Instance.CreateAllocationAsync(2);
            Debug.Log("[CreateLobby] Relay allocation created");

            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(relayAllocation.AllocationId);
            Debug.Log("[CreateLobby] Relay join code obtained");

            Debug.Log("[CreateLobby] Creating Lobby Service lobby...");
            var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2,
                new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                });

            Debug.Log($"[CreateLobby] LOBBY CREATED: {lobby.LobbyCode}");

            _currentLobbyCode = lobby.LobbyCode;

            if (string.IsNullOrEmpty(_currentLobbyCode))
            {
                Debug.LogError("[CreateLobby] LobbyCode is NULL or EMPTY!");
                return false;
            }

            Debug.Log("[CreateLobby] Setting up Relay transport...");
            var serverData = AllocationUtils.ToRelayServerData(relayAllocation, "dtls");

            if (transport == null)
            {
                Debug.LogError("[CreateLobby] UnityTransport is NULL!");
                return false;
            }

            transport.SetRelayServerData(serverData);

            Debug.Log("[CreateLobby] Starting NetworkManager as Host...");
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[CreateLobby] NetworkManager.Singleton is NULL!");
                return false;
            }

            NetworkManager.Singleton.StartHost();
            Debug.Log("[CreateLobby] Host started successfully!");

            _localPlayerNumber = 1;
            AssignLocalPlayer(1);

            Debug.Log($"[CreateLobby] COMPLETE! Share code: {_currentLobbyCode}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CreateLobby] EXCEPTION: {e.Message}");
            Debug.LogError($"[CreateLobby] Stack: {e.StackTrace}");
            _currentLobbyCode = null;
            return false;
        }
    }

    // ===== LOBBY JOINING (Client) =====

    public async Task<bool> JoinLobbyAsync(string lobbyCode)
    {
        try
        {
            Debug.Log($"[JoinLobby] Attempting to join: {lobbyCode}");

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Debug.Log("[JoinLobby] Joining by code...");
            var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            Debug.Log($"[JoinLobby] Successfully joined: {lobby.Name}");

            if (lobby.Data == null || !lobby.Data.ContainsKey("RelayJoinCode"))
            {
                Debug.LogError("[JoinLobby] Lobby missing RelayJoinCode!");
                return false;
            }

            string relayJoinCode = lobby.Data["RelayJoinCode"].Value;
            Debug.Log("[JoinLobby] Got Relay join code");

            var relayAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            Debug.Log("[JoinLobby] Joined Relay allocation");

            if (transport == null)
            {
                Debug.LogError("[JoinLobby] UnityTransport is NULL!");
                return false;
            }

            var serverData = AllocationUtils.ToRelayServerData(relayAllocation, "dtls");
            transport.SetRelayServerData(serverData);

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[JoinLobby] NetworkManager.Singleton is NULL!");
                return false;
            }

            NetworkManager.Singleton.StartClient();
            Debug.Log("[JoinLobby] Started as Client");

            _localPlayerNumber = 2;
            AssignLocalPlayer(2);

            Debug.Log($"[JoinLobby] CONNECTED! You are Player {_localPlayerNumber}");
            return true;
        }
        catch (Exception e)
        {
            string errorMsg = e.Message.Contains("Lobby not found") || e.Message.Contains("404")
                ? "Lobby code invalid, expired, or full"
                : e.Message;

            Debug.LogError($"[JoinLobby] FAILED: {errorMsg}");
            return false;
        }
    }

    public async Task LeaveLobbyIfExists()
{
    try
    {
        // Check if NetworkManager is connected (indicates we're in a lobby)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("[LeaveLobby] NetworkManager is connected, shutting down...");
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[LeaveLobby] NetworkManager shutdown complete");
        }
        
        // Note: Unity Lobby Service automatically handles leaving when shutting down
        // No explicit LeaveLobbyAsync needed in most cases
        
        // Reset local state
        _localPlayerNumber = 0;
        _currentLobbyCode = null;
        
        Debug.Log("[LeaveLobby] Cleanup complete");
    }
    catch (Exception e)
    {
        Debug.LogError($"[LeaveLobby] Error during cleanup: {e.Message}");
    }
}

    // ===== PLAYER MANAGEMENT =====

    private void AssignLocalPlayer(int playerNumber)
    {
        _localPlayerNumber = playerNumber;
        Debug.Log($"You are Player {playerNumber}");
    }

    public int GetLocalPlayerNumber() => _localPlayerNumber;

    public bool IsLocalPlayerTurn() =>
        NetworkCurrentPlayer.Value == _localPlayerNumber && !NetworkGameEnded.Value;

    private int GetPlayerNumber(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClient.ClientId == clientId)
            return _localPlayerNumber;
        return (_localPlayerNumber == 1) ? 2 : 1;
    }

    // ===== UTILITY RPCs =====

    [ClientRpc]
    public void SyncBoardVisualsClientRpc(int[] boardState)
    {
        OnBoardUpdated?.Invoke(boardState);
    }

    [ServerRpc]
    public void UpdateLobbySettingsServerRpc(string gameType, string gameTime)
    {
        // Only server can update these
        if (!IsServer) return;

        NetworkGameType.Value = gameType;
        NetworkGameTime.Value = gameTime;

        Debug.Log($"[Server] Lobby settings updated: {gameType} | {gameTime}");
    }

    // Call this when host changes dropdowns
    public void UpdateLobbySettings(string gameType, string gameTime)
    {
        if (!IsClient) return; // Only clients call ServerRpc

        UpdateLobbySettingsServerRpc(gameType, gameTime);
    }

    // Helper to get current settings (works for host and client)
    public string GetCurrentGameType() => NetworkGameType.Value;
    public string GetCurrentGameTime() => NetworkGameTime.Value;

    // ===== CLEANUP =====

    private void OnDestroy()
    {
        // Clear events to prevent memory leaks
        OnTurnChanged = null;
        OnPhaseChanged = null;
        OnBoardUpdated = null;
        OnPieceCountChanged = null;
        OnGameOver = null;
        OnGameEnded = null;
        OnPlayerReadyChanged = null;
        OnGameStarted = null;
    }
}

[System.Serializable]
public struct MoveResult
{
    public int[] NewBoardState;
    public int NewPlacementCounter;
    public int NewPlayer1Pieces;
    public int NewPlayer2Pieces;
    public GamePhase NewPhase;
    public bool IsGameOver;
    public int Winner;
}