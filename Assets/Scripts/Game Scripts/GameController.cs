using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class GameSnapshot //This allows for the rewind function to work
{
    public int currentPlayer;
    public int placementCounter;

    public int player1Pieces;
    public int player2Pieces;

    public int player1Captures;
    public int player2Captures;

    public GamePhase phase;

    public int selectedSlot;

    public string slotStates;
}
public class GameController : NetworkBehaviour
{
    public NetworkVariable<float> TotalGameTime = new NetworkVariable<float>(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

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

    //Rewind Variabe counters
    public NetworkVariable<int> Player1Rewinds = new NetworkVariable<int>(
    3,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

    public NetworkVariable<int> Player2Rewinds = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsGamePaused = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> PauseRequesterClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Game Type Configuration")]
    public GameTypeData morabarabaData;
    public GameTypeData sixMensMorrisData;
    private GameTypeData currentGameType;
    private Dictionary<int, int[]> adjacency;
    private List<int[]> mills;
    private int totalSlots;
    private int piecesPerPlayer;
    private int totalPlacements;

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
    public GameObject DrawScreen;
    public TextMeshProUGUI DrawReason;

    [Header("Rewind UI")]
    public TextMeshProUGUI RewindText;
    public Button rewindButton;
    public GameObject rewindEffectPanel;

    [Header("Player piece depiction")]
    public TextMeshProUGUI Player1Pieces;
    public TextMeshProUGUI Player1Captures;

    public TextMeshProUGUI Player2Pieces;
    public TextMeshProUGUI Player2Captures;

    [Header("Timers")]
    private float lastTimerUpdate = 0f;
    private const float TIMER_UPDATE_INTERVAL = 0.1f; // Update UI 10 times per second
    public TextMeshProUGUI TimerUI;
    private float player1Time;
    private float player2Time;

    [Header("Pause Logic")]
    private float lastPauseRequestTime = 0f;
    private const float PAUSE_COOLDOWN = 0.5f;
    public GameObject pausePanel;
  //  public TextMeshProUGUI pauseStatusText;
    public Button forfeitButton;
    public Button rulesButton;
    public Button closeGameButton;
    private bool isLocallyPaused = false;

    public enum TimerMode
    {
        None,
        GameTimer,
        TurnTimer
    }

    private TimerMode timerMode = TimerMode.None;

    private float gameTimeRemaining;
    private float turnTimeRemaining;
    private float timerTick = 1f;
    private float timerAcc = 0f;
    private bool timerRunning = false;

    private SlotID selectedSlot = null;

    [Header("Stats tracking")]
    private float gameStartTime;

    //rewind functionality
    private List<GameSnapshot> gameHistory = new List<GameSnapshot>();
    void Awake()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (loadingText != null)
            loadingText.text = "Initializing Network...";

        LoadGameTypeData();
    }
    void Update()
    {
        if (IsServer && IsGamePaused.Value && pauseStartTime > 0)
        {
            if (Time.time - pauseStartTime >= MAX_PAUSE_DURATION)
            {
                Debug.Log("Auto-resuming game - maximum pause duration reached");
                IsGamePaused.Value = false;
                PauseRequesterClientId.Value = 0;
                Time.timeScale = 1f;
            }
        }
        // Don't process game logic if paused
        if (IsGamePaused.Value)
            return;

        // Check for pause key (Escape)
        if (Input.GetKeyDown(KeyCode.Escape) && !GameEnded.Value && IsSpawned)
        {
            if (IsGamePaused.Value)
                RequestResume();
            else
                RequestPause();
        }

        if (!IsSpawned || !networkReady || GameEnded.Value || !timerRunning)
            return;

        float dt = Time.deltaTime;
        timerAcc += dt;

        // Update timer logic on server only
        if (IsServer)
        {
            if (timerMode == TimerMode.GameTimer)
            {
                gameTimeRemaining -= dt;

                if (gameTimeRemaining <= 0f)
                {
                    gameTimeRemaining = 0f;
                    EndGameByTime(0); // Draw by timeout
                    return;
                }
            }
            else if (timerMode == TimerMode.TurnTimer)
            {
                turnTimeRemaining -= dt;

                if (turnTimeRemaining <= 0f)
                {
                    turnTimeRemaining = 0f;
                    // Time's up - end turn
                    EndTurn();
                    ResetTurnTimer();

                    // Notify clients of timer reset
                    UpdateTimerClientRpc(gameTimeRemaining, turnTimeRemaining, timerMode);
                    return;
                }
            }

            // Send timer updates to clients periodically
            if (timerAcc >= TIMER_UPDATE_INTERVAL)
            {
                timerAcc = 0f;
                UpdateTimerClientRpc(gameTimeRemaining, turnTimeRemaining, timerMode);
            }
        }

        // Highlight selected slot during moving phase (moved outside server check)
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
    }
    //INITIALIZATION

    void OnEnable()
    {
        SetButtonsInteractable(false);
        // Register pause callback
        IsGamePaused.OnValueChanged += OnPauseStateChanged;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"=== OnNetworkSpawn START ===");
        Debug.Log($"totalSlots: {totalSlots}, piecesPerPlayer: {piecesPerPlayer}");
        Debug.Log($"adjacency null? {adjacency == null}, mills null? {mills == null}");

        // Read current settings
        string gameType = GameSettings.GameType;
        string gameTimeSetting = GameSettings.GameTime;

        Debug.Log($"=== GAMECONTROLLER ONNETWORKSPAWN ===");
        Debug.Log($"Game Type from settings: {gameType}");
        Debug.Log($"Game Time from settings: {gameTimeSetting}");
        Debug.Log($"IsServer: {IsServer}");
        Debug.Log($"LocalClientId: {NetworkManager.Singleton?.LocalClientId}");

        localPlayerId = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;

        if (loadingText != null)
            loadingText.text = "Syncing Game State...";

        // Register callbacks
        CurrentPlayer.OnValueChanged += OnCurrentPlayerChanged;
        PlacementCounter.OnValueChanged += OnPlacementCounterChanged;
        CurrentPhase.OnValueChanged += OnPhaseChanged;
        SlotStates.OnValueChanged += OnSlotStatesChanged;
        Player1CapturesCount.OnValueChanged += OnCaptureChanged;
        Player2CapturesCount.OnValueChanged += OnCaptureChanged;
        Player1Rewinds.OnValueChanged += OnRewindChanged;
        Player2Rewinds.OnValueChanged += OnRewindChanged;
        TotalGameTime.OnValueChanged += OnTotalGameTimeChanged;
        IsGamePaused.OnValueChanged += OnPauseStateChanged;

        if (IsServer)
        {
            InitializeGameState();
            SetupTimer(gameTimeSetting);
            Debug.Log($"Server: Timer setup with {gameTimeSetting}");
        }

        ApplySlotStatesToVisuals();
        UpdateTurnIndicator();

        StartCoroutine(FinishLoading());
    }
    void LoadGameTypeData()
    {
        HardcodeGameData(); // Just uses hardcoded data directly
    }

