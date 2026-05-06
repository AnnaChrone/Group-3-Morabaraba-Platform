using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;

public class CreateUsername : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField userInput;
    public TMP_InputField userPassword; //input field for pass word
    public Button signInButton;
    public TextMeshProUGUI statusText; // Optional: shows "Signing in..." feedback
    public GameObject signInPanel;

    [Header("Scene Settings")]
    public string lobbySceneName = "Lobby"; // Change to your actual lobby scene name

    [Header("Player Stats")]
    public float playerWins;
    public float playerLosses;
    private bool isInitialized = false;
    private bool isSigningIn = false;

  


    async void Start()
    {
        // Disable button until services are ready
        if (signInButton != null) signInButton.interactable = false;

        await InitializeServices();

        // Try to load existing username
       // await LoadUsername();

        // Enable button once ready
        if (signInButton != null) signInButton.interactable = true;
    }

    public async void OnCreateAccount()
    {
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

         isSigningIn = true;
        signInButton.interactable = false;
        statusText.text = "Creating account...";

    try
    {
        await SaveUsername(username);

         // 2. Save to persistent PlayerData singleton (for current session)
            if (PlayerData.Instance != null)
            {
                PlayerData.Instance.SetUsername(username);
                PlayerData.Instance.setWins(playerWins);
                PlayerData.Instance.setLoss(playerLosses);
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
           // LoadLobbyScene();

    }
    catch
    {
        statusText.text = "Failed to create account";
        isSigningIn = false;
        signInButton.interactable = true;
    }


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

        

        // Start sign-in process
        isSigningIn = true;
        if (signInButton != null) signInButton.interactable = false;
        if (statusText != null) statusText.text = "Signing in...";

        try
        {
             bool success = await LoadUsername(username);

        if (!success)
        {
            statusText.text = "Invalid login";
            isSigningIn = false;
            signInButton.interactable = true;
            return;
        }

        PlayerData.Instance.SetUsername(username);

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
            {"wins", playerWins},
            {"loss", playerLosses},
            { "lastLogin", System.DateTime.UtcNow.ToString() }
            
        };

        await CloudSaveService.Instance.Data.ForceSaveAsync(data);
        Debug.Log("Username saved to CloudSave");
    }

    async Task<bool> LoadUsername(string user) //Move this to the sign in button
    {
        
            var data = await CloudSaveService.Instance.Data.LoadAsync(new HashSet<string> { "username", "wins","losses" });

            if (!data.ContainsKey("username") && !string.IsNullOrEmpty(data["username"].ToString()))
            {
                return false;
                
            }

                string savedUser = data["username"].ToString();
                userInput.text = savedUser;

            if (savedUser != user) return false;

            if (data.ContainsKey("wins"))
            {
                playerWins = float.Parse(data["wins"].ToString());
            }
            else
            {
                playerWins = 0;
            }

            Debug.Log($"Loaded: {userInput.text}, Wins: {playerWins}");

            if (data.ContainsKey("losses"))
            {
                playerLosses = float.Parse(data["losses"].ToString());
            }
            else
            {
                playerLosses = 0;
            }

            return true;
       
    }

    void ExecuteSceneLoad()
    {
        // Load the lobby scene (additive or single)
        SceneManager.LoadScene(lobbySceneName);

        // If you want to keep this scene loaded too (for persistent managers):
        // SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Additive);
    }

    public void openSignInPanel()
    {
        signInPanel.SetActive(true);
    }

    public void closeSignInPanel()
    {
        signInPanel.SetActive(false);
    }

}