using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject hostingPanel;
    public GameObject joiningPanel;

    [Header("Hosting Panel References")]
    public TextMeshProUGUI hostLobbyCodeText;
    public TMP_Dropdown hostGameTypeDropdown;
    public TMP_Dropdown hostTimeDropdown;
    public Transform hostPlayerListContainer;
    public PlayerSlot hostPlayerSlotPrefab;
    public Button hostStartButton;

    [Header("Joining Panel References")]
    public TMP_InputField joinLobbyCodeInput;
    public Button joinButton;
    public TextMeshProUGUI joinButtonText;
    public TextMeshProUGUI joinGameTypeText;
    public TextMeshProUGUI joinTimeText;
    public Transform joinPlayerListContainer;
    public PlayerSlot joinPlayerSlotPrefab;

    [Header("Network References")]
    public GameNetwork gameNetwork;
    public TextMeshProUGUI connectionStatusText;
    public GameObject loadingOverlay;

    [Header("UI Feedback")]
    public TextMeshProUGUI errorMessageText;
    public GameObject errorPopup;

    // Lobby Data
    private string lobbyCode;
    private string gameType = "12 Men's Morris";
    private string gameTime = "10:00";
    private List<string> playersInLobby = new List<string>();
    private Dictionary<int, bool> playerReadyStates = new Dictionary<int, bool>();
    private bool isHost = false;
    private bool isNetworkOperationInProgress = false;
    private bool hasJoinedLobby = false;
    private bool isLocalPlayerReady = false;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ShowMainMenu();

        // Setup join input listeners
        if (joinLobbyCodeInput != null)
        {
            joinLobbyCodeInput.onEndEdit.AddListener(OnLobbyCodeSubmitted);
            joinLobbyCodeInput.onValueChanged.AddListener(UpdateJoinButtonState);
            UpdateJoinButtonState("");
        }

        // Setup hosting dropdowns
        SetupHostingDropdowns();

        // Hide error popup by default
        if (errorPopup != null) errorPopup.SetActive(false);

        // Subscribe to network ready events
        if (gameNetwork != null)
        {
            gameNetwork.OnPlayerReadyChanged += OnPlayerReadyChanged;
            gameNetwork.OnGameTypeChanged += OnNetworkGameTypeChanged;
            gameNetwork.OnGameTimeChanged += OnNetworkGameTimeChanged;

            // Initialize UI with current network values (in case we joined late)
            if (isHost || hasJoinedLobby)
            {
                UpdateLobbySettingsUI(gameNetwork.GetCurrentGameType(), gameNetwork.GetCurrentGameTime());
            }
        }
    }

    private void OnDestroy()
    {
        if (gameNetwork != null)
        {
            gameNetwork.OnGameTypeChanged -= OnNetworkGameTypeChanged;
            gameNetwork.OnGameTimeChanged -= OnNetworkGameTimeChanged;
            gameNetwork.OnPlayerReadyChanged -= OnPlayerReadyChanged;
        }
    }

    #region Panel Switching

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(false);
        ClearLobbyData();
        HideError();
    }

    public void ShowHostingPanel()
    {
        mainMenuPanel.SetActive(false);
        hostingPanel.SetActive(true);
        joiningPanel.SetActive(false);
        isHost = true;
        hasJoinedLobby = false;
        HideError();

        if (hostLobbyCodeText != null)
            hostLobbyCodeText.text = "------";

        playersInLobby.Clear();
        playersInLobby.Add(GetPlayerUsername());
        playerReadyStates.Clear();
        playerReadyStates[1] = true; // Host is always ready implicitly
        UpdateHostingPlayerList();
        UpdateStartButtonInteractable();
    }

    public void ShowJoiningPanel()
    {
        mainMenuPanel.SetActive(false);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(true);
        isHost = false;
        HideError();

        if (joinButton != null)
        {
            // Enable button only if input is valid
            string input = joinLobbyCodeInput?.text?.Trim() ?? "";
            joinButton.interactable = input.Length >= 6;
        }

        // Reset ready state
        isLocalPlayerReady = false;
        UpdateJoiningPlayerList();
    }

    #endregion

    #region Network Integration (Host/Join)

    public async void OnHostButtonClicked()
    {
        if (isNetworkOperationInProgress) return;
        if (gameNetwork == null)
        {
            ShowError("Network system not initialized!");
            return;
        }

        isNetworkOperationInProgress = true;
        SetNetworkUIState(false, "Creating lobby...");
        HideError();

        ShowHostingPanel();

        try
        {
            bool success = await gameNetwork.CreateLobbyAsync("Morabaraba");

            if (success && !string.IsNullOrEmpty(gameNetwork.GetLobbyCode()))
            {
                lobbyCode = gameNetwork.GetLobbyCode();
                if (hostLobbyCodeText != null)
                    hostLobbyCodeText.text = lobbyCode;
            }
            else
            {
                ShowError("Failed to create online lobby. Check console for details.");
            }
        }
        catch (System.Exception e)
        {
            ShowError($"Host error: {e.Message}");
        }
        finally
        {
            isNetworkOperationInProgress = false;
            SetNetworkUIState(true, "");
        }
    }

    public async void OnJoinButtonClicked()
    {
        Debug.Log($"[DEBUG] OnJoinButtonClicked - hasJoinedLobby: {hasJoinedLobby}");

        if (isNetworkOperationInProgress)
        {
            Debug.LogWarning("[DEBUG] Network operation in progress, blocking click");
            return;
        }

        // If already joined, this button is now the Ready button
        if (hasJoinedLobby)
        {
            Debug.Log("[DEBUG] Player has joined, toggling ready state");
            ToggleLocalPlayerReady();
            return;
        }

        // --- Join Flow Below ---
        Debug.Log("[DEBUG] Starting join flow");

        string code = joinLobbyCodeInput.text.Trim().ToUpper();
        Debug.Log($"[DEBUG] Lobby code entered: {code}");

        if (code.Length < 6)
        {
            ShowError("Enter a valid 6-character lobby code");
            return;
        }

        if (gameNetwork == null)
        {
            Debug.LogError("[DEBUG] gameNetwork is NULL!");
            ShowError("Network system not initialized!");
            return;
        }

        isNetworkOperationInProgress = true;
        SetNetworkUIState(false, "Joining lobby...");
        HideError();

        // CRITICAL: Clean up any existing lobby membership before rejoining
        Debug.Log("[DEBUG] Cleaning up existing lobby state...");
        await gameNetwork.LeaveLobbyIfExists();

        // Small delay to ensure cleanup completes
        await Task.Delay(500);

        // Disable button visually during join attempt
        if (joinButton != null) joinButton.interactable = false;
        if (joinButtonText != null) joinButtonText.text = "Joining...";

        try
        {
            Debug.Log($"[DEBUG] Calling gameNetwork.JoinLobbyAsync({code})");
            bool success = await gameNetwork.JoinLobbyAsync(code);
            Debug.Log($"[DEBUG] JoinLobbyAsync returned: {success}");

            if (success)
            {
                lobbyCode = code;

                Debug.Log("[DEBUG] Setting hasJoinedLobby = true");
                hasJoinedLobby = true;

                ShowJoiningPanel();

                if (gameNetwork != null)
                {
                    string networkType = gameNetwork.GetCurrentGameType();
                    string networkTime = gameNetwork.GetCurrentGameTime();
                    joinGameTypeText.text = networkType;
                    joinTimeText.text = networkTime;
                    gameType = networkType;
                    gameTime = networkTime;
                }

                playersInLobby.Clear();
                playersInLobby.Add("Host");
                playersInLobby.Add(GetPlayerUsername());

                playerReadyStates.Clear();
                playerReadyStates[1] = false;
                playerReadyStates[2] = false;

                UpdateJoiningPlayerList();
                Debug.Log($"Joined lobby: {lobbyCode}");
            }
            else
            {
                Debug.LogError("[DEBUG] Join failed, resetting button");
                ShowError("Lobby not found or is full");
                ResetJoinButtonToJoinState();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DEBUG] Join exception: {e.Message}");
            ShowError($"Join error: {e.Message}");
            ResetJoinButtonToJoinState();
        }
        finally
        {
            isNetworkOperationInProgress = false;
            if (!hasJoinedLobby)
            {
                SetNetworkUIState(true, "");
            }
        }
    }

    void ResetJoinButtonToJoinState()
    {
        hasJoinedLobby = false;
        isLocalPlayerReady = false;

        if (joinButtonText != null)
            joinButtonText.text = "Join";

        if (joinButton != null)
        {
            string input = joinLobbyCodeInput?.text?.Trim() ?? "";
            joinButton.interactable = input.Length >= 6;
        }

        Debug.Log("[UI] Join button reset to Join state");
    }

    void ToggleLocalPlayerReady()
    {
        if (!hasJoinedLobby || gameNetwork == null ||
        Unity.Netcode.NetworkManager.Singleton?.IsConnectedClient != true)
        {
            ShowError("Not connected to lobby. Please rejoin.");
            ResetJoinButtonToJoinState();
            return;
        }

        isLocalPlayerReady = !isLocalPlayerReady;
        gameNetwork.SetLocalPlayerReady(isLocalPlayerReady);

        // Update UI feedback
        if (joinButtonText != null)
        {
            // Show checkmark when ready
            joinButtonText.text = isLocalPlayerReady ? "Ready ✓" : "Ready";
        }

        UpdateJoiningPlayerList();
        Debug.Log($"Local player ready: {isLocalPlayerReady}");
    }

    void OnPlayerReadyChanged(int playerNumber, bool isReady)
    {
        playerReadyStates[playerNumber] = isReady;

        if (isHost)
        {
            UpdateHostingPlayerList();
            UpdateStartButtonInteractable();
        }
        else
        {
            UpdateJoiningPlayerList();
        }

        Debug.Log($"Player {playerNumber} ready: {isReady}");
    }

    void UpdateStartButtonInteractable()
    {
        if (hostStartButton == null) return;

        // Start button enabled if:
        // 1. At least 2 players in lobby
        // 2. ALL players are ready (host is implicitly ready, joiners must press ready)
        bool hasMinPlayers = playersInLobby.Count >= 2;
        bool allReady = true;

        foreach (var kvp in playerReadyStates)
        {
            if (!kvp.Value)
            {
                allReady = false;
                break;
            }
        }

        hostStartButton.interactable = hasMinPlayers && allReady;

        // Visual feedback
        ColorBlock colors = hostStartButton.colors;
        if (hostStartButton.interactable)
        {
            colors.normalColor = Color.green;
            colors.highlightedColor = new Color(0.5f, 1f, 0.5f);
        }
        else
        {
            colors.normalColor = Color.gray;
            colors.highlightedColor = Color.gray;
        }
        hostStartButton.colors = colors;
    }

    void SetNetworkUIState(bool interactable, string statusMessage)
    {
        if (joinButton != null) joinButton.interactable = interactable;
        if (hostStartButton != null) hostStartButton.interactable = interactable;
        if (connectionStatusText != null) connectionStatusText.text = statusMessage;
        if (loadingOverlay != null) loadingOverlay.SetActive(!interactable);
    }

    string GetPlayerUsername()
    {
        // Priority: PlayerSession > PlayerPrefs > Default
        if (PlaySession.Instance != null && PlaySession.Instance.IsAuthenticated)
        {
            return PlaySession.Instance.Username;
        }

        string saved = PlayerPrefs.GetString("PlayerUsername", "");
        return !string.IsNullOrEmpty(saved) ? saved : "Player";
    }

    #endregion

    #region Hosting Functions
    void SetupHostingDropdowns()
    {
        if (hostGameTypeDropdown != null)
        {
            hostGameTypeDropdown.ClearOptions();
            hostGameTypeDropdown.AddOptions(new List<string>
            {
                "12 Men's Morris",
                "9 Men's Morris",
                "6 Men's Morris"
            });
            hostGameTypeDropdown.onValueChanged.RemoveAllListeners();
            hostGameTypeDropdown.onValueChanged.AddListener(OnHostGameTypeChanged);
        }

        if (hostTimeDropdown != null)
        {
            hostTimeDropdown.ClearOptions();
            hostTimeDropdown.AddOptions(new List<string>
            {
                "5:00",
                "10:00",
                "15:00"
            });
            hostTimeDropdown.onValueChanged.RemoveAllListeners();
            hostTimeDropdown.onValueChanged.AddListener(OnHostTimeChanged);
        }
    }

    void OnHostGameTypeChanged(int index)
    {
        string[] options = { "12 Men's Morris", "9 Men's Morris", "6 Men's Morris" };
        gameType = options[index];

        // SYNC TO NETWORK
        if (gameNetwork != null && isHost)
        {
            gameNetwork.UpdateLobbySettings(gameType, gameTime);
        }
    }

    void OnHostTimeChanged(int index)
    {
        string[] options = { "5:00", "10:00", "15:00" };
        gameTime = options[index];

        // SYNC TO NETWORK
        if (gameNetwork != null && isHost)
        {
            gameNetwork.UpdateLobbySettings(gameType, gameTime);
        }
    }

    void UpdateHostingPlayerList()
    {
        // Clear existing slots
        foreach (Transform child in hostPlayerListContainer)
        {
            Destroy(child.gameObject);
        }

        // Create slots for active players
        for (int i = 0; i < playersInLobby.Count && i < 3; i++)
        {
            PlayerSlot slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            string displayName = string.IsNullOrEmpty(playersInLobby[i]) ? $"Player {i + 1}" : playersInLobby[i];
            slot.Initialize(displayName, i + 1);
        }

        // Fill empty slots with placeholders
        for (int i = playersInLobby.Count; i < 3; i++)
        {
            PlayerSlot slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            slot.Initialize("", i + 1);
            if (slot.TryGetComponent<Image>(out var img))
            {
                img.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }

    public void StartGame()
    {
        if (playersInLobby.Count >= 2 && gameNetwork != null && gameNetwork.AreAllPlayersReady())
        {
            Debug.Log($"Host starting game with {playersInLobby.Count} players...");

            // Notify all clients via network
            if (gameNetwork.IsServer)
            {
                gameNetwork.StartGameServerRpc();
            }

            // Load GameScene additively
            SceneManager.LoadScene("GameScene", LoadSceneMode.Additive);

            Scene gameScene = SceneManager.GetSceneByName("GameScene");
            if (gameScene.IsValid())
                SceneManager.SetActiveScene(gameScene);
        }
        else
        {
            ShowError("Wait for all players to ready up!");
        }
    }

    [ContextMenu("Simulate Player Join (Host View)")]
    public void SimulatePlayerJoin()
    {
        if (playersInLobby.Count < 3)
        {
            int playerNum = playersInLobby.Count + 1;
            playersInLobby.Add($"Player {playerNum}");
            UpdateHostingPlayerList();
        }
    }

    #endregion

    #region Joining Functions

    void InitializeJoining()
    {
        if (joinLobbyCodeInput != null) joinLobbyCodeInput.text = "";
        if (joinGameTypeText != null) joinGameTypeText.text = "(Game Type)";
        if (joinTimeText != null) joinTimeText.text = "(Time)";

        playersInLobby.Clear();
        UpdateJoiningPlayerList();
    }

    void OnLobbyCodeSubmitted(string input)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            TryJoinLobby(input);
        }
    }

    void UpdateJoinButtonState(string input)
    {
        if (joinButton != null && !hasJoinedLobby)
        {
            joinButton.interactable = input.Trim().Length >= 6 && !isNetworkOperationInProgress;
        }
    }

    void TryJoinLobby(string code)
    {
        code = code.Trim().ToUpper();

        if (code.Length < 6)
        {
            ShowError("Lobby code must be 6 characters!");
            return;
        }

        // Visual feedback
        if (joinButton != null)
        {
            joinButton.interactable = false;
            if (joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>() != null)
            {
                joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Joining...";
            }
        }

        // Small delay for visual feedback, then call actual join
        Invoke(nameof(OnJoinButtonClicked), 0.1f);
    }

    void UpdateJoiningPlayerList()
    {
        // Clear existing slots
        foreach (Transform child in joinPlayerListContainer)
        {
            Destroy(child.gameObject);
        }

        // Create slots for active players
        for (int i = 0; i < playersInLobby.Count && i < 3; i++)
        {
            PlayerSlot slot = Instantiate(joinPlayerSlotPrefab, joinPlayerListContainer);
            string displayName = string.IsNullOrEmpty(playersInLobby[i]) ? $"Player {i + 1}" : playersInLobby[i];
            slot.Initialize(displayName, i + 1);
        }

        // Fill empty slots
        for (int i = playersInLobby.Count; i < 3; i++)
        {
            PlayerSlot slot = Instantiate(joinPlayerSlotPrefab, joinPlayerListContainer);
            slot.Initialize("", i + 1);
            if (slot.TryGetComponent<Image>(out var img))
            {
                img.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }

    public void OnJoinReadyClicked()
    {
        if (playersInLobby.Count < 2)
        {
            ShowError("Join a lobby first!");
            return;
        }

        Debug.Log("Player ready!");
        // In production: send ready signal to host via ServerRpc
    }

    #endregion

    #region Utility & Error Handling

    void ClearLobbyData()
    {
        lobbyCode = "";
        gameType = "12 Men's Morris";
        gameTime = "10:00";
        playersInLobby.Clear();
        isHost = false;
    }

    void ShowError(string message)
    {
        Debug.LogWarning($"UI Error: {message}");

        if (errorMessageText != null)
        {
            errorMessageText.text = message;
        }

        if (errorPopup != null)
        {
            errorPopup.SetActive(true);
            // Auto-hide after 3 seconds
            Invoke(nameof(HideError), 3f);
        }
    }

    void HideError()
    {
        if (errorPopup != null)
        {
            errorPopup.SetActive(false);
        }
        CancelInvoke(nameof(HideError));
    }

    public void OnCloseErrorPopup()
    {
        HideError();
    }

    public async void OnBackToMainMenu()
    {
        if (gameNetwork != null && hasJoinedLobby)
        {
            Debug.Log("[UI] Leaving lobby before returning to menu...");
            await gameNetwork.LeaveLobbyIfExists();
            hasJoinedLobby = false;
        }

        // If we're in a networked lobby, disconnect first
        if (gameNetwork != null && Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }

        ClearLobbyData();
        ShowMainMenu();
    }

    void OnNetworkGameTypeChanged(string newType)
    {
        gameType = newType;
        UpdateLobbySettingsUI(gameType, gameTime);
        Debug.Log($"[UI] Game type updated: {newType}");
    }

    void OnNetworkGameTimeChanged(string newTime)
    {
        gameTime = newTime;
        UpdateLobbySettingsUI(gameType, gameTime);
        Debug.Log($"[UI] Game time updated: {newTime}");
    }

    // ✅ Centralized UI update for both panels
    void UpdateLobbySettingsUI(string type, string time)
    {
        if (isHost)
        {
            // Update host dropdowns to match network (in case of late joiner seeing changes)
            if (hostGameTypeDropdown != null)
            {
                int index = Array.IndexOf(new[] { "12 Men's Morris", "9 Men's Morris", "6 Men's Morris" }, type);
                if (index >= 0) hostGameTypeDropdown.value = index;
            }
            if (hostTimeDropdown != null)
            {
                int index = Array.IndexOf(new[] { "5:00", "10:00", "15:00" }, time);
                if (index >= 0) hostTimeDropdown.value = index;
            }
        }
        else if (hasJoinedLobby)
        {
            // Update joining player's display
            if (joinGameTypeText != null) joinGameTypeText.text = type;
            if (joinTimeText != null) joinTimeText.text = time;
        }
    }

    #endregion
}