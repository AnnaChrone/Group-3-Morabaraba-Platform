using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameController : NetworkBehaviour
{
    // ===== NETWORK VARIABLES (Auto-synced to all clients) =====

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
        12,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Player2PiecesOnBoard = new NetworkVariable<int>(
        12,
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

    // Track slot ownership: slotNumber -> playerID (0 = empty)
    public NetworkVariable<FixedString4096Bytes> SlotStates = new NetworkVariable<FixedString4096Bytes>("");

    [Header("Loading UI")]
    public GameObject loadingPanel;
    public TextMeshProUGUI loadingText;

    private bool networkReady = false;

    [Header("UI References")]
    public TextMeshProUGUI CurrentTurnIndicator;
    public SlotID[] allSlots;

    // Local selection (NOT synced - client-side only)
    private SlotID selectedSlot = null;

    // Adjacency and mills data
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

    // ✅ MERGED Update() method - only one now
    void Update()
    {
        if (!IsSpawned || !networkReady) return;

        // Highlight selected slot during moving phase
        if (CurrentPhase.Value == GamePhase.Moving && SelectedSlot.Value != 0)
        {
            var slot = GetSlotByNumber(SelectedSlot.Value);
            if (slot != null)
            {
                // ✅ Fix: Use visual feedback through SlotID's existing methods
                // You may need to add a method like SetSelected(bool) to SlotID
                slot.GetComponent<UnityEngine.UI.Button>().interactable = true;
            }
        }

        // Optional debug logging at frame 120
        if (Time.frameCount == 120)
        {
            Debug.Log($"[GC] Frame 120 | IsSpawned: {IsSpawned} | networkReady: {networkReady}");
        }
    }

    // ===== INITIALIZATION =====

    void OnEnable() => SetButtonsInteractable(false);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"🎮 OnNetworkSpawn called | IsServer: {IsServer} | ClientId: {NetworkManager.Singleton?.LocalClientId}");
        if (loadingText != null)
            loadingText.text = "Syncing Game State...";

        // Register callbacks
        CurrentPlayer.OnValueChanged += OnCurrentPlayerChanged;
        PlacementCounter.OnValueChanged += OnPlacementCounterChanged;
        CurrentPhase.OnValueChanged += OnPhaseChanged;
        SlotStates.OnValueChanged += OnSlotStatesChanged;

        if (IsServer)
        {
            InitializeGameState();
            Debug.Log("🎮 Game state initialized");
        }

        ApplySlotStatesToVisuals();
        UpdateTurnIndicator();

        StartCoroutine(FinishLoading());
    }

    System.Collections.IEnumerator FinishLoading()
    {
        yield return null;
        yield return null;

        networkReady = true;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
            Debug.Log("✅ Loading complete - Game ready!");
        }

        SetButtonsInteractable(true);
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
        base.OnNetworkDespawn();
    }

    void Start()
    {
        Debug.Log($"📍 GameController Start() | IsListening: {NetworkManager.Singleton?.IsListening}");
        Debug.Log($"GameController IsSpawned: {IsSpawned}");
        Debug.Log($"NetworkManager active: {NetworkManager.Singleton != null}");
        Debug.Log($"IsListening: {NetworkManager.Singleton?.IsListening}");
    }

    void InitializeGameState()
    {
        CurrentPlayer.Value = 1;
        PlacementCounter.Value = 0;
        Player1PiecesOnBoard.Value = 12;
        Player2PiecesOnBoard.Value = 12;
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

    // ===== INPUT HANDLING =====

    public void OnSlotClicked(SlotID slot)
    {
        Debug.Log($"🎯 [CLIENT] OnSlotClicked called for Slot {slot.slotNumber}");

        if (!networkReady)
        {
            Debug.LogWarning("⏳ Game still loading...");
            return;
        }

        if (!IsSpawned)
        {
            Debug.LogError("❌ GameController not spawned!");
            return;
        }

        if (GameEnded.Value)
        {
            Debug.LogWarning("🏁 Game has ended!");
            return;
        }

        bool isMyTurn = IsLocalPlayerTurn();
        Debug.Log($"🎯 [CLIENT] IsLocalPlayerTurn()={isMyTurn}");
        Debug.Log($"🎯 [CLIENT] CurrentPlayer.Value={CurrentPlayer.Value}");
        Debug.Log($"🎯 [CLIENT] CurrentPhase.Value={CurrentPhase.Value}");

        if (!isMyTurn)
        {
            Debug.LogWarning("⛔ Not your turn!");
            return;
        }

        // ✅ Add this log RIGHT before the RPC call
        Debug.Log($"🚀 [CLIENT] ABOUT TO CALL RPC - Slot={slot.slotNumber}, Phase={CurrentPhase.Value}");
        Debug.Log($"🚀 [CLIENT] IsServer={IsServer}, IsClient={IsClient}");

        RequestMoveServerRpc(slot.slotNumber, CurrentPhase.Value);

        // ✅ Add this log RIGHT after the RPC call
        Debug.Log($"✅ [CLIENT] RPC CALLED SUCCESSFULLY");
    }

    bool IsLocalPlayerTurn()
    {
        if (!NetworkManager.Singleton) return false;
        int localPlayerId = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
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

    // ===== SERVER-SIDE GAME LOGIC =====

    void ServerHandlePlacing(SlotID slot)
    {
        if (GetSlotOwner(slot.slotNumber) != 0) return;

        SetSlotOwner(slot.slotNumber, CurrentPlayer.Value);
        PlacementCounter.Value++;

        if (CheckMill(slot.slotNumber, CurrentPlayer.Value))
        {
            CurrentPhase.Value = GamePhase.Capturing;
            return;
        }

        if (PlacementCounter.Value >= 24)
        {
            CurrentPhase.Value = GamePhase.Moving;
        }

        EndTurn();
    }

    // ✅ Fixed: Changed 'toSlot' to 'slot' (the parameter name)
    void ServerHandleMoving(SlotID slot)
    {
        int currentPlayer = CurrentPlayer.Value;

        if (SelectedSlot.Value == 0)
        {
            if (GetSlotOwner(slot.slotNumber) != currentPlayer)
                return;

            SelectedSlot.Value = slot.slotNumber;
            return;
        }

        SlotID fromSlot = GetSlotByNumber(SelectedSlot.Value);
        if (!IsValidMove(fromSlot, slot, currentPlayer))
        {
            SelectedSlot.Value = 0;
            return;
        }

        SetSlotOwner(SelectedSlot.Value, 0);
        SetSlotOwner(slot.slotNumber, currentPlayer);

        SelectedSlot.Value = 0;

        if (CheckMill(slot.slotNumber, currentPlayer))
        {
            CurrentPhase.Value = GamePhase.Capturing;
            return;
        }

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
            Player2PiecesOnBoard.Value--;
        else
            Player1PiecesOnBoard.Value--;

        if (HasWon())
        {
            ServerGameOver(CurrentPlayer.Value);
            return;
        }

        CurrentPhase.Value = (PlacementCounter.Value >= 24) ? GamePhase.Moving : GamePhase.Placing;
        EndTurn();
    }

    void EndTurn()
    {
        CurrentPlayer.Value = (CurrentPlayer.Value == 1) ? 2 : 1;
    }

    void ServerGameOver(int winner)
    {
        GameEnded.Value = true;
        GameOverClientRpc(winner);
    }

    [ClientRpc]
    void GameOverClientRpc(int winner)
    {
        Debug.Log($"🏆 GAME OVER: Player {winner} wins!");
    }

    // ===== SLOT STATE MANAGEMENT =====

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

    // ===== MILL & WIN LOGIC =====

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
                return true;
            }
        }
        return false;
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
        if (opponentPieces <= 2) return true;

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
        return true;
    }

    // ===== UI UPDATES =====

    void OnCurrentPlayerChanged(int oldVal, int newVal) => UpdateTurnIndicator();
    void OnPhaseChanged(GamePhase oldVal, GamePhase newVal) => UpdateTurnIndicator();

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

    void OnPlacementCounterChanged(int oldVal, int newVal) => UpdateTurnIndicator();

    // ===== HELPERS =====

    SlotID GetSlotByNumber(int number) => allSlots.FirstOrDefault(s => s.slotNumber == number);

    bool IsAdjacent(SlotID from, SlotID to) => adjacency[from.slotNumber].Contains(to.slotNumber);

    void GameOver(int winner) => ServerGameOver(winner);
}

public enum GamePhase
{
    Placing,
    Moving,
    Capturing,
    End
}