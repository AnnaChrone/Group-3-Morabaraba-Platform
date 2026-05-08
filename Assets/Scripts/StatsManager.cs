using UnityEngine;
using TMPro;

public class StatsManager : MonoBehaviour
{
    [Header("Player Stats Panel")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI winNumText;
    public TextMeshProUGUI lossNumText;
    public TextMeshProUGUI drawNumText;
    public GameObject profilePanel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        playerNameText.text = PlayerData.Instance.Username;
        winNumText.text = PlayerData.Instance.wins.ToString();
        lossNumText.text=PlayerData.Instance.losses.ToString();
        drawNumText.text = PlayerData.Instance.draw.ToString();
    }

     public void onOpenPlayerProfile()
    {
        profilePanel.SetActive(true);
    }

    public void onGoBackToMain()
    {
        profilePanel.SetActive(false);
    }
}
