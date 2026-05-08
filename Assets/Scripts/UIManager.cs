using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
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
    private string gameType = "Morabaraba";
    private string gameTime = "10:00";
    private Dictionary<ulong, PlayerSceneState> playerSceneStates
    = new Dictionary<ulong, PlayerSceneState>();

    // INTERNAL: Raw player data with CID tags for networking
    private List<string> playersInLobbyRaw = new List<string>();

    // DISPLAY: Clean names for UI (generated from raw data)
    private List<string> playersInLobbyDisplay = new List<string>();

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
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Services initialization failed: {e.Message}");
        }

        //Spawn NetworkObject if not already spawned
        var networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsSpawned)
        {
            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            {
                networkObject.Spawn();
                Debug.Log("UIManager NetworkObject spawned");
            }
        }

        ShowMainMenu();

        joinLobbyCodeInput.onEndEdit.AddListener(OnLobbyCodeSubmitted);
        joinLobbyCodeInput.onValueChanged.AddListener(UpdateJoinButtonState);
        UpdateJoinButtonState("");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }


    }

    public void ReturnToMainMenuAsClient()
    {
        Debug.Log("Host disconnected - returning to main menu");

        if (errorPanel != null && errorText != null)
        {
            errorText.text = "Host left the lobby";
            errorPanel.SetActive(true);
            errorPanel.SetActive(false);
        }

        var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", 7777);
        }

        ClearLobbyData();

        mainMenuPanel.SetActive(true);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(false);
        UpdateJoinButtonState("");
    }

    private new void OnDestroy()
    {
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

    public void LeaveLobbyAndReturnToMainMenu()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("NetworkManager shutdown");
        }

        var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", 7777);
        }

        ClearLobbyData();

        mainMenuPanel.SetActive(true);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(false);

        Debug.Log(" Returned to main menu");
    }

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
            if (buttonText != null) buttonText.SetText("Creating Lobby...");
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
                Debug.Log(" Host listening for connections");
            }

            SetupHostingDropdowns();
            playersInLobbyRaw.Clear();

            string myName = "Guest";
            if (PlayerData.Instance != null && !string.IsNullOrEmpty(PlayerData.Instance.Username))
            {
                myName = PlayerData.Instance.Username;
                Debug.Log($" Using username from PlayerData: {myName}");
            }
            else
            {
                // Fallback to PlayerPrefs if PlayerData is missing
                if (PlayerPrefs.HasKey("PlayerUsername"))
                {
                    myName = PlayerPrefs.GetString("PlayerUsername");
                    Debug.Log($"PlayerData missing, using PlayerPrefs fallback: {myName}");
                }
                else
                {
                    Debug.LogWarning("No username found anywhere - using default 'Guest'");
                }
            }

            //  Add with proper format
            playersInLobbyRaw.Add($"{myName}|CID:0");
            Debug.Log($" Added to raw list: {playersInLobbyRaw[0]}");

            UpdateDisplayList();
            Debug.Log($" Display list generated: {string.Join(", ", playersInLobbyDisplay)}");

            UpdateHostingPlayerList();

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
        playersInLobbyRaw.Clear();
        playersInLobbyDisplay.Clear();
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

            joinGameTypeText.text = gameType;
            joinTimeText.text = gameTime;

            //  Don't pre-populate player list - wait for server broadcast
            playersInLobbyRaw.Clear();
            playersInLobbyDisplay.Clear();
            UpdateJoiningPlayerList();

            joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Ready";
            Debug.Log($" Joined lobby: {lobbyCode}");

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

    public void StartGame()
    {
        if (!isHost) return;

        if (playersInLobbyRaw.Count < 2)
        {
            Debug.LogWarning("Wait for at least one player to join!");
            return;
        }

        startGameButton.interactable = false;

        // Log current settings before starting game
        Debug.Log($"=== STARTING GAME WITH SETTINGS ===");
        Debug.Log($"GameType: {GameSettings.GameType}");
        Debug.Log($"GameTime: {GameSettings.GameTime}");

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            playerSceneStates[clientId] = PlayerSceneState.InGame;
            Debug.Log($"Marked Client {clientId} as InGame");
        }

        // Load the appropriate scene based on game type
        if (GameSettings.GameType == "6 Men's Morris")
        {
            NetworkManager.Singleton.SceneManager.LoadScene("SixMensMorris", LoadSceneMode.Additive);
        }
        else
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Additive);
        }
    }

    void SetupHostingDropdowns()
    {
        // Game Type Dropdown
        hostGameTypeDropdown.ClearOptions();
        hostGameTypeDropdown.AddOptions(new List<string> { "Morabaraba", "6 Men's Morris" });
        hostGameTypeDropdown.onValueChanged.RemoveAllListeners();
        hostGameTypeDropdown.onValueChanged.AddListener(OnHostGameTypeChanged);

        // Time Options Dropdown - Add all timer modes
        hostTimeDropdown.ClearOptions();
        hostTimeDropdown.AddOptions(new List<string> {
        "Casual",      // No timer
        "5:00",        // 5 minute game timer
        "10:00",       // 10 minute game timer  
        "15:00",       // 15 minute game timer
        "5s",          // 5 second turn timer
        "15s",         // 15 second turn timer
        "30s"          // 30 second turn timer
    });
        hostTimeDropdown.onValueChanged.RemoveAllListeners();
        hostTimeDropdown.onValueChanged.AddListener(OnHostTimeChanged);
    }

    void OnHostGameTypeChanged(int index)
    {
        if (!isHost) return;
        string[] options = { "Morabaraba", "6 Men's Morris" };
        string newGameType = options[index];

        Debug.Log($"Host changed game type to: {newGameType}");

        // Update local UI immediately
        gameType = newGameType;
        GameSettings.GameType = newGameType;

        // Send to server (which will broadcast to all clients)
        RequestLobbySettingsUpdateServerRpc(newGameType, gameTime);
    }


    void OnHostTimeChanged(int index)
    {
        if (!isHost) return;
        string[] options = { "Casual", "5:00", "10:00", "15:00", "5s", "15s", "30s" };
        string newTime = options[index];

        Debug.Log($"Host changed game time to: {newTime}");

        // Update local UI immediately
        gameTime = newTime;
        GameSettings.GameTime = newTime;

        // Send to server (which will broadcast to all clients)
        RequestLobbySettingsUpdateServerRpc(gameType, newTime);
    }

    void UpdateHostingPlayerList()
    {
        foreach (Transform child in hostPlayerListContainer) Destroy(child.gameObject);

        for (int i = 0; i < playersInLobbyDisplay.Count && i < 3; i++)
        {
            PlayerSlots slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            slot.Initialize(playersInLobbyDisplay[i], i + 1);
        }
        for (int i = playersInLobbyDisplay.Count; i < 3; i++)
        {
            PlayerSlots slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            slot.Initialize("", i + 1);
            slot.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    void UpdateJoiningPlayerList()
    {
        foreach (Transform child in joinPlayerListContainer) Destroy(child.gameObject);

        for (int i = 0; i < playersInLobbyDisplay.Count && i < 3; i++)
        {
            PlayerSlots slot = Instantiate(joinPlayerSlotPrefab, joinPlayerListContainer);
            slot.Initialize(playersInLobbyDisplay[i], i + 1);
        }
        for (int i = playersInLobbyDisplay.Count; i < 3; i++)
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
        playersInLobbyRaw.Clear();
        playersInLobbyDisplay.Clear();
        isHost = false;
        hostAllocation = default;
    }

    public void ResetToMainMenuWithDefaults()
    {
        GameSettings.ResetToDefaults();
        ClearLobbyData();
        ShowMainMenu();
    }

    public void SimulatePlayerJoin()
    {
        if (playersInLobbyRaw.Count < 3)
        {
            int playerNum = playersInLobbyRaw.Count + 1;
            // Use proper format for simulation
            playersInLobbyRaw.Add($"Player{playerNum}|CID:{100 + playerNum}");
            UpdateDisplayList();
            UpdateHostingPlayerList();
        }
    }

    async System.Threading.Tasks.Task ShowErrorPanelAsync(string message)
    {
        if (errorPanel == null || errorText == null) return;
        errorText.text = message;
        errorPanel.SetActive(true);
        await System.Threading.Tasks.Task.Delay(2000);
        errorPanel.SetActive(false);
    }

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
                Debug.Log("Host started successfully via Relay!");
                // Host already added in InitializeHosting, just broadcast
                BroadcastPlayerListUpdate();
            }
            else
            {
                Debug.LogError("Failed to start host");
            }
        }
        else
        {
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Client connected successfully via Relay!");
                // Start coroutine to send username after connection is ready
                StartCoroutine(SendUsernameAfterConnect());
            }
            else
            {
                Debug.LogError("Failed to start client");
            }
        }
    }

    System.Collections.IEnumerator SendUsernameAfterConnect()
    {
        // Wait for NetworkManager to report connected
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsConnectedClient
        );

        //  Wait for NetworkObjects to sync (critical for RPCs to work)
        float timeout = 5f;
        float elapsed = 0f;
        while (!GetComponent<NetworkObject>().IsSpawned && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!GetComponent<NetworkObject>().IsSpawned)
        {
            Debug.LogError("UIManager NetworkObject not spawned - cannot send username");
            yield break;
        }

        //  Get username with fallbacks
        string myName = "Guest";
        if (PlayerData.Instance != null && !string.IsNullOrEmpty(PlayerData.Instance.Username))
        {
            myName = PlayerData.Instance.Username;
        }
        else if (PlayerPrefs.HasKey("PlayerUsername"))
        {
            myName = PlayerPrefs.GetString("PlayerUsername");
        }

        Debug.Log($"Client sending username: '{myName}' (CID: {NetworkManager.Singleton.LocalClientId})");

        // Safety check before sending RPC
        if (Instance != null)
        {
            SendUsernameServerRpc(NetworkManager.Singleton.LocalClientId, myName);
        }
        else
        {
            Debug.LogError("UIManager.Instance is null - cannot send username RPC");
        }
    }

    [ServerRpc]
    void RequestLobbySettingsUpdateServerRpc(string newGameType, string newTime)
    {
        // Update the static settings on the server
        GameSettings.GameType = newGameType;
        GameSettings.GameTime = newTime;

        Debug.Log($"SERVER: Settings updated - GameType: {GameSettings.GameType}, GameTime: {GameSettings.GameTime}");

        // Broadcast to all clients
        UpdateLobbySettingsClientRpc(newGameType, newTime);
    }

    [ClientRpc]
    void UpdateLobbySettingsClientRpc(string newGameType, string newTime)
    {
        // Update local settings on clients
        GameSettings.GameType = newGameType;
        GameSettings.GameTime = newTime;

        gameType = newGameType;
        gameTime = newTime;

        Debug.Log($"CLIENT: Settings updated - GameType: {GameSettings.GameType}, GameTime: {GameSettings.GameTime}");

        if (isHost)
        {
            if (hostGameTypeDropdown != null)
            {
                string[] typeOptions = { "Morabaraba", "6 Men's Morris" };
                int index = System.Array.IndexOf(typeOptions, newGameType);
                if (index >= 0) hostGameTypeDropdown.value = index;
            }
            if (hostTimeDropdown != null)
            {
                string[] timeOptions = { "Casual", "5:00", "10:00", "15:00", "5s", "15s", "30s" };
                int index = System.Array.IndexOf(timeOptions, newTime);
                if (index >= 0) hostTimeDropdown.value = index;
            }
        }
        else
        {
            if (joinGameTypeText != null) joinGameTypeText.text = newGameType;
            if (joinTimeText != null) joinTimeText.text = newTime;
        }
    }

    [ClientRpc]
    void UpdatePlayerListClientRpc(string displayNamesDelimited)
    {
        var displayNames = string.IsNullOrEmpty(displayNamesDelimited)
            ? new List<string>()
            : new List<string>(displayNamesDelimited.Split('|'));

        //  Only update display list - raw data stays on server
        playersInLobbyDisplay = displayNames;

        if (isHost)
        {
            UpdateHostingPlayerList();
            if (startGameButton != null)
            {
                startGameButton.interactable = playersInLobbyDisplay.Count >= 2;
            }
        }
        else
        {
            UpdateJoiningPlayerList();
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"Client connected: {clientId}");

        playerSceneStates[clientId] = PlayerSceneState.InLobby;

        Debug.Log($"Added Client {clientId} to scene tracker");

        if (clientId == 0) return;

        playersInLobbyRaw.Add($"Player|CID:{clientId}");

        UpdateDisplayList();
        UpdateHostingPlayerList();
        BroadcastPlayerListUpdate();
    }

    void OnClientDisconnected(ulong clientId)
    {

        if (IsServer)
        {
            Debug.Log($" Client disconnected: {clientId}");


            playersInLobbyRaw.RemoveAll(p => p.Contains($"|CID:{clientId}"));

            UpdateDisplayList();
            BroadcastPlayerListUpdate();
            return;
        }

        if (hostingPanel.activeSelf || joiningPanel.activeSelf)
        {
            ReturnToMainMenuAsClient();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendUsernameServerRpc(ulong clientId, string username)
    {
        string cleanName = string.IsNullOrEmpty(username) ? "Guest" : username.Trim();
        if (cleanName.Length > 20) cleanName = cleanName.Substring(0, 20);

        Debug.Log($" Received username RPC: clientId={clientId}, name='{cleanName}'");

        //  Find entry by CID in RAW list
        int playerIndex = playersInLobbyRaw.FindIndex(p => p.Contains($"|CID:{clientId}"));

        if (playerIndex >= 0)
        {
            playersInLobbyRaw[playerIndex] = $"{cleanName}|CID:{clientId}";
            Debug.Log($" Updated existing entry: {playersInLobbyRaw[playerIndex]}");
        }
        else
        {
            //  Add new entry - but ONLY if it's not the host (CID:0)
            if (clientId == 0)
            {
                Debug.LogWarning(" Host username update received but no CID:0 entry found - re-adding");
                playersInLobbyRaw.Insert(0, $"{cleanName}|CID:0");
            }
            else
            {
                playersInLobbyRaw.Add($"{cleanName}|CID:{clientId}");
                Debug.Log($" Added new entry: {cleanName}|CID:{clientId}");
            }
        }

        UpdateDisplayList();
        BroadcastPlayerListUpdate();
    }

    //  Generate display list from raw data - host always first
    void UpdateDisplayList()
    {
        playersInLobbyDisplay.Clear();

        // Parse raw entries: "username|CID:xxx" → (name, clientId)
        var parsed = new List<(string name, ulong cid)>();

        foreach (var raw in playersInLobbyRaw)
        {
            if (string.IsNullOrEmpty(raw)) continue;

            //  More robust parsing with error handling
            int cidIndex = raw.LastIndexOf("|CID:");
            if (cidIndex > 0)
            {
                string name = raw.Substring(0, cidIndex);
                string cidPart = raw.Substring(cidIndex + 5); 

                if (ulong.TryParse(cidPart, out ulong cid))
                {
                    // Sanitize name
                    if (string.IsNullOrEmpty(name)) name = "Guest";
                    parsed.Add((name, cid));
                    Debug.Log($" Parsed: '{raw}' → name='{name}', cid={cid}");
                }
                else
                {
                    Debug.LogWarning($" Failed to parse CID from: {raw}");
                }
            }
            else
            {
                // ✅ Fallback: treat entire string as name, assign dummy CID
                Debug.LogWarning($" No CID tag found in: '{raw}' - treating as name only");
                parsed.Add((string.IsNullOrEmpty(raw) ? "Guest" : raw, 999));
            }
        }

        var host = parsed.FirstOrDefault(p => p.cid == 0);
        var others = parsed.Where(p => p.cid != 0).ToList();

        // Add host with role label
        if (!string.IsNullOrEmpty(host.name))
        {
            string label = IsServer ? "(You)" : "(Host)";
            playersInLobbyDisplay.Add($"{host.name} {label}".Trim());
            Debug.Log($" Host added to display: {host.name} {label}");
        }
        else if (parsed.Count > 0)
        {
            // Fallback: if no CID:0 found, use first entry as host
            var first = parsed[0];
            string label = IsServer ? "(You)" : "(Host)";
            playersInLobbyDisplay.Add($"{first.name} {label}".Trim());
            Debug.LogWarning($" No CID:0 host found, using first entry: {first.name}");
        }

        // Add others with "(You)" for local client
        foreach (var p in others)
        {
            if (string.IsNullOrEmpty(p.name)) continue;

            string label = (p.cid == NetworkManager.Singleton?.LocalClientId) ? " (You)" : "";
            playersInLobbyDisplay.Add($"{p.name}{label}".Trim());
        }

        Debug.Log($" Final display list: [{string.Join("], [", playersInLobbyDisplay)}]");
    }

    // Server-only: Broadcast display list to all clients
    void BroadcastPlayerListUpdate()
    {
        if (!IsServer) return;
        string delimited = string.Join("|", playersInLobbyDisplay);
        UpdatePlayerListClientRpc(delimited);
    }

    public void PlayerReturnedToLobby(ulong clientId)
    {
        if (!IsServer) return;
        playerSceneStates[clientId] = PlayerSceneState.InLobby;
        Debug.Log($"Client {clientId} returned to lobby");
        CheckAllPlayersReturned();
    }

    void CheckAllPlayersReturned()
    {
        foreach (var kvp in playerSceneStates)
        {
            Debug.Log($"Client {kvp.Key} = {kvp.Value}");
        }

        bool everyoneBack = playerSceneStates.Values
            .All(state => state == PlayerSceneState.InLobby);

        Debug.Log($"Is everyone Back? {everyoneBack}");

        if (everyoneBack)
        {
            Debug.Log("Everyone returned to lobby");

            startGameButton.interactable = true;
        }
    }

    public void NotifyReturnedToLobby()
    {
        NotifyReturnedToLobbyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void NotifyReturnedToLobbyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        Debug.Log($"Client {clientId} returned to lobby");

        PlayerReturnedToLobby(clientId);
    }

   
}

public enum PlayerSceneState { InLobby, LoadingGame, InGame, ReturningToLobby }
