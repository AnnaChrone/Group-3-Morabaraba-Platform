using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance { get; private set; }

    public string Username { get; private set; } = "Guest";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("✅ PlayerData initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            Username = "Guest";
        }
        else
        {
            Username = username.Trim();
            if (Username.Length > 20) Username = Username.Substring(0, 20);
        }
        Debug.Log($"📝 PlayerData.Username set to: '{Username}'");
    }

    public void ClearUsername()
    {
        Username = "Guest";
    }
}