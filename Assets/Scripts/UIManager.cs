using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode.Components;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject hostingPanel;
    public GameObject joiningPanel;
    public GameObject errorPanel;
    public TextMeshProUGUI errorText;

    [Header("Hosting Panel References")]
    public TextMeshProUGUI hostLobbyCodeText;
    public TMP_Dropdown hostGameTypeDropdown;
    public TMP_Dropdown hostTimeDropdown;
    public Transform hostPlayerListContainer;
    public PlayerSlots hostPlayerSlotPrefab;
    public Button startGameButton;

    [Header("Joining Panel References")]
    public TMP_InputField joinLobbyCodeInput;
    public Button joinButton;
    public TextMeshProUGUI joinGameTypeText;
    public TextMeshProUGUI joinTimeText;
    public Transform joinPlayerListContainer;
    public PlayerSlots joinPlayerSlotPrefab;

    // Lobby Data
    private string lobbyCode;
    private string gameType = "12 Men's Morris";
    private string gameTime = "10:00";
    private List<string> playersInLobby = new List<string>();
    private bool isHost = false;

    private Allocation hostAllocation;
    private string relayJoinCode;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // Initialize Unity Services
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay initialization failed: {e.Message}");
        }

        // 🔥 ADD THIS: Spawn UIManager's NetworkObject
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn();
                Debug.Log("✅ UIManager NetworkObject spawned as Session Owner");
            }
        }

        ShowMainMenu();

        // Setup input listeners
        joinLobbyCodeInput.onEndEdit.AddListener(OnLobbyCodeSubmitted);
        joinLobbyCodeInput.onValueChanged.AddListener(UpdateJoinButtonState);
        UpdateJoinButtonState("");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private new void OnDestroy()
    {
        // Clean up callbacks to avoid memory leaks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(false);
        ClearLobbyData();
    }

    /// <summary>
    /// Leaves current lobby/network session and returns to main menu
    /// </summary>
    public void LeaveLobbyAndReturnToMainMenu()
    {
        // 1. Shutdown NetworkManager if actively listening
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("🛑 NetworkManager shutdown");
        }

        // 2. Reset UnityTransport to clear any Relay server data
        var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", 7777); // Reset to default local values
        }

        // 3. Clear all lobby data
        ClearLobbyData();

        // 4. Show main menu (don't call LeaveLobbyAndReturnToMainMenu recursively!)
        mainMenuPanel.SetActive(true);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(false);

        Debug.Log("✅ Returned to main menu");
    }

    /// <summary>
    /// Hosting
    /// </summary>

    public void OnHostButtonClicked()
    {
        ShowHostingPanel();
    }

    public void ShowHostingPanel()
    {
        mainMenuPanel.SetActive(false);
        hostingPanel.SetActive(true);
        joiningPanel.SetActive(false);
        isHost = true;
        InitializeHosting();
    }

    async void InitializeHosting()
    {
        if (startGameButton != null)
        {
            var buttonText = startGameButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.SetText("Creating Lobby...");
            }
            startGameButton.interactable = false;
        }

        try
        {
            hostAllocation = await RelayService.Instance.CreateAllocationAsync(2);
            relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);
            lobbyCode = relayJoinCode;
            hostLobbyCodeText.text = lobbyCode;

            ConfigureRelayTransportForHost(hostAllocation);

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("✅ Host listening for connections");
            }

            SetupHostingDropdowns();
            playersInLobby.Clear();
            playersInLobby.Add("Player 1 (You)");
            UpdateHostingPlayerList();

            Debug.Log($"✅ Lobby created! Join Code: {lobbyCode}");
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to create Relay allocation: {e.Message}");
            hostLobbyCodeText.text = "ERROR";
            await ShowErrorPanelAsync($"Host failed: {e.Message}");
        }
        finally
        {
            if (startGameButton != null)
            {
                var buttonText = startGameButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (buttonText != null) buttonText.SetText("Start Game");
                startGameButton.interactable = true;
            }
        }
    }

    /// <summary>
    /// Joining
    /// </summary>

    public void OnMainJoinButtonClicked()
    {
        ShowJoiningPanel();
    }

    public void ShowJoiningPanel()
    {
        mainMenuPanel.SetActive(false);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(true);
        isHost = false;
        InitializeJoining();
    }

    void InitializeJoining()
    {
        joinLobbyCodeInput.text = "";
        joinGameTypeText.text = "(Game Type)";
        joinTimeText.text = "(Time)";
        playersInLobby.Clear();
        UpdateJoiningPlayerList();
        joinButton.interactable = false;
        joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Join";
    }

    public async void OnJoinButtonClicked()
    {
        await TryJoinLobbyAsync(joinLobbyCodeInput.text);
    }

    async Task TryJoinLobbyAsync(string code)
    {
        code = code.Trim().ToUpper();

        if (code.Length < 6)
        {
            Debug.LogWarning("Lobby code must be 6 characters!");
            return;
        }

        joinButton.interactable = false;
        joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Joining...";

        try
        {
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode: code);

            ConfigureRelayTransportForClient(joinAllocation);

            lobbyCode = code;

            // ✅ Use current network-synced values (will be updated via RPC)
            joinGameTypeText.text = gameType;
            joinTimeText.text = gameTime;

            playersInLobby.Clear();
            playersInLobby.Add("Player 1 (Host)");
            playersInLobby.Add("Player 2 (You)");
            UpdateJoiningPlayerList();

            joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Ready";
            Debug.Log($"✅ Joined lobby: {lobbyCode}");

            StartNetworkConnection();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            await ShowErrorPanelAsync($"Join failed: {e.Message}");
            joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Join";
            joinButton.interactable = true;
        }
    }

    /// <summary>
    /// Relay Transport Config
    /// </summary>

    void ConfigureRelayTransportForHost(Allocation allocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return;

        var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        transport.SetRelayServerData(relayServerData);
        Debug.Log("Relay transport configured for HOST");
    }

    void ConfigureRelayTransportForClient(JoinAllocation joinAllocation)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return;

        var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
        transport.SetRelayServerData(relayServerData);
        Debug.Log("Relay transport configured for CLIENT");
    }

    /// <summary>
    /// Game Start
    /// </summary>

    public void StartGame()
    {
        if (!isHost) return;

        if (playersInLobby.Count < 2)
        {
            Debug.LogWarning("Wait for at least one player to join!");
            return;
        }

        Debug.Log($"🎮 Starting game with {playersInLobby.Count} players!");

        // ✅ This is correct for modern Netcode
        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("❌ NetworkSceneManager is null!");
        }
    }

    /// <summary>
    /// UI helpers
    /// </summary>

    void SetupHostingDropdowns()
    {
        hostGameTypeDropdown.ClearOptions();
        hostGameTypeDropdown.AddOptions(new List<string> { "12 Men's Morris", "9 Men's Morris", "6 Men's Morris" });
        hostGameTypeDropdown.onValueChanged.RemoveAllListeners();
        hostGameTypeDropdown.onValueChanged.AddListener(OnHostGameTypeChanged);

        hostTimeDropdown.ClearOptions();
        hostTimeDropdown.AddOptions(new List<string> { "5:00", "10:00", "15:00" });
        hostTimeDropdown.onValueChanged.RemoveAllListeners();
        hostTimeDropdown.onValueChanged.AddListener(OnHostTimeChanged);
    }

    void OnHostGameTypeChanged(int index)
    {
        if (!isHost) return;
        string[] options = { "12 Men's Morris", "9 Men's Morris", "6 Men's Morris" };
        RequestLobbySettingsUpdateServerRpc(options[index], gameTime);
    }

    void OnHostTimeChanged(int index)
    {
        if (!isHost) return;
        string[] options = { "5:00", "10:00", "15:00" };
        RequestLobbySettingsUpdateServerRpc(gameType, options[index]);
    }

    void UpdateHostingPlayerList()
    {
        foreach (Transform child in hostPlayerListContainer) Destroy(child.gameObject);

        for (int i = 0; i < playersInLobby.Count && i < 3; i++)
        {
            PlayerSlots slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            slot.Initialize(playersInLobby[i], i + 1);
        }
        for (int i = playersInLobby.Count; i < 3; i++)
        {
            PlayerSlots slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            slot.Initialize("", i + 1);
            slot.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    void UpdateJoiningPlayerList()
    {
        foreach (Transform child in joinPlayerListContainer) Destroy(child.gameObject);

        for (int i = 0; i < playersInLobby.Count && i < 3; i++)
        {
            PlayerSlots slot = Instantiate(joinPlayerSlotPrefab, joinPlayerListContainer);
            slot.Initialize(playersInLobby[i], i + 1);
        }
        for (int i = playersInLobby.Count; i < 3; i++)
        {
            PlayerSlots slot = Instantiate(joinPlayerSlotPrefab, joinPlayerListContainer);
            slot.Initialize("", i + 1);
            slot.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    void UpdateJoinButtonState(string input)
    {
        joinButton.interactable = input.Trim().Length >= 6;
    }

    void OnLobbyCodeSubmitted(string input)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnJoinButtonClicked();
        }
    }

    void ClearLobbyData()
    {
        lobbyCode = "";
        relayJoinCode = "";
        gameType = "12 Men's Morris";
        gameTime = "10:00";
        playersInLobby.Clear();
        isHost = false;
        hostAllocation = default;
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

    /// <summary>
    /// Error panel
    /// </summary>

    async System.Threading.Tasks.Task ShowErrorPanelAsync(string message)
    {
        if (errorPanel == null || errorText == null) return;

        errorText.text = message;
        errorPanel.SetActive(true);
        await System.Threading.Tasks.Task.Delay(2000);
        errorPanel.SetActive(false);
    }

    /// <summary>
    /// Networking
    /// </summary>

    public void StartNetworkConnection()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager not found in scene!");
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("Already connected to a network session");
            return;
        }

        if (isHost)
        {
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("🎮 Host started successfully via Relay!");
            }
            else
            {
                Debug.LogError("❌ Failed to start host");
            }
        }
        else
        {
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("🎮 Client connected successfully via Relay!");
            }
            else
            {
                Debug.LogError("❌ Failed to start client");
            }
        }
    }

    /// <summary>
    /// RPCs for syncing lobby settings and player list
    /// </summary>

    [ServerRpc]
    void RequestLobbySettingsUpdateServerRpc(string newGameType, string newTime)
    {
        // Update authoritative values on server
        gameType = newGameType;
        gameTime = newTime;

        // Broadcast to all clients
        UpdateLobbySettingsClientRpc(gameType, gameTime);
    }

    [ClientRpc]
    void UpdateLobbySettingsClientRpc(string newGameType, string newTime)
    {
        // Runs on ALL clients (including host)
        gameType = newGameType;
        gameTime = newTime;

        // Update UI based on role
        if (isHost)
        {
            // Sync dropdown visual selection on host
            if (hostGameTypeDropdown != null)
            {
                string[] typeOptions = { "12 Men's Morris", "9 Men's Morris", "6 Men's Morris" };
                int index = System.Array.IndexOf(typeOptions, newGameType);
                if (index >= 0) hostGameTypeDropdown.value = index;
            }
            if (hostTimeDropdown != null)
            {
                string[] timeOptions = { "5:00", "10:00", "15:00" };
                int index = System.Array.IndexOf(timeOptions, newTime);
                if (index >= 0) hostTimeDropdown.value = index;
            }
        }
        else
        {
            // Update joining panel on clients
            if (joinGameTypeText != null) joinGameTypeText.text = newGameType;
            if (joinTimeText != null) joinTimeText.text = newTime;
        }
    }

    [ClientRpc]
    void UpdatePlayerListClientRpc(string playerNamesDelimited)
    {
        // ✅ FIXED: Use delimited string instead of string[]

        // Split the delimited string back into a list
        var playerNames = string.IsNullOrEmpty(playerNamesDelimited)
            ? new List<string>()
            : new List<string>(playerNamesDelimited.Split('|'));

        playersInLobby = playerNames;

        // Update UI based on role
        if (isHost)
        {
            UpdateHostingPlayerList();
            // Enable start button if 2+ players ready
            if (startGameButton != null)
            {
                startGameButton.interactable = playersInLobby.Count >= 2;
            }
        }
        else
        {
            UpdateJoiningPlayerList();
        }
    }

    void OnClientConnected(ulong clientId)
    {
        // Only the host/server should handle this
        if (!IsServer) return;

        Debug.Log($"🎉 Client connected: {clientId}");

        // Add player to the list (on host only)
        string playerName = $"Player {playersInLobby.Count + 1}";
        playersInLobby.Add(playerName);

        // Update host UI
        UpdateHostingPlayerList();

        // 🔥 Notify ALL clients using delimited string
        string delimited = string.Join("|", playersInLobby);
        UpdatePlayerListClientRpc(delimited);
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"👋 Client disconnected: {clientId}");

        // Remove last player (simplified approach)
        if (playersInLobby.Count > 0)
        {
            playersInLobby.RemoveAt(playersInLobby.Count - 1);
            string delimited = string.Join("|", playersInLobby);
            UpdatePlayerListClientRpc(delimited);
        }
    }
}