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
using Unity.Services.Lobbies.Models;

public class CreateUsername : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField userInputCreate;
    public TMP_InputField userPasswordCreate; //input field for pass word
    public TMP_InputField userInput;
    public TMP_InputField userPassword;
    public Button createAccount;
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

        await UnityServices.InitializeAsync();

        // Try to load existing username
       // await LoadUsername();

        // Enable button once ready
        if (signInButton != null) signInButton.interactable = true;
    }

    public async void OnCreateAccount()
    {
        if (isSigningIn) return;

        string username = userInputCreate.text.Trim();
        string password = userPasswordCreate.text.Trim();

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
        createAccount.interactable = false;
        statusText.text = "Creating account...";

    try
    {
         await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

        Debug.Log("Account created");
        PlayerData.Instance.SetUsername(username);

        await SaveCloudData(username, password); // optional

        LoadLobbyScene();
    }
    catch (AuthenticationException e)
    {
        Debug.LogError(e);
        statusText.text = "Account creation failed";
    }
    finally
    {
        isSigningIn = false;
        signInButton.interactable = true;
    }
    }


    public async void OnSignInButtonClicked()
    {
        // Prevent double-clicks
        if (isSigningIn) return;

        string username = userInput.text.Trim();
        string password = userPassword.text.Trim();

        // Validate username
        if (string.IsNullOrEmpty(username))
        {
            if (statusText != null) statusText.text = "Please enter a username";
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            if (statusText != null) statusText.text = "Please enter a password";
            return;
        }

        

        // Start sign-in process
        isSigningIn = true;
        if (signInButton != null) signInButton.interactable = false;
        if (statusText != null) statusText.text = "Signing in...";

         try
    {
        await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

        Debug.Log("Signed in as: " + AuthenticationService.Instance.PlayerId);
        PlayerData.Instance.SetUsername(username);

        await LoadCloudData(); // load wins/losses

        LoadLobbyScene();
    }
    catch (AuthenticationException e)
    {
        Debug.LogError(e);
        statusText.text = "Invalid login";
    }
    finally
    {
        isSigningIn = false;
        signInButton.interactable = true;
    }
    }
    async Task InitializeServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            //await AuthenticationService.Instance.SignInAnonymouslyAsync();
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

    public async Task SaveCloudData(string username, string password) //Create a save username button, add save password
    {
        var data = new Dictionary<string, object>
        {
            {"username", username},
            {"password", password},
            {"wins", PlayerData.Instance.wins},
            {"losses", PlayerData.Instance.losses},
            { "lastLogin", System.DateTime.UtcNow.ToString() }
            
        };

        await CloudSaveService.Instance.Data.ForceSaveAsync(data);
        Debug.Log("Username saved to CloudSave");
    }

    async Task LoadCloudData() //Move this to the sign in button
    {
        
        var data = await CloudSaveService.Instance.Data.LoadAsync(new HashSet<string> { "wins", "losses","username","password" });

        PlayerData.Instance.setWins(data.ContainsKey("wins") ? float.Parse(data["wins"].ToString()) : 0);
        PlayerData.Instance.setLoss(data.ContainsKey("losses") ? float.Parse(data["losses"].ToString()) : 0); // Sets to the cloud

        string username = data.ContainsKey("username") ? data["username"].ToString() : "";
        string password = data.ContainsKey("password") ? data["password"].ToString() : "";
       
       Debug.Log("Username: " + username);
       Debug.Log("Password: " + password);
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