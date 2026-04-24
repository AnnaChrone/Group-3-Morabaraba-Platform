using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CreateUsername : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField userInput;
    public Button signInButton;
    public TextMeshProUGUI statusText; // Optional: shows "Signing in..." feedback

    [Header("Scene Settings")]
    public string lobbySceneName = "Lobby"; // Change to your actual lobby scene name

    private bool isInitialized = false;
    private bool isSigningIn = false;

    async void Start()
    {
        // Disable button until services are ready
        if (signInButton != null) signInButton.interactable = false;

        await InitializeServices();

        // Try to load existing username
        await LoadUsername();

        // Enable button once ready
        if (signInButton != null) signInButton.interactable = true;
    }

    async Task InitializeServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            isInitialized = true;
            Debug.Log("Unity Services initialized and signed in");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize services: {e.Message}");
            if (statusText != null) statusText.text = "Connection failed";
        }
    }

    public async void OnSignInClicked()
    {
        if (isSigningIn) return;
        if (!isInitialized) return;

        string username = userInput.text.Trim();

        if (string.IsNullOrEmpty(username))
        {
            if (statusText != null) statusText.text = "Please enter a username";
            return;
        }

        isSigningIn = true;
        if (signInButton != null) signInButton.interactable = false;
        if (statusText != null) statusText.text = "Signing in...";

        try
        {
            await SaveUsername(username);

            // KEY CHANGE: Store in PlayerSession
            if (PlaySession.Instance != null)
            {
                PlaySession.Instance.SetAuthenticated(username);
            }
            else
            {
                // Fallback if PlayerSession doesn't exist yet
                PlayerPrefs.SetString("PlayerUsername", username);
                PlayerPrefs.Save();
            }

            Debug.Log($"Signed in as: {username}");

            // 🔥 Load the LOBBY scene (not main menu)
            LoadLobbyScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Sign in failed: {e.Message}");
            if (statusText != null) statusText.text = "Sign in failed";
            isSigningIn = false;
            if (signInButton != null) signInButton.interactable = true;
        }
    }

    void LoadLobbyScene()
    {
        // Load your lobby scene that contains UIManager + GameNetwork
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    async Task SaveUsername(string username)
    {
        var data = new Dictionary<string, object>
        {
            { "username", username },
            { "lastLogin", System.DateTime.UtcNow.ToString() }
        };

        await CloudSaveService.Instance.Data.ForceSaveAsync(data);
        Debug.Log("Username saved to CloudSave");
    }

    async Task LoadUsername()
    {
        try
        {
            var data = await CloudSaveService.Instance.Data.LoadAsync(new HashSet<string> { "username" });

            if (data.ContainsKey("username") && !string.IsNullOrEmpty(data["username"].ToString()))
            {
                string username = data["username"].ToString();
                userInput.text = username;
                Debug.Log($"Loaded saved username: {username}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not load saved username: {e.Message}");
        }
    }

    void ExecuteSceneLoad()
    {
        // Load the lobby scene (additive or single)
        SceneManager.LoadScene(lobbySceneName);

        // If you want to keep this scene loaded too (for persistent managers):
        // SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Additive);
    }
}