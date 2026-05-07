using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance { get; private set; }

    public string Username { get; private set; } = "Guest";
    public string Password{get; private set;}
    public float wins {get; private set;}
    public float losses {get; private set;}

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log(" PlayerData initialized");
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
        Debug.Log($" PlayerData.Username set to: '{Username}'");
    }

    public void ClearUsername()
    {
        Username = "Guest";
    }

    public void SetPassword(string password)
    {
        Password = password.Trim();
    }

    public void setWins(float Wins)
    {
        wins = Wins;
    }
    public void AddWin()
    {
        wins++;
        Debug.Log("Wins for " + Username + ": " + wins);
    }

    public void setLoss(float lossNum)
    {
        losses = lossNum;
    }

    public void AddLoss()
    {
        losses++;
        Debug.Log("Losses for "+ Username + ": " +losses);
    }
}