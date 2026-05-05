using System.IO;
using UnityEngine;

public class SaveController : MonoBehaviour
{
    private int playerWins;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Define player player stats
        //playerWins = Path.Combine(Application.persistentDataPath, "saveData.json");
    }

    // public void saveGame()
    // {
    //     SaveData saveData = new SaveData
    //     {
    //         //Find win data
    //     };

    //     File.WriteAllText(playerWins, JsonUtility.ToJson(saveData)); //player wins needs to be a string
    // }

    // public void LoadGame()
    // {
    //     if (File.Exists(playerWins))
    //     {
    //         SaveData saveData = JsonUtility.FromJson<SaveData>(File.ReadAllText(playerWins));
    //         //Set player wins to something
    //     }
    //     else
    //     {
    //         saveGame();
    //     }
    // }
}
