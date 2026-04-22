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

    // Make NetworkBoard public
    public NetworkList<int> NetworkBoard = new NetworkList<int>();
    public NetworkVariable<bool> NetworkGameEnded = new NetworkVariable<bool>(false);
    public NetworkVariable<int> NetworkWinner = new NetworkVariable<int>(0);

    public event Action<int> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int[]> OnBoardUpdated;
    public event Action<int, int> OnPieceCountChanged;
    public event Action<int> OnGameOver;
    public event Action<bool> OnGameEnded;

    private int _localPlayerNumber;

    private string _currentLobbyCode;
    public string GetLobbyCode() => _currentLobbyCode;

    public static GameNetwork Instance { get; private set; }

    [ServerRpc]
    public void StartGameServerRpc()
    {
        // Ensure only server/host can start
        if (!IsServer) return;

        Debug.Log("Server: Game starting!");

        // Notify all clients via ClientRpc
        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        Debug.Log("Client: Game starting signal received");
        // Optional: Trigger any client-side game start logic here
        // The UI switch is handled locally by each client's LobbyUI
    }

    void Awake()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager not found in scene!");
            enabled = false;
            return;
        }
        RegisterNetworkCallbacks();
    }

    void RegisterNetworkCallbacks()
    {
        NetworkCurrentPlayer.OnValueChanged += (oldVal, newVal) => OnTurnChanged?.Invoke(newVal);
        NetworkPhase.OnValueChanged += (oldVal, newVal) => OnPhaseChanged?.Invoke(newVal);

        // Fix: NetworkList OnListChanged takes NetworkListEvent<int> parameter
        NetworkBoard.OnListChanged += (NetworkListEvent<int> changeEvent) =>
        {
            OnBoardUpdated?.Invoke(GetBoardArray());
        };

        NetworkPlayer1Pieces.OnValueChanged += (oldVal, newVal) => OnPieceCountChanged?.Invoke(1, newVal);
        NetworkPlayer2Pieces.OnValueChanged += (oldVal, newVal) => OnPieceCountChanged?.Invoke(2, newVal);
        NetworkWinner.OnValueChanged += (oldVal, newVal) => { if (newVal > 0) OnGameOver?.Invoke(newVal); };
        NetworkGameEnded.OnValueChanged += (oldVal, newVal) => OnGameEnded?.Invoke(newVal);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialize board with 25 zeros (index 0 unused, 1-24 used)
            NetworkBoard.Clear();
            for (int i = 0; i < 25; i++)
            {
                NetworkBoard.Add(0);
            }
        }
        base.OnNetworkSpawn();
    }

    private int[] GetBoardArray()
    {
        int[] board = new int[NetworkBoard.Count];
        for (int i = 0; i < NetworkBoard.Count; i++)
        {
            board[i] = NetworkBoard[i];
        }
        return board;
    }

    // Add this method back for GameController
    public T GetNetworkValue<T>(NetworkVariable<T> variable) => variable.Value;

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
        for (int i = 0; i < result.NewBoardState.Length; i++)
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

    // ===== LOBBY & RELAY INTEGRATION =====

    public async Task<bool> CreateLobbyAsync(string lobbyName = "Morabaraba")
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            var relayAllocation = await RelayService.Instance.CreateAllocationAsync(2);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(relayAllocation.AllocationId);

            var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2,
                new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                });

            // Store the lobby code for UI access
            _currentLobbyCode = lobby.LobbyCode;

            var serverData = AllocationUtils.ToRelayServerData(relayAllocation, "dtls");
            transport.SetRelayServerData(serverData);
            NetworkManager.Singleton.StartHost();

            _localPlayerNumber = 1;
            AssignLocalPlayer(1);

            Debug.Log($"Lobby created: {lobby.Id} | Code: {lobby.LobbyCode}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            _currentLobbyCode = null;
            return false;
        }
    }

    public async Task<bool> JoinLobbyAsync(string lobbyCode)
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            var query = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        QueryFilter.FieldOptions.Name,
                        lobbyCode,
                        QueryFilter.OpOptions.EQ)
                }
            };

            var result = await LobbyService.Instance.QueryLobbiesAsync(query);
            if (result.Results.Count == 0)
            {
                Debug.LogError("Lobby not found");
                return false;
            }

            var lobby = result.Results[0];
            string relayJoinCode = lobby.Data["RelayJoinCode"].Value;

            var relayAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            var serverData = AllocationUtils.ToRelayServerData(relayAllocation, "dtls");
            transport.SetRelayServerData(serverData);
            NetworkManager.Singleton.StartClient();

            _localPlayerNumber = 2;
            AssignLocalPlayer(2);

            Debug.Log("Joined lobby");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            return false;
        }
    }

    private void AssignLocalPlayer(int playerNumber)
    {
        _localPlayerNumber = playerNumber;
        Debug.Log($"You are Player {playerNumber}");
    }

    public int GetLocalPlayerNumber() => _localPlayerNumber;
    public bool IsLocalPlayerTurn() => NetworkCurrentPlayer.Value == _localPlayerNumber && !NetworkGameEnded.Value;

    private int GetPlayerNumber(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClient.ClientId == clientId)
            return _localPlayerNumber;
        return (_localPlayerNumber == 1) ? 2 : 1;
    }

    [ClientRpc]
    public void SyncBoardVisualsClientRpc(int[] boardState)
    {
        OnBoardUpdated?.Invoke(boardState);
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