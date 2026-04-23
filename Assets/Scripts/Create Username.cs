using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CreateUsername : MonoBehaviour
{
    public TMP_InputField userInput;
public bool isInitialized = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
 
    async void Start()
    {
        await initializeServices();
        await LoadUsername();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    async Task initializeServices()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log("Services initialized and signed in");
    }
    public void onSaveClick()
    {
        string username = userInput.text;

        if (username == "")
        {
            Debug.Log("Username is empty");
        }
        else
        {
            saveUsername(username);
        }
    }

    async Task saveUsername(string username)
    {
        var data = new Dictionary<string, object>
        {
            {"username",username}
        };

        await CloudSaveService.Instance.Data.ForceSaveAsync(data);
        Debug.Log("Username saved");
    }

    async Task LoadUsername()
{
    var data = await CloudSaveService.Instance.Data.LoadAsync(new HashSet<string> { "username" });

    if (data.ContainsKey("username"))
    {
        string username = data["username"];
        userInput.text = username;
    }
}

}
