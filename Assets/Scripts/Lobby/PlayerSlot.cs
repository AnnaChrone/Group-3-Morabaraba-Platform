using TMPro;
using UnityEngine;

public class PlayerSlot : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;

    public void Initialize(string name, int playerNumber)
    {
        if (string.IsNullOrEmpty(name))
        {
            playerNameText.text = $"Player {playerNumber}";
        }
        else
        {
            playerNameText.text = name;
        }
    }
}
