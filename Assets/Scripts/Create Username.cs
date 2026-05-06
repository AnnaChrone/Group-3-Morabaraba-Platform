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
    public TMP_InputField userPassword; //input field for pass word
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
    public async void OnSignInButtonClicked()
    {
        // Prevent double-clicks
        if (isSigningIn) return;

        string username = userInput.text.Trim();

        // Validate username
        if (string.IsNullOrEmpty(username))
        {
            if (statusText != null) statusText.text = "Please enter a username";
            return;
        }

        if (username.Length < 3)
        {
            if (statusText != null) statusText.text = "Username must be 3+ characters";
            return;
        }

        if (username.Length > 20)
        {
            if (statusText != null) statusText.text = "Username must be under 20 characters";
            return;
        }

        // Start sign-in process
        isSigningIn = true;
        if (signInButton != null) signInButton.interactable = false;
        if (statusText != null) statusText.text = "Saving...";

        try
        {
            // 1. Save username to CloudSave for cross-device persistence
            await SaveUsername(username);

            // 2. Save to persistent PlayerData singleton (for current session)
            if (PlayerData.Instance != null)
            {
                PlayerData.Instance.SetUsername(username);
            }
            else
            {
                // Fallback: create PlayerData if it doesn't exist yet
                var playerDataObj = new GameObject("PlayerData");
                DontDestroyOnLoad(playerDataObj);
                var playerData = playerDataObj.AddComponent<PlayerData>();
                playerData.SetUsername(username);
                Debug.LogWarning("PlayerData was not found in scene - created new instance");
            }

            // 3. Optional: Store in PlayerPrefs for quick local fallback access
            PlayerPrefs.SetString("PlayerUsername", username);
            PlayerPrefs.Save();

            Debug.Log($"✅ Username saved: {username}");

            // 4. Load the lobby scene
            LoadLobbyScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save username: {e.Message}");
            if (statusText != null) statusText.text = "Save failed. Try again.";

            // Re-enable button on error so user can retry
            isSigningIn = false;
            if (signInButton != null) signInButton.interactable = true;
        }
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

    void LoadLobbyScene()
    {
        // Load your lobby scene that contains UIManager + GameNetwork
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    async Task SaveUsername(string username) //Create a save username button, add save password
    {
        var data = new Dictionary<string, object>
        {
            { "username", username },
            { "lastLogin", System.DateTime.UtcNow.ToString() }
        };

        await CloudSaveService.Instance.Data.ForceSaveAsync(data);
        Debug.Log("Username saved to CloudSave");
    }

    async Task LoadUsername() //Move this to the sign in button
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

    //Create password
}