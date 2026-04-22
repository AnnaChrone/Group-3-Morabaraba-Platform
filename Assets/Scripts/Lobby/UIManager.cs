using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    [Header("Joining Panel References")]
    public TMP_InputField joinLobbyCodeInput;
    public Button joinButton;
    public TextMeshProUGUI joinGameTypeText;
    public TextMeshProUGUI joinTimeText;
    public Transform joinPlayerListContainer;
    public PlayerSlot joinPlayerSlotPrefab;

    // Lobby Data
    private string lobbyCode;
    private string gameType = "12 Men's Morris";
    private string gameTime = "10:00";
    private List<string> playersInLobby = new List<string>();
    private bool isHost = false;

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
        // Show main menu on start
        ShowMainMenu();
        // Listen for Enter key on the input field
        joinLobbyCodeInput.onEndEdit.AddListener(OnLobbyCodeSubmitted);

        // Enable/disable join button based on input
        joinLobbyCodeInput.onValueChanged.AddListener(UpdateJoinButtonState);
        UpdateJoinButtonState("");
    }

    #region Panel Switching

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(false);

        // Clear lobby data when returning to main menu
        ClearLobbyData();
    }

    public void ShowHostingPanel()
    {
        mainMenuPanel.SetActive(false);
        hostingPanel.SetActive(true);
        joiningPanel.SetActive(false);

        isHost = true;
        InitializeHosting();
    }

    public void ShowJoiningPanel()
    {
        mainMenuPanel.SetActive(false);
        hostingPanel.SetActive(false);
        joiningPanel.SetActive(true);

        isHost = false;
        InitializeJoining();
    }

    #endregion

    #region Hosting Functions

    void InitializeHosting()
    {
        // Generate lobby code
        GenerateLobbyCode();
        hostLobbyCodeText.text = lobbyCode;

        // Setup dropdowns
        SetupHostingDropdowns();

        // Add host as Player 1
        playersInLobby.Clear();
        playersInLobby.Add("Player 1");
        UpdateHostingPlayerList();
    }

    void GenerateLobbyCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Text.StringBuilder code = new System.Text.StringBuilder(6);

        for (int i = 0; i < 6; i++)
        {
            code.Append(chars[Random.Range(0, chars.Length)]);
        }

        lobbyCode = code.ToString();
    }

    void SetupHostingDropdowns()
    {
        // Game Type Dropdown
        hostGameTypeDropdown.ClearOptions();
        hostGameTypeDropdown.AddOptions(new List<string> { "12 Men's Morris", "9 Men's Morris", "6 Men's Morris" });
        hostGameTypeDropdown.onValueChanged.RemoveAllListeners();
        hostGameTypeDropdown.onValueChanged.AddListener(OnHostGameTypeChanged);

        // Time Dropdown
        hostTimeDropdown.ClearOptions();
        hostTimeDropdown.AddOptions(new List<string> { "5:00", "10:00", "15:00" });
        hostTimeDropdown.onValueChanged.RemoveAllListeners();
        hostTimeDropdown.onValueChanged.AddListener(OnHostTimeChanged);
    }

    void OnHostGameTypeChanged(int index)
    {
        string[] options = { "12 Men's Morris", "9 Men's Morris", "6 Men's Morris" };
        gameType = options[index];
    }

    void OnHostTimeChanged(int index)
    {
        string[] options = { "5:00", "10:00", "15:00" };
        gameTime = options[index];
    }

    void UpdateHostingPlayerList()
    {
        // Clear existing slots
        foreach (Transform child in hostPlayerListContainer)
        {
            Destroy(child.gameObject);
        }

        // Create slots for players
        for (int i = 0; i < playersInLobby.Count && i < 3; i++)
        {
            PlayerSlot slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            slot.Initialize(playersInLobby[i], i + 1);
        }

        // Fill empty slots
        for (int i = playersInLobby.Count; i < 3; i++)
        {
            PlayerSlot slot = Instantiate(hostPlayerSlotPrefab, hostPlayerListContainer);
            slot.Initialize("", i + 1);
            slot.GetComponent<UnityEngine.UI.Image>().color = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    public void OnHostReadyClicked()
    {
        // Host is always ready
        Debug.Log("Host is ready!");
    }

    public void StartGame()
    {
        if (playersInLobby.Count >= 1)
        {
            Debug.Log($"Host starting game with {playersInLobby.Count} players...");

            // Load the Game Scene
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }
        else
        {
            Debug.LogWarning("Not enough players to start!");
        }
    }

    // For testing - simulate player joining
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
        // Clear input
        joinLobbyCodeInput.text = "";
        joinGameTypeText.text = "(Game Type)";
        joinTimeText.text = "(Time)";

        // Clear player list
        playersInLobby.Clear();
        UpdateJoiningPlayerList();
    }

    public void OnLobbyCodeEntered(string code)
    {
        if (code.Length >= 6)
        {
            JoinLobby(code.ToUpper());
        }
    }

    void JoinLobby(string code)
    {
        lobbyCode = code;
        Debug.Log($"Attempting to join lobby: {code}");

        // In production, validate with server/networking
        // For now, simulate successful join
        SimulateSuccessfulJoin();
    }

    void SimulateSuccessfulJoin()
    {
        // Simulate receiving lobby data from host
        lobbyCode = joinLobbyCodeInput.text.Trim().ToUpper();
        gameType = "12 Men's Morris";
        gameTime = "10:00";

        joinGameTypeText.text = gameType;
        joinTimeText.text = gameTime;

        // Add joining player
        playersInLobby.Clear();
        playersInLobby.Add("Player 1"); // Host
        playersInLobby.Add("Player 2"); // You

        UpdateJoiningPlayerList();

        joinButton.interactable = true;
        joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Ready";

        Debug.Log($"✅ Joined lobby: {lobbyCode}");
    }

    void UpdateJoiningPlayerList()
    {
        // Clear existing slots
        foreach (Transform child in joinPlayerListContainer)
        {
            Destroy(child.gameObject);
        }

        // Create slots for players
        for (int i = 0; i < playersInLobby.Count && i < 3; i++)
        {
            PlayerSlot slot = Instantiate(joinPlayerSlotPrefab, joinPlayerListContainer);
            slot.Initialize(playersInLobby[i], i + 1);
        }

        // Fill empty slots
        for (int i = playersInLobby.Count; i < 3; i++)
        {
            PlayerSlot slot = Instantiate(joinPlayerSlotPrefab, joinPlayerListContainer);
            slot.Initialize("", i + 1);
            slot.GetComponent<UnityEngine.UI.Image>().color = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    public void OnJoinReadyClicked()
    {
        if (playersInLobby.Count < 2)
        {
            Debug.LogWarning("Join lobby first!");
            return;
        }

        Debug.Log("Player ready!");
        // Send ready signal to host
    }

    #endregion

    #region Utility

    void ClearLobbyData()
    {
        lobbyCode = "";
        gameType = "12 Men's Morris";
        gameTime = "10:00";
        playersInLobby.Clear();
        isHost = false;
    }

    #endregion

    void UpdateJoinButtonState(string input)
    {
        // Enable button only when code is 6+ characters
        joinButton.interactable = input.Trim().Length >= 6;
    }

    void OnLobbyCodeSubmitted(string input)
    {
        // Only trigger if pressed Enter (not just clicked away)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            TryJoinLobby(input);
        }
    }

    // Called by the Join Button's OnClick() event
    public void OnJoinButtonClicked()
    {
        TryJoinLobby(joinLobbyCodeInput.text);
    }

    void TryJoinLobby(string code)
    {
        code = code.Trim().ToUpper();

        if (code.Length < 6)
        {
            Debug.LogWarning("Lobby code must be 6 characters!");
            return;
        }

        // Optional: Show loading state
        joinButton.interactable = false;
        joinButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Joining...";

        // Simulate network delay (replace with actual networking call)
        Invoke(nameof(SimulateSuccessfulJoin), 0.5f);
    }
}