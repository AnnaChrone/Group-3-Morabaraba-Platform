using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;

public class GameController : NetworkBehaviour
{
    //public NetworkVariable<int> CurrentPlayer = new NetworkVariable<int>(1);
    //public NetworkVariable<int> PlacementCounter = new NetworkVariable<int>(0);
    //public NetworkVariable<int> Player1PiecesOnBoard = new NetworkVariable<int>(12);
    //public NetworkVariable<int> Player2PiecesOnBoard = new NetworkVariable<int>(12);
    //public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Placing);
    //public NetworkVariable<bool> GameEnded = new NetworkVariable<bool>(false);
    //public NetworkVariable<int> SelectedSlot = new NetworkVariable<int>(0);
    public NetworkVariable<int> CurrentPlayer = new NetworkVariable<int>(
    1,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

    public NetworkVariable<int> PlacementCounter = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Player1PiecesOnBoard = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Player2PiecesOnBoard = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(
        GamePhase.Placing,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> GameEnded = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> SelectedSlot = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Player1CapturesCount = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player2CapturesCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Track slot ownership: slotNumber -> playerID (0 = empty)
    public NetworkVariable<FixedString4096Bytes> SlotStates = new NetworkVariable<FixedString4096Bytes>("");

    [Header("Loading UI")]
    public GameObject loadingPanel;
    public TextMeshProUGUI loadingText;

    private bool networkReady = false;

    [Header("UI References")]
    public TextMeshProUGUI CurrentTurnIndicator;
    public SlotID[] allSlots;
    public SlotUI[] slotUIs;
    public GameObject Rules;
    private bool open =false;

    [Header("Win and Loss Screens")]
    public GameObject WinScreen;
    public TextMeshProUGUI WinReason;
    public TextMeshProUGUI WinGameStats;
    public GameObject LossScreen;
    public TextMeshProUGUI LossReason;
    public TextMeshProUGUI LossGameStats;

    [Header("Player piece depiction")]
    public TextMeshProUGUI Player1Pieces;
    public TextMeshProUGUI Player1Captures;

    public TextMeshProUGUI Player2Pieces;
    public TextMeshProUGUI Player2Captures;

    private SlotID selectedSlot = null;

    // Adjacency and mills data - Morabaraba specific
    private readonly Dictionary<int, int[]> adjacency = new Dictionary<int, int[]>()
    {
        {1, new int[] {2, 8, 9}}, {2, new int[] {1,3, 10}}, {3, new int[] {2,4, 11}},
        {4, new int[] {3,5, 12}}, {5, new int[] {4,6, 13}}, {6, new int[] {5,7, 14}},
        {7, new int[] {6,8, 15}}, {8, new int[] {7,1, 16}}, {9, new int[] {1,10, 16,17}},
        {10, new int[] {2,9, 11,18}}, {11, new int[] {3,10,12, 19}}, {12, new int[] {4,11,13,20}},
        {13, new int[] {5,12,14,21}}, {14, new int[] {6,13,15,22}}, {15, new int[] {7,14,16,23}},
        {16, new int[] {8,9,15,24}}, {17, new int[] {9,18,24}}, {18, new int[] {10,17,19}},
        {19, new int[] {11,18,20}}, {20, new int[] {12,19,21}}, {21, new int[] {13,20,22}},
        {22, new int[] {14,21,23}}, {23, new int[] {15,22,24}}, {24, new int[] {16,17,23}}
    };

    private readonly int[][] mills = new int[][]
    {
        new int[] {1,2,3}, new int[] {3,4,5}, new int[] {5,6,7}, new int[] {7,8,1},
        new int[] {9,10,11}, new int[] {11,12,13}, new int[] {13,14,15}, new int[] {15,16,9},
        new int[] {17,18,19}, new int[] {19,20,21}, new int[] {21,22,23}, new int[] {23,24,17},
        new int[] {1,9,17}, new int[] {2,10,18}, new int[] {3,11,19}, new int[] {4,12,20},
        new int[] {5,13,21}, new int[] {6,14,22}, new int[] {7,15,23}, new int[] {8,16,24}
    };

    void Awake()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (loadingText != null)
            loadingText.text = "Initializing Network...";
    }
    void Update()
    {
        if (!IsSpawned || !networkReady) return;

        // Highlight selected slot during moving phase
        if (CurrentPhase.Value == GamePhase.Moving && SelectedSlot.Value != 0)
        {
            var slot = GetSlotByNumber(SelectedSlot.Value);
            if (slot != null)
            {
                SlotUI slotUI = slot.GetComponent<SlotUI>();
                if (slotUI != null)
                {
                    slotUI.Highlight(CurrentPlayer.Value);
                }

                slot.GetComponent<UnityEngine.UI.Button>().interactable = true;
            }
        }

        // Optional debug logging at frame 120
        if (Time.frameCount == 120)
        {
            Debug.Log($"[GC] Frame 120 | IsSpawned: {IsSpawned} | networkReady: {networkReady}");
        }
    }

    //INITIALIZATION

    void OnEnable() => SetButtonsInteractable(false);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        localPlayerId = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
        Debug.Log($" OnNetworkSpawn called | IsServer: {IsServer} | ClientId: {NetworkManager.Singleton?.LocalClientId}");
        if (loadingText != null)
            loadingText.text = "Syncing Game State...";

        // Register callbacks
        CurrentPlayer.OnValueChanged += OnCurrentPlayerChanged;
        PlacementCounter.OnValueChanged += OnPlacementCounterChanged;
        CurrentPhase.OnValueChanged += OnPhaseChanged;
        SlotStates.OnValueChanged += OnSlotStatesChanged;
        Player1CapturesCount.OnValueChanged += OnCaptureChanged;
        Player2CapturesCount.OnValueChanged += OnCaptureChanged;

        if (IsServer)
        {
            InitializeGameState();
            Debug.Log(" Game state initialized");
        }

        ApplySlotStatesToVisuals();
        UpdateTurnIndicator();

        StartCoroutine(FinishLoading());
    }
    void OnCaptureChanged(int oldVal, int newVal)
    {
        UpdatePiecesToPlaceUI();
    }
    System.Collections.IEnumerator FinishLoading()
    {
        yield return null;
        yield return null;

        networkReady = true;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
            Debug.Log(" Loading complete - Game ready!");
        }