    void OnTotalGameTimeChanged(float oldVal, float newVal)
    {
        // Optional: Update UI if needed
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
        UpdateRewindUI();
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
        Player1Rewinds.OnValueChanged -= OnRewindChanged;
        Player2Rewinds.OnValueChanged -= OnRewindChanged;
        TotalGameTime.OnValueChanged -= OnTotalGameTimeChanged;
        IsGamePaused.OnValueChanged -= OnPauseStateChanged;
        base.OnNetworkDespawn();
    }

    private string gameType;
    void Start()
    {
        Debug.Log($"GameController Start() | IsListening: {NetworkManager.Singleton?.IsListening}");
        Debug.Log($"GameController IsSpawned: {IsSpawned}");
        Debug.Log($"NetworkManager active: {NetworkManager.Singleton != null}");
        Debug.Log($"Current GameSettings - Type: {GameSettings.GameType}, Time: {GameSettings.GameTime}");
        gameType = GameSettings.GameType;   
    }

    void InitializeGameState()
    {
        if (totalSlots == 0)
        {
            Debug.LogError("totalSlots is 0! Loading default values.");
            totalSlots = 24;
            piecesPerPlayer = 12;
        }

        CurrentPlayer.Value = 1;
        PlacementCounter.Value = 0;
        Player1PiecesOnBoard.Value = 0;
        Player2PiecesOnBoard.Value = 0;
        CurrentPhase.Value = GamePhase.Placing;
        GameEnded.Value = false;
        gameStartTime = Time.time;
        TotalGameTime.Value = 0;

        var states = new List<string>();
        for (int i = 1; i <= totalSlots; i++) states.Add($"{i}:0");
        SlotStates.Value = string.Join(",", states);

        Debug.Log($"Game state initialized with {totalSlots} slots, {totalPlacements} total placements needed");
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

        Debug.Log($"Validation: CurrentPlayer={CurrentPlayer.Value}, RequestingPlayer={requestingPlayer}");

        if (CurrentPlayer.Value != requestingPlayer)
        {
            Debug.LogWarning($"Wrong player! Rejecting request. Current={CurrentPlayer.Value}, Requested={requestingPlayer}");
            return;
        }

        SlotID slot = GetSlotByNumber(slotNumber);
        if (slot == null)
        {
            Debug.LogError($"Slot {slotNumber} not found!");
            return;
        }

        Debug.Log($"Processing move for Slot {slotNumber} in Phase {phase}");

        

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
                Debug.LogWarning($"Unknown phase: {phase}");
                break;
        }
    }

    //Serverside game logic
    void ServerHandlePlacing(SlotID slot)
    {
        if (GetSlotOwner(slot.slotNumber) != 0) return;
        SaveSnapshot(); //stores snapshot per turn
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

        if (PlacementCounter.Value >= totalPlacements)
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

        SaveSnapshot(); //stores snapshot per turn
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
        SaveSnapshot(); //stores snapshot per turn
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

        CurrentPhase.Value = (PlacementCounter.Value >= totalPlacements) ? GamePhase.Moving : GamePhase.Placing; UpdatePiecesClientRpc();
        EndTurn();
    }

    void EndTurn()
    {
        CurrentPlayer.Value = (CurrentPlayer.Value == 1) ? 2 : 1;

        // Reset turn timer when turn changes
        if (timerMode == TimerMode.TurnTimer)
        {
            ResetTurnTimer();
        }
    }

    void ServerGameOver(int winner, string winReason, string lossReason)
    {
        if (GameEnded.Value) return;
        if (IsGamePaused.Value)
        {
            IsGamePaused.Value = false;
            Time.timeScale = 1f;
        }

        GameEnded.Value = true;

        // Calculate total time taken (same for both players)
        float totalGameTime = Time.time - gameStartTime;
        TotalGameTime.Value = totalGameTime;

        Debug.Log($"Game Over - Total Time: {FormatTime(totalGameTime)}, P1 Captures: {Player1CapturesCount.Value}, P2 Captures: {Player2CapturesCount.Value}");

        GameOverClientRpc(winner, winReason, lossReason,
                         TotalGameTime.Value,
                         Player1CapturesCount.Value, Player2CapturesCount.Value);
    }

    [ClientRpc]
    void GameOverClientRpc(int winner, string winReason, string lossReason,
                       float totalGameTime,
                       int player1Caps, int player2Caps)
    {
        Debug.Log($"GAME OVER: Player {winner} wins!");
        Debug.Log($"Total Game Time: {FormatTime(totalGameTime)}, P1 Caps: {player1Caps}, P2 Caps: {player2Caps}");
        UpdateRewindUI();

        if (winner == 0)
        {
            DrawReason.text = winReason;
            DrawScreen.SetActive(true);
             AudioController.Instance?.PlayAudio("Draw"); 
        }
        else if (winner == localPlayerId)
        {
            if (PlayerData.Instance != null)
                PlayerData.Instance.AddWin();

            WinReason.text = winReason;

            string timeTaken = FormatTime(totalGameTime);
            string piecesCaptured = (localPlayerId == 1 ? player1Caps : player2Caps).ToString();
            WinGameStats.text = $"Time taken: {timeTaken}\nPieces captured: {piecesCaptured}";

            WinScreen.SetActive(true);
            AudioController.Instance?.PlayAudio("Win");
        }
        else
        {
            if (PlayerData.Instance != null)
                PlayerData.Instance.AddLoss();

            LossReason.text = lossReason;

            // Format the stats string - same time for both, just different captures
            string timeTaken = FormatTime(totalGameTime);
            string piecesCaptured = (localPlayerId == 1 ? player1Caps : player2Caps).ToString();
            LossGameStats.text = $"Time taken: {timeTaken}\nPieces captured: {piecesCaptured}";

            LossScreen.SetActive(true);
            AudioController.Instance?.PlayAudio("Lose");
        }
    }
    void UpdatePiecesToPlaceUI()
    {
        int player1Placed = (PlacementCounter.Value + 1) / 2;
        int player2Placed = PlacementCounter.Value / 2;
        int player1Left = Mathf.Max(0, piecesPerPlayer - player1Placed);
        int player2Left = Mathf.Max(0, piecesPerPlayer - player2Placed);

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
        // Add null check for allSlots
        if (allSlots == null || allSlots.Length == 0)
        {
            Debug.LogError("allSlots array is not assigned or empty! Please assign all slots in the Inspector.");
            return;
        }

        var states = ParseSlotStates();
        foreach (var slot in allSlots)
        {
            if (slot == null) continue;

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
        Debug.Log($"=== CHECK MILL CALLED ===");
        Debug.Log($"Slot: {slotNumber}, Player: {player}");
        Debug.Log($"Mills count: {mills?.Count ?? 0}");

        if (mills == null || mills.Count == 0)
        {
            Debug.LogError($"Mills is null or empty! mills null? {mills == null}, Count: {mills?.Count ?? 0}");
            return false;
        }

        foreach (var mill in mills)
        {
            if (mill == null)
            {
                Debug.LogWarning("Mill is null!");
                continue;
            }

            Debug.Log($"Checking mill: [{string.Join(", ", mill)}]");

            if (!mill.Contains(slotNumber)) continue;

            Debug.Log($"Slot {slotNumber} found in mill [{string.Join(", ", mill)}]");

            int count = 0;
            foreach (int s in mill)
            {
                int owner = GetSlotOwner(s);
                Debug.Log($"  Slot {s} owner: {owner}");
                if (owner == player) count++;
            }

            Debug.Log($"Count: {count}/{mill.Length}");

            if (count == mill.Length)
            {
                Debug.Log($" MILL FORMED! Player {player} at slot {slotNumber}");
                foreach (int s in mill)
                {
                    var slot = GetSlotByNumber(s);
                    if (slot != null) slot.SetMillStatus(true);
                }
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
        if (allSlots == null) return;

        foreach (var slot in allSlots)
        {
            if (slot != null) slot.SetMillStatus(false);
        }

        foreach (var mill in mills)
        {
            int owner = GetSlotOwner(mill[0]);
            if (owner != 0)
            {
                bool isMill = true;
                foreach (int s in mill)
                {
                    if (GetSlotOwner(s) != owner)
                    {
                        isMill = false;
                        break;
                    }
                }
                if (isMill)
                {
                    foreach (int s in mill)
                    {
                        var slot = GetSlotByNumber(s);
                        if (slot != null)
                        {
                            slot.SetMillStatus(true);
                            if (slot.slotUI != null)
                                slot.slotUI.HighlightMill(owner);
                        }
                    }
                }
            }
        }
    }

    bool OpponentHasFreePiece(int opponent)
    {
        if (allSlots == null) return false;

        foreach (var slot in allSlots)
        {
            if (slot != null && GetSlotOwner(slot.slotNumber) == opponent && !slot.isInMill)
                return true;
        }
        return false;
    }

    bool HasWon()
    {
        int opponent = (CurrentPlayer.Value == 1) ? 2 : 1;
        int opponentPieces = (opponent == 1) ? Player1PiecesOnBoard.Value : Player2PiecesOnBoard.Value;

        if (PlacementCounter.Value < totalPlacements) return false;

        if (opponentPieces <= 2)
        {
            WinReason.text = "Your opponent has 2 or less pieces left!";
            LossReason.text = "You have 2 or less pieces left!";
            return true;
        }

        bool canFly = opponentPieces <= 3;

        if (allSlots != null)
        {
            foreach (var slot in allSlots)
            {
                if (slot == null) continue;

                if (GetSlotOwner(slot.slotNumber) != opponent) continue;

                if (canFly)
                {
                    if (allSlots.Any(s => s != null && GetSlotOwner(s.slotNumber) == 0))
                        return false;
                }
                else
                {
                    if (adjacency.ContainsKey(slot.slotNumber))
                    {
                        foreach (int adj in adjacency[slot.slotNumber])
                            if (GetSlotOwner(adj) == 0) return false;
                    }
                }
            }
        }

        WinReason.text = "Your opponent has no more valid moves!";
        LossReason.text = "You have no more valid moves!";
        return true;
    }

    public void OnForfeit()
    {
        if (!IsSpawned || GameEnded.Value)
            return;

        // If game is paused, resume it first
        if (IsGamePaused.Value && IsServer)
        {
            IsGamePaused.Value = false;
            RequestResume();
            PauseRequesterClientId.Value = 0;
            Time.timeScale = 1f;
        }

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

        if (open)
        {
            RequestPause();
        }
        else
        {
            RequestResume();
        }
    }

    public void onGoToLobby()
    {
        StartCoroutine(ReturnToLobbyRoutine());
    }

    System.Collections.IEnumerator ReturnToLobbyRoutine()
    {
        UIManager.Instance.NotifyReturnedToLobby();
        if  (gameType == "Morabaraba")
        {
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("GameScene");
        }
        else
        {
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("SixMensMorris");

        }


        UIManager.Instance.NotifyReturnedToLobby();

    }

    [ServerRpc(RequireOwnership = false)]
    void NotifyReturnedToLobbyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        UIManager.Instance.PlayerReturnedToLobby(clientId);
    }

    void OnCurrentPlayerChanged(int oldVal, int newVal)
    {
        UpdateTurnIndicator();
        UpdateRewindUI();
    }
    void OnPhaseChanged(GamePhase oldVal, GamePhase newVal)
    {
        UpdateTurnIndicator();
        UpdatePiecesToPlaceUI(); 
        UpdateRewindUI();
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

    

    //Rewind Functions
    void SaveSnapshot()
    {
        GameSnapshot snapshot = new GameSnapshot()
        {
            currentPlayer = CurrentPlayer.Value,
            placementCounter = PlacementCounter.Value,

            player1Pieces = Player1PiecesOnBoard.Value,
            player2Pieces = Player2PiecesOnBoard.Value,

            player1Captures = Player1CapturesCount.Value,
            player2Captures = Player2CapturesCount.Value,

            phase = CurrentPhase.Value,

            selectedSlot = SelectedSlot.Value,

            slotStates = SlotStates.Value.ToString()
        };

        gameHistory.Add(snapshot);

        Debug.Log($"Snapshot saved. Count: {gameHistory.Count}");
    }

    void LoadSnapshot(GameSnapshot snapshot)
    {
        CurrentPlayer.Value = snapshot.currentPlayer;

        PlacementCounter.Value = snapshot.placementCounter;

        Player1PiecesOnBoard.Value = snapshot.player1Pieces;
        Player2PiecesOnBoard.Value = snapshot.player2Pieces;

        Player1CapturesCount.Value = snapshot.player1Captures;
        Player2CapturesCount.Value = snapshot.player2Captures;

        CurrentPhase.Value = snapshot.phase;

        SelectedSlot.Value = snapshot.selectedSlot;

        SlotStates.Value = snapshot.slotStates;

        ApplySlotStatesToVisuals();

        UpdatePiecesToPlaceUI();

        UpdateTurnIndicator();

        Debug.Log("Snapshot restored.");
    }

    public void OnRewindPressed()
    {
        if (!IsLocalPlayerTurn())
        {
            Debug.Log("Not your turn.");
            return;
        }

        int rewindsLeft = localPlayerId == 1? Player1Rewinds.Value: Player2Rewinds.Value;

        if (rewindsLeft <= 0)
        {
            rewindButton.interactable = false;
            return;
        }

        RequestRewindServerRpc();
    }

    void UpdateRewindUI()
    {
        int rewindsLeft = localPlayerId == 1? Player1Rewinds.Value: Player2Rewinds.Value;

        RewindText.text = $"Rewinds Left: {rewindsLeft}";

        rewindButton.interactable = IsLocalPlayerTurn() && rewindsLeft > 0 && !GameEnded.Value;
    }

    void OnRewindChanged(int oldVal, int newVal)
    {
        UpdateRewindUI();
    }
    [ServerRpc(RequireOwnership = false)]
    void RequestRewindServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        int requestingPlayer = senderClientId == 0 ? 1 : 2;

        // Only current player can rewind
        if (CurrentPlayer.Value != requestingPlayer)
        {
            Debug.Log("Not this player's turn.");
            return;
        }

        // Check rewind counts
        if (requestingPlayer == 1)
        {
            if (Player1Rewinds.Value <= 0)
            {
                Debug.Log("Player 1 has no rewinds left.");
                return;
            }
            Player1Rewinds.Value--;
        }
        else
        {
            if (Player2Rewinds.Value <= 0)
            {
                Debug.Log("Player 2 has no rewinds left.");
                return;
            }
            Player2Rewinds.Value--;
        }

        // Need at least 2 snapshots
        if (gameHistory.Count < 2)
        {
            Debug.Log("Not enough history to rewind.");
            return;
        }

        // Remove newest state
        gameHistory.RemoveAt(gameHistory.Count - 1);

        // Restore previous state
        GameSnapshot snapshot = gameHistory[gameHistory.Count - 1];

        // Remove restored snapshot
        gameHistory.RemoveAt(gameHistory.Count - 1);

        LoadSnapshot(snapshot);

        Debug.Log($"Player {requestingPlayer} used rewind.");

        // Play sound for all players
        PlaySoundClientRpc("Rewind");

        // Show rewind effect for all players
        ShowRewindEffectClientRpc();
    }

    [ClientRpc]
    public void ShowRewindEffectClientRpc()
    {
        if (rewindEffectPanel != null)
        {
            // Stop any ongoing coroutine on this panel
            StopCoroutine("FadeOutRewindPanel");
            StartCoroutine(FadeOutRewindPanel());
        }
    }


    private System.Collections.IEnumerator FadeOutRewindPanel()
    {
        // Activate the panel
        rewindEffectPanel.SetActive(true);

        // Set initial alpha (if using CanvasGroup)
        CanvasGroup canvasGroup = rewindEffectPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            // If no CanvasGroup, add one
            canvasGroup = rewindEffectPanel.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;


        // Wait for 1 second
        yield return new WaitForSecondsRealtime(1f);

        // Fade out over 0.5 seconds
        float fadeDuration = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            yield return null;
        }

        // Deactivate the panel
        rewindEffectPanel.SetActive(false);
        canvasGroup.alpha = 1f; // Reset alpha for next time
    }

    //Timer Functions

    void SetupTimer(string gameTimeSetting)
    {
        timerMode = TimerMode.None;
        timerRunning = true;

        // Check for turn timer modes first
        if (gameTimeSetting == "5s" || gameTimeSetting == "15s" || gameTimeSetting == "30s")
        {
            timerMode = TimerMode.TurnTimer;

            switch (gameTimeSetting)
            {
                case "5s": turnTimeRemaining = 5f; break;
                case "15s": turnTimeRemaining = 15f; break;
                case "30s": turnTimeRemaining = 30f; break;
                default: turnTimeRemaining = 30f; break;
            }
            Debug.Log($"Turn timer mode enabled: {turnTimeRemaining} seconds per turn");
        }
        // Check for game timer modes
        else if (gameTimeSetting == "5:00" || gameTimeSetting == "10:00" || gameTimeSetting == "15:00")
        {
            timerMode = TimerMode.GameTimer;

            switch (gameTimeSetting)
            {
                case "5:00": gameTimeRemaining = 300f; break;
                case "10:00": gameTimeRemaining = 600f; break;
                case "15:00": gameTimeRemaining = 900f; break;
                default: gameTimeRemaining = 600f; break;
            }
            Debug.Log($"Game timer mode enabled: {gameTimeRemaining} seconds total");
        }
        else
        {
            // No timer
            timerMode = TimerMode.None;
            timerRunning = false;
            Debug.Log("No timer mode - casual play");
        }

        // Initial UI update
        UpdateTimerClientRpc(gameTimeRemaining, turnTimeRemaining, timerMode);
    }

    [ClientRpc]
    void UpdateTimerClientRpc(float gameTime, float turnTime, TimerMode mode)
    {
        timerMode = mode;
        gameTimeRemaining = gameTime;
        turnTimeRemaining = turnTime;
        UpdateTimerUI();
    }

    void UpdateTimerUI()
    {
        if (TimerUI == null) return;

        if (timerMode == TimerMode.GameTimer)
        {
            TimerUI.text = $"Time Left: {FormatTime(gameTimeRemaining)}";
            TimerUI.color = gameTimeRemaining < 60f ? Color.red : Color.white;
        }
        else if (timerMode == TimerMode.TurnTimer)
        {
            TimerUI.text = $"Turn Time: {FormatTime(turnTimeRemaining)}";
            TimerUI.color = turnTimeRemaining < 10f ? Color.red : Color.white;
        }
        else
        {
            TimerUI.text = "Casual Mode";
            TimerUI.color = Color.white;
        }
    }


    string FormatTime(float t)
    {
        if (t < 0) t = 0;
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    void ResetTurnTimer()
    {
        if (timerMode != TimerMode.TurnTimer) return;

        switch (GameSettings.GameTime)
        {
            case "5s": turnTimeRemaining = 5f; break;
            case "15s": turnTimeRemaining = 15f; break;
            case "30s": turnTimeRemaining = 30f; break;
            default: turnTimeRemaining = 30f; break;
        }

        // Send update to clients
        UpdateTimerClientRpc(gameTimeRemaining, turnTimeRemaining, timerMode);
    }

    void EndGameByTime(int loserPlayerId)
    {
        if (GameEnded.Value) return;

        timerRunning = false;
        GameEnded.Value = true;

        int winner = (loserPlayerId == 1) ? 2 : 1;

        if (loserPlayerId == 0)
        {
            // Draw by timeout
            ServerGameOver(0, "Game ended in a draw - Time's up!", "Game ended in a draw - Time's up!");
        }
        else
        {
            ServerGameOver(winner, "Opponent ran out of time!", "You ran out of time!");
        }
    }

    public void PauseGame(bool pause)
    {
        timerRunning = !pause;

        if (pause)
        {
            // Show pause panel (you need to add a reference to a pause panel UI)
            if (pausePanel != null)
                pausePanel.SetActive(true);

            // Disable user input on clicking slots
            SetButtonsInteractable(false);

            // Disable rewind button
            if (rewindButton != null)
                rewindButton.interactable = false;

            Time.timeScale = 0f;
        }
        else
        {
            // Hide pause panel
            if (pausePanel != null)
                pausePanel.SetActive(false);

            // Re-enable input based on current game state
            SetButtonsInteractable(true);

            // Re-enable rewind button if conditions are met
            if (rewindButton != null && IsLocalPlayerTurn() && !GameEnded.Value)
            {
                int rewindsLeft = localPlayerId == 1 ? Player1Rewinds.Value : Player2Rewinds.Value;
                rewindButton.interactable = rewindsLeft > 0;
            }

            Time.timeScale = 1f;
        }
    }
    private float pauseStartTime = 0f;
    private const float MAX_PAUSE_DURATION = 300f; // 5 minutes max pause
    void OnPauseStateChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            pauseStartTime = Time.time;
            isLocallyPaused = true;
            timerRunning = false;

            // Show pause UI
            if (pausePanel != null)
                pausePanel.SetActive(true);

            // Show who paused
            /*if (pauseStatusText != null && PauseRequesterClientId.Value != 0)
            {
                int pausingPlayer = (PauseRequesterClientId.Value == 0) ? 1 : 2;
                pauseStatusText.text = $"Game Paused by Player {pausingPlayer}\nPress Resume to continue";
            }*/

            // Disable game board but keep UI buttons
            SetGameBoardInteractable(false);

            // Freeze time for everyone
            Time.timeScale = 0f;
        }
        else
        {
            pauseStartTime = 0f;
            isLocallyPaused = false;
            timerRunning = true;

            // Hide pause UI
            if (pausePanel != null)
                pausePanel.SetActive(false);

            // Re-enable game board based on game state
            SetGameBoardInteractable(true);

            // Resume time
            Time.timeScale = 1f;
        }
    }

    public void RequestPause()
    {
        if (!IsSpawned || GameEnded.Value)
            return;

        // Check cooldown
        if (Time.time - lastPauseRequestTime < PAUSE_COOLDOWN)
        {
            Debug.Log("Pause on cooldown");
            return;
        }

        lastPauseRequestTime = Time.time;

        // Send pause request to server
        RequestPauseServerRpc();
    }

    public void RequestResume()
    {
        if (!IsSpawned || GameEnded.Value)
            return;

        // Only the player who paused OR the server can resume
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (IsServer || PauseRequesterClientId.Value == localClientId)
        {
            RequestResumeServerRpc();
        }
        else
        {
            Debug.Log("Only the player who paused can resume the game");
            /*if (pauseStatusText != null)
                pauseStatusText.text = $"Waiting for Player {(PauseRequesterClientId.Value == 0 ? 1 : 2)} to resume...";
       */
            }
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestPauseServerRpc(ServerRpcParams rpcParams = default)
    {
        if (IsGamePaused.Value)
        {
            Debug.Log("Game is already paused");
            return;
        }

        ulong requesterId = rpcParams.Receive.SenderClientId;

        // Optional: Add vote-based pause system
        // For now, allow any player to pause

        Debug.Log($"Player {requesterId} requested pause");

        IsGamePaused.Value = true;
        PauseRequesterClientId.Value = requesterId;

        // Notify all clients about pause
        PauseStateChangedClientRpc(true, requesterId);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestResumeServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsGamePaused.Value)
        {
            Debug.Log("Game is not paused");
            return;
        }

        ulong requesterId = rpcParams.Receive.SenderClientId;
        ulong pauseRequesterId = PauseRequesterClientId.Value;

        // Allow server or the pausing player to resume
        if (IsServer || requesterId == pauseRequesterId)
        {
            Debug.Log($"Resuming game. Requested by: {requesterId}");

            IsGamePaused.Value = false;
            PauseRequesterClientId.Value = 0;

            // Notify all clients about resume
            PauseStateChangedClientRpc(false, 0);
        }
        else
        {
            Debug.Log($"Player {requesterId} cannot resume - only player {pauseRequesterId} can");
        }
    }

    [ClientRpc]
    void PauseStateChangedClientRpc(bool isPaused, ulong requesterId)
    {
        if (isPaused)
        {
            isLocallyPaused = true;
            timerRunning = false;

            if (pausePanel != null)
                pausePanel.SetActive(true);

            SetGameBoardInteractable(false);
            Time.timeScale = 0f;
        }
        else
        {
            isLocallyPaused = false;
            timerRunning = true;

            if (pausePanel != null)
                pausePanel.SetActive(false);

            SetGameBoardInteractable(true);
            Time.timeScale = 1f;
        }
    }

    // Helper methods for controlling interactability
    void SetGameBoardInteractable(bool enabled)
    {
        // Disable/enable game slots
        foreach (var slot in allSlots)
        {
            if (slot != null)
            {
                var btn = slot.GetComponent<UnityEngine.UI.Button>();
                if (btn != null)
                    btn.interactable = enabled && !IsGamePaused.Value;
            }
        }

        // Handle rewind button
        if (rewindButton != null)
        {
            if (IsGamePaused.Value)
                rewindButton.interactable = false;
            else
                rewindButton.interactable = enabled && IsLocalPlayerTurn() && !GameEnded.Value;
        }
    }


    //Helper Functions
    SlotID GetSlotByNumber(int number) => allSlots.FirstOrDefault(s => s.slotNumber == number);
    bool IsAdjacent(SlotID from, SlotID to) => adjacency[from.slotNumber].Contains(to.slotNumber);

    void HardcodeGameData()
    {
        string gameType = GameSettings.GameType;

        if (gameType == "Morabaraba")
        {
            totalSlots = 24;
            piecesPerPlayer = 12;
            totalPlacements = 24; // 12 + 12 = 24 placements total

            adjacency = new Dictionary<int, int[]>
        {
            {1, new int[] {2, 8, 9}}, {2, new int[] {1, 3, 10}}, {3, new int[] {2, 4, 11}},
            {4, new int[] {3, 5, 12}}, {5, new int[] {4, 6, 13}}, {6, new int[] {5, 7, 14}},
            {7, new int[] {6, 8, 15}}, {8, new int[] {7, 1, 16}}, {9, new int[] {1, 10, 16, 17}},
            {10, new int[] {2, 9, 11, 18}}, {11, new int[] {3, 10, 12, 19}}, {12, new int[] {4, 11, 13, 20}},
            {13, new int[] {5, 12, 14, 21}}, {14, new int[] {6, 13, 15, 22}}, {15, new int[] {7, 14, 16, 23}},
            {16, new int[] {8, 9, 15, 24}}, {17, new int[] {9, 18, 24}}, {18, new int[] {10, 17, 19}},
            {19, new int[] {11, 18, 20}}, {20, new int[] {12, 19, 21}}, {21, new int[] {13, 20, 22}},
            {22, new int[] {14, 21, 23}}, {23, new int[] {15, 22, 24}}, {24, new int[] {16, 17, 23}}
        };

            mills = new List<int[]>
        {
            new int[] {1,2,3}, new int[] {3,4,5}, new int[] {5,6,7}, new int[] {7,8,1},
            new int[] {9,10,11}, new int[] {11,12,13}, new int[] {13,14,15}, new int[] {15,16,9},
            new int[] {17,18,19}, new int[] {19,20,21}, new int[] {21,22,23}, new int[] {23,24,17},
            new int[] {1,9,17}, new int[] {2,10,18}, new int[] {3,11,19}, new int[] {4,12,20},
            new int[] {5,13,21}, new int[] {6,14,22}, new int[] {7,15,23}, new int[] {8,16,24}
        };
        }
        else if (gameType == "6 Men's Morris")
        {
            totalSlots = 16;
            piecesPerPlayer = 6;
            totalPlacements = 12; // 6 + 6 = 12 placements total

            adjacency = new Dictionary<int, int[]>
        {
            {1, new int[] {2, 8, 9}}, {2, new int[] {1, 3, 10}}, {3, new int[] {2, 4, 11}},
            {4, new int[] {3, 5, 12}}, {5, new int[] {4, 6, 13}}, {6, new int[] {5, 7, 14}},
            {7, new int[] {6, 8, 15}}, {8, new int[] {7, 1, 16}}, {9, new int[] {1, 10, 16}},
            {10, new int[] {2, 9, 11}}, {11, new int[] {3, 10, 12}}, {12, new int[] {4, 11, 13}},
            {13, new int[] {5, 12, 14}}, {14, new int[] {6, 13, 15}}, {15, new int[] {7, 14, 16}},
            {16, new int[] {8, 15, 9}}
        };

            mills = new List<int[]>
        {
            new int[] {1,2,3}, new int[] {3,4,5}, new int[] {5,6,7}, new int[] {7,8,1},
            new int[] {9,10,11}, new int[] {11,12,13}, new int[] {13,14,15}, new int[] {15,16,9},
            new int[] {1,9,16}, new int[] {2,10,15}, new int[] {3,11,14}, new int[] {4,12,13},
            new int[] {5,13,12}, new int[] {6,14,11}, new int[] {7,15,10}, new int[] {8,16,9}
        };
        }

        Debug.Log($"Game data loaded: {totalSlots} slots, {piecesPerPlayer} pieces per player, {totalPlacements} total placements, {mills.Count} mills");
    }
    void GameOver(int winner) => ServerGameOver(CurrentPlayer.Value,WinReason.text,LossReason.text);
}

public enum GamePhase
{
    Placing,
    Moving,
    Capturing,
    End
}

