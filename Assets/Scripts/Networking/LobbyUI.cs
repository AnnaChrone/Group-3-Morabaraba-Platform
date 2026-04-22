using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;

public class LobbyUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InputField lobbyCodeInput;
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject inGamePanel;
    [SerializeField] private TextMeshProUGUI playerIndicator;
    [SerializeField] private TextMeshProUGUI lobbyCodeDisplay;

    [SerializeField] private GameNetwork _networkManager;

    //void Awake() => _networkManager = FindObjectOfType<GameNetwork>();

    void Start()
    {
        if (_networkManager != null)
        {
            // Update UI when player assignment is ready
            // (Simplified: assume host=1, client=2)
        }
    }

    public void OnCreateLobby()
    {
        SetInteractable(false);
        startGameButton.interactable = false; // Disable until ready
        statusText.text = "Creating lobby...";
        lobbyCodeDisplay.text = "";

        var unityContext = SynchronizationContext.Current;

        _networkManager.CreateLobbyAsync().ContinueWith(task =>
        {
            unityContext.Post(_ =>
            {
                if (task.Result)
                {
                    // ✅ STAY on lobby panel - don't call ShowInGameUI() yet

                    // Display lobby code
                    string code = _networkManager.GetLobbyCode();
                    if (!string.IsNullOrEmpty(code))
                    {
                        lobbyCodeDisplay.text = $"Code: {code}";
                        lobbyCodeDisplay.fontSize = 80;
                        lobbyCodeDisplay.color = Color.blue;
                    }

                    statusText.text = "Lobby created! Share code with friend.";
                    UpdatePlayerIndicator(1);

                    // ✅ Enable Start Game button for host
                    startGameButton.interactable = true;
                    startGameButton.gameObject.SetActive(true);
                }
                else
                {
                    SetInteractable(true);
                    startGameButton.gameObject.SetActive(false);
                    statusText.text = "Failed to create lobby";
                    lobbyCodeDisplay.text = "";
                }
            }, null);
        });
    }

    public void OnJoinLobby()
    {
        if (_networkManager == null)
        {
            statusText.text = "Network not ready!";
            return;
        }

        string code = lobbyCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            statusText.text = "Enter a lobby code";
            return;
        }

        SetInteractable(false);
        startGameButton.gameObject.SetActive(false); // Hide for clients
        statusText.text = "Joining...";

        var unityContext = SynchronizationContext.Current;

        _networkManager.JoinLobbyAsync(code).ContinueWith(task =>
        {
            unityContext.Post(_ =>
            {
                if (task.Result)
                {
                    // Clients stay on lobby panel waiting for host
                    // Don't call ShowInGameUI() here!

                    statusText.text = "Connected! Waiting for host to start...";
                    UpdatePlayerIndicator(2); // This will hide Start button
                }
                else
                {
                    SetInteractable(true);
                    statusText.text = "Failed to join";
                }
            }, null);
        });
    }

    /// <summary>
    /// Updates the player indicator text and color based on player number
    /// </summary>
    private void UpdatePlayerIndicator(int playerNumber)
    {
        if (playerIndicator == null) return;

        playerIndicator.gameObject.SetActive(true);

        switch (playerNumber)
        {
            case 1:
                playerIndicator.text = "You: Player 1 (Green) - Host";
                playerIndicator.color = Color.green;
                // Show Start Game button only for host
                if (startGameButton != null)
                    startGameButton.gameObject.SetActive(true);
                break;
            case 2:
                playerIndicator.text = "You: Player 2 (Red) - Joined";
                playerIndicator.color = Color.red;
                // Hide Start Game button for client
                if (startGameButton != null)
                    startGameButton.gameObject.SetActive(false);
                break;
            default:
                playerIndicator.text = $"You: Player {playerNumber}";
                playerIndicator.color = Color.white;
                if (startGameButton != null)
                    startGameButton.gameObject.SetActive(false);
                break;
        }
    }

    public void OnStartGame()
    {
        if (_networkManager == null) return;

        // Only host (Player 1) can start the game
        if (_networkManager.GetLocalPlayerNumber() != 1)
        {
            statusText.text = "Only the host can start the game!";
            return;
        }

        Debug.Log("Host starting game...");

        // Switch to in-game UI
        ShowInGameUI();

        // Notify network that game is starting
        _networkManager.StartGameServerRpc(); // We'll add this RPC below

        statusText.text = "Game started!";
        startGameButton.gameObject.SetActive(false);
    }

    private void SetInteractable(bool interactable)
    {
        createButton.interactable = interactable;
        joinButton.interactable = interactable;
        lobbyCodeInput.interactable = interactable;
    }

    private void ShowInGameUI()
    {
        lobbyPanel.SetActive(false);
        inGamePanel.SetActive(true);
    }
}
