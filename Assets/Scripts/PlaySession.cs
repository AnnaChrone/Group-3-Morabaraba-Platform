using UnityEngine;

public class PlaySession : MonoBehaviour
{
    public static PlaySession Instance { get; private set; }

    public string Username { get; private set; } = "Guest";
    public bool IsAuthenticated { get; private set; } = false;

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

    public void SetAuthenticated(string username)
    {
        Username = username;
        IsAuthenticated = true;
        PlayerPrefs.SetString("PlayerUsername", username);
        PlayerPrefs.Save();
        Debug.Log($"PlayerSession: {username} authenticated");
    }

    public void ClearSession()
    {
        Username = "Guest";
        IsAuthenticated = false;
        PlayerPrefs.DeleteKey("PlayerUsername");
    }
}