        SetButtonsInteractable(true);
        UpdatePiecesToPlaceUI();
    }

    void SetButtonsInteractable(bool enabled)
    {
        foreach (var slot in allSlots)
        {
            var btn = slot.GetComponent<UnityEngine.UI.Button>();
            if (btn != null) btn.interactable = enabled;
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentPlayer.OnValueChanged -= OnCurrentPlayerChanged;
        PlacementCounter.OnValueChanged -= OnPlacementCounterChanged;
        CurrentPhase.OnValueChanged -= OnPhaseChanged;
        SlotStates.OnValueChanged -= OnSlotStatesChanged;
        Player1CapturesCount.OnValueChanged -= OnCaptureChanged;
        Player2CapturesCount.OnValueChanged -= OnCaptureChanged;
        base.OnNetworkDespawn();
    }

    void Start()
    {
        Debug.Log($"GameController Start() | IsListening: {NetworkManager.Singleton?.IsListening}");
        Debug.Log($"GameController IsSpawned: {IsSpawned}");
        Debug.Log($"NetworkManager active: {NetworkManager.Singleton != null}");
        Debug.Log($"IsListening: {NetworkManager.Singleton?.IsListening}");
    }

    void InitializeGameState()
    {
        CurrentPlayer.Value = 1;
        PlacementCounter.Value = 0;
        
        Player1PiecesOnBoard.Value = 0;
        Player2PiecesOnBoard.Value = 0;
        CurrentPhase.Value = GamePhase.Placing;
        GameEnded.Value = false;

        var states = new List<string>();
        for (int i = 1; i <= 24; i++) states.Add($"{i}:0");
        SlotStates.Value = string.Join(",", states);
    }

    void InitializeSlotStates()
    {
        var states = new List<string>();
        for (int i = 1; i <= 24; i++) states.Add($"{i}:0");
        SlotStates.Value = string.Join(",", states);
        ApplySlotStatesToVisuals();
    }

    //Input Handling

    public void OnSlotClicked(SlotID slot)
    {
        Debug.Log($"[CLIENT] OnSlotClicked called for Slot {slot.slotNumber}");

        if (!networkReady)
        {
            Debug.LogWarning("Game still loading...");
            return;
        }

        if (!IsSpawned)
        {
            Debug.LogError("GameController not spawned!");
            return;
        }

        if (GameEnded.Value)
        {
            Debug.LogWarning(" Game has ended!");
            return;
        }

        bool isMyTurn = IsLocalPlayerTurn();
       // Debug.Log($" [CLIENT] IsLocalPlayerTurn()={isMyTurn}");
       // Debug.Log($" [CLIENT] CurrentPlayer.Value={CurrentPlayer.Value}");
       // Debug.Log($" [CLIENT] CurrentPhase.Value={CurrentPhase.Value}");

        if (!isMyTurn)
        {
            Debug.LogWarning(" Not your turn!");
            return;
        }

        RequestMoveServerRpc(slot.slotNumber, CurrentPhase.Value);

        Debug.Log($" [CLIENT] RPC CALLED SUCCESSFULLY");
    }
    int localPlayerId;
    bool IsLocalPlayerTurn()
    {
        if (!NetworkManager.Singleton) return false;
        localPlayerId = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
        return CurrentPlayer.Value == localPlayerId;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMoveServerRpc(int slotNumber, GamePhase phase, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"📡 SERVER RECEIVED RPC from Client {rpcParams.Receive.SenderClientId}");

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int requestingPlayer = (senderClientId == 0) ? 1 : 2;

        Debug.Log($"🔍 Validation: CurrentPlayer={CurrentPlayer.Value}, RequestingPlayer={requestingPlayer}");

        if (CurrentPlayer.Value != requestingPlayer)
        {
            Debug.LogWarning($"❌ Wrong player! Rejecting request. Current={CurrentPlayer.Value}, Requested={requestingPlayer}");
            return;
        }

        SlotID slot = GetSlotByNumber(slotNumber);
        if (slot == null)
        {
            Debug.LogError($"❌ Slot {slotNumber} not found!");
            return;
        }

        Debug.Log($"✅ Processing move for Slot {slotNumber} in Phase {phase}");

        switch (phase)
        {
            case GamePhase.Placing:
                ServerHandlePlacing(slot);
                break;
            case GamePhase.Moving:
                ServerHandleMoving(slot);
                break;
            case GamePhase.Capturing:
                ServerHandleCapturing(slot);
                break;
            default:
                Debug.LogWarning($"⚠️ Unknown phase: {phase}");
                break;
        }
    }

    //Serverside game logic
    void ServerHandlePlacing(SlotID slot)
    {
        if (GetSlotOwner(slot.slotNumber) != 0) return;

        SetSlotOwner(slot.slotNumber, CurrentPlayer.Value);
        //Change display of current player's pieces left
        if (CurrentPlayer.Value == 1)
        {
            Player1PiecesOnBoard.Value++;
        }
        else
        {
            Player2PiecesOnBoard.Value++;
        }
        PlacementCounter.Value++;
        PlaySoundClientRpc("Place");
        Debug.Log("Playing PLACE AUDIO");
        if (CheckMill(slot.slotNumber, CurrentPlayer.Value))
        {
            CurrentPhase.Value = GamePhase.Capturing;
            return;
        }

        if (PlacementCounter.Value >= 24)
        {
            CurrentPhase.Value = GamePhase.Moving;
        }
        UpdatePiecesClientRpc();
        EndTurn();
    }

    void ServerHandleMoving(SlotID slot)
    {
        int currentPlayer = CurrentPlayer.Value;


        if (SelectedSlot.Value == 0)
        {
            if (GetSlotOwner(slot.slotNumber) != currentPlayer)
                return;

            SelectedSlot.Value = slot.slotNumber;
            PlaySoundClientRpc("Select");
            return;
        }

        SlotID fromSlot = GetSlotByNumber(SelectedSlot.Value);

        if (!IsValidMove(fromSlot, slot, currentPlayer))
        {
            SelectedSlot.Value = 0;
            PlaySoundClientRpc("Invalid");
            return;
        }


        bool isFlying =
            (currentPlayer == 1 ? Player1PiecesOnBoard.Value : Player2PiecesOnBoard.Value) <= 3;

        SetSlotOwner(SelectedSlot.Value, 0);
        SetSlotOwner(slot.slotNumber, currentPlayer);

        // Audio
        if (isFlying)
        {
            PlaySoundClientRpc("Fly");
        }
        else
        {
            PlaySoundClientRpc("Move");
        }

        SelectedSlot.Value = 0;


        if (CheckMill(slot.slotNumber, currentPlayer))
        {
            CurrentPhase.Value = GamePhase.Capturing;
            return;
        }
        UpdatePiecesClientRpc();
        EndTurn();
    }

    bool IsValidMove(SlotID from, SlotID to, int player)
    {
        if (GetSlotOwner(to.slotNumber) != 0) return false;

        int piecesOnBoard = (player == 1) ? Player1PiecesOnBoard.Value : Player2PiecesOnBoard.Value;

        if (piecesOnBoard <= 3)
            return true;

        return IsAdjacent(from, to);
    }

    void ServerHandleCapturing(SlotID slot)
    {
        int opponent = (CurrentPlayer.Value == 1) ? 2 : 1;
        if (GetSlotOwner(slot.slotNumber) != opponent) return;

        if (slot.isInMill && OpponentHasFreePiece(opponent))
            return;

        SetSlotOwner(slot.slotNumber, 0);

        if (CurrentPlayer.Value == 1)
        {
            Player2PiecesOnBoard.Value--;
            Player1CapturesCount.Value++;
            UpdateCaptureUIClientRpc(1, Player1CapturesCount.Value);
        }
        else
        {
            Player1PiecesOnBoard.Value--;
            Player2CapturesCount.Value++;
            UpdateCaptureUIClientRpc(2, Player2CapturesCount.Value);
        }
       
        PlaySoundClientRpc("Capture");
        CheckBrokenMills(opponent);
        if (HasWon())
        {
            ServerGameOver(CurrentPlayer.Value,WinReason.text,LossReason.text);
            return;
        }

        CurrentPhase.Value = (PlacementCounter.Value >= 24) ? GamePhase.Moving : GamePhase.Placing;
        UpdatePiecesClientRpc();
        EndTurn();
    }

    void EndTurn()
    {
        CurrentPlayer.Value = (CurrentPlayer.Value == 1) ? 2 : 1;
    }

    void ServerGameOver(int winner, string winReason, string lossReason)
    {
        GameEnded.Value = true;
        GameOverClientRpc(winner, winReason, lossReason);
    }

    [ClientRpc]
    void GameOverClientRpc(int winner, string winReason, string lossReason)
    {
        Debug.Log($"GAME OVER: Player {winner} wins!");

        if (winner == localPlayerId)
        {
            PlayerData.Instance.AddWin();
            WinReason.text = winReason;
            WinScreen.SetActive(true);
            AudioController.Instance?.PlayAudio("Win");
        }
        else
        {
            PlayerData.Instance.AddLoss();
            LossReason.text = lossReason;
            LossScreen.SetActive(true);
            AudioController.Instance?.PlayAudio("Lose");
        }
    }
    void UpdatePiecesToPlaceUI()
    {
        // Pieces left to place
        int player1Placed = (PlacementCounter.Value + 1) / 2;
        int player2Placed = PlacementCounter.Value / 2;

        int player1Left = Mathf.Max(0, 12 - player1Placed);
        int player2Left = Mathf.Max(0, 12 - player2Placed);

        Player1Pieces.text = new string('●', player1Left);
        Player2Pieces.text = new string('●', player2Left);

        Player1Captures.text = new string('●', Player1CapturesCount.Value);
        Player2Captures.text = new string('●', Player2CapturesCount.Value);
    }

    [ClientRpc]
    void UpdateCaptureUIClientRpc(int player, int newValue)
    {
        if (player == 1)
            Player1Captures.text = new string('●', newValue);
        else
            Player2Captures.text = new string('●', newValue);
    }

    [ClientRpc]
    void UpdatePiecesClientRpc()
    {
        UpdatePiecesToPlaceUI();
    }

    //Audio
    [ClientRpc]
    void PlaySoundClientRpc(string AudioType)
    {
        AudioController.Instance?.PlayAudio(AudioType);
    }

    //Slot State Management

    void SetSlotOwner(int slotNumber, int player)
    {
        var states = ParseSlotStates();
        states[slotNumber] = player;
        SlotStates.Value = SerializeSlotStates(states);
    }

    int GetSlotOwner(int slotNumber)
    {
        var states = ParseSlotStates();
        return states.TryGetValue(slotNumber, out int owner) ? owner : 0;
    }

    Dictionary<int, int> ParseSlotStates()
    {
        var result = new Dictionary<int, int>();
        string statesString = SlotStates.Value.ToString();
        if (string.IsNullOrEmpty(statesString)) return result;

        foreach (var entry in SlotStates.Value.ToString().Split(','))
        {
            var parts = entry.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int slot) && int.TryParse(parts[1], out int owner))
            {
                result[slot] = owner;
            }
        }
        return result;
    }

    string SerializeSlotStates(Dictionary<int, int> states)
    {
        var entries = new List<string>();
        foreach (var kv in states) entries.Add($"{kv.Key}:{kv.Value}");
        return string.Join(",", entries);
    }

    void OnSlotStatesChanged(FixedString4096Bytes oldVal, FixedString4096Bytes newVal)
    {
        if (IsSpawned)
            ApplySlotStatesToVisuals();
    }

    void ApplySlotStatesToVisuals()
    {
        var states = ParseSlotStates();
        foreach (var slot in allSlots)
        {
            if (states.TryGetValue(slot.slotNumber, out int owner))
            {
                if (owner == 0)
                    slot.ClearSlot();
                else
                    slot.SetOccupant(owner);
            }
        }
        UpdateAllMills();
    }

    //Mill and Win Logic

    bool CheckMill(int slotNumber, int player)
    {
        foreach (var mill in mills)
        {
            if (!mill.Contains(slotNumber)) continue;

            int count = 0;
            foreach (int s in mill)
                if (GetSlotOwner(s) == player) count++;

            if (count == 3)
            {
                foreach (int s in mill)
                    GetSlotByNumber(s).SetMillStatus(true);
                PlaySoundClientRpc("FormMill");
                return true;
            }
        }
        return false;
    }

    void CheckBrokenMills(int player)
    {
        foreach (var mill in mills)
        {
            bool wasMill = true;

            foreach (int slot in mill)
            {
                if (GetSlotOwner(slot) != player)
                {
                    wasMill = false;
                    break;
                }
            }

            if (!wasMill)
            {
                PlaySoundClientRpc("BreakMill");
                return;
            }
        }
    }

    void UpdateAllMills()
    {
        foreach (var slot in allSlots) slot.SetMillStatus(false);

        foreach (var mill in mills)
        {
            int owner = GetSlotOwner(mill[0]);
            if (owner != 0 &&
                GetSlotOwner(mill[1]) == owner &&
                GetSlotOwner(mill[2]) == owner)
            {
                foreach (int s in mill)
                {
                    var slot = GetSlotByNumber(s);
                    slot.SetMillStatus(true);
                    slot.slotUI.HighlightMill(owner);
                }
            }
        }
    }

    bool OpponentHasFreePiece(int opponent)
    {
        foreach (var slot in allSlots)
        {
            if (GetSlotOwner(slot.slotNumber) == opponent && !slot.isInMill)
                return true;
        }
        return false;
    }

    bool HasWon()
    {
        int opponent = (CurrentPlayer.Value == 1) ? 2 : 1;
        int opponentPieces = (opponent == 1) ? Player1PiecesOnBoard.Value : Player2PiecesOnBoard.Value;

        if (PlacementCounter.Value < 24) return false;
        if (opponentPieces <= 2)
        {
            Debug.Log("Opponent has less than 2 pieces left");
            WinReason.text = "Your opponent has 2 of less pieces left!";
            LossReason.text = "You have 2 or less pieces left!";
            return true;
        }

        bool canFly = opponentPieces <= 3;
        foreach (var slot in allSlots)
        {
            if (GetSlotOwner(slot.slotNumber) != opponent) continue;

            if (canFly)
            {
                if (allSlots.Any(s => GetSlotOwner(s.slotNumber) == 0))
                    return false;
            }
            else
            {
                foreach (int adj in adjacency[slot.slotNumber])
                    if (GetSlotOwner(adj) == 0) return false;
            }
        }

        Debug.Log("Your opponent has no more valid moves");
        WinReason.text = "Your opponent has no more valid moves!";
        LossReason.text = "You have no more valid moves!";
        return true;
    }

    public void OnForfeit()
    {
        if (!IsSpawned || GameEnded.Value)
            return;

        SubmitForfeitServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitForfeitServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameEnded.Value)
            return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int forfeitingPlayer = (senderClientId == 0) ? 1 : 2;
        int winner = (forfeitingPlayer == 1) ? 2 : 1;

        Debug.Log($" Player {forfeitingPlayer} forfeited. Player {winner} wins.");

        ServerGameOver(
            winner,
            "Your opponent forfeited!",
            "You forfeited the match!"
        );
    }

    //UI UPDATES
    public void onRules()
    {
        open = !open;
        Rules.SetActive(open);
    }

    public void onGoToLobby()
    {
        StartCoroutine(ReturnToLobbyRoutine());
    }

    System.Collections.IEnumerator ReturnToLobbyRoutine()
    {
        UIManager.Instance.NotifyReturnedToLobby();
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("GameScene");


    }

    [ServerRpc(RequireOwnership = false)]
    void NotifyReturnedToLobbyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        UIManager.Instance.PlayerReturnedToLobby(clientId);
    }

    void OnCurrentPlayerChanged(int oldVal, int newVal) => UpdateTurnIndicator();
    void OnPhaseChanged(GamePhase oldVal, GamePhase newVal)
    {
        UpdateTurnIndicator();
        UpdatePiecesToPlaceUI(); 
    }

    void UpdateTurnIndicator()
    {
        if (CurrentTurnIndicator == null) return;

        if (GameEnded.Value)
        {
            CurrentTurnIndicator.text = "Game Over";
            return;
        }

        CurrentTurnIndicator.text = $"Player {CurrentPlayer.Value} Turn - {CurrentPhase.Value}";
        CurrentTurnIndicator.color = CurrentPlayer.Value == 1 ? Color.green : Color.red;
    }

    void OnPlacementCounterChanged(int oldVal, int newVal)
    {
        UpdateTurnIndicator();
        UpdatePiecesToPlaceUI(); // THis is what updates clients
    }

    //Helper Functions
    SlotID GetSlotByNumber(int number) => allSlots.FirstOrDefault(s => s.slotNumber == number);
    bool IsAdjacent(SlotID from, SlotID to) => adjacency[from.slotNumber].Contains(to.slotNumber);

    void GameOver(int winner) => ServerGameOver(CurrentPlayer.Value,WinReason.text,LossReason.text);
}

public enum GamePhase
{
    Placing,
    Moving,
    Capturing,
    End
}