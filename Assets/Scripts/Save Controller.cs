using System.IO;
using UnityEngine;

public class SaveController : MonoBehaviour
{
    private string savePath;
    public float playerWins;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Define save path
        savePath = Path.Combine(Application.persistentDataPath, "saveData.json");
        LoadGame();
    }

     public void saveGame()
     {
        SaveData saveData = new SaveData
         {
            wins = playerWins
         };

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(savePath, json);

        Debug.Log("Game save to: " + savePath);
     }

     public void LoadGame()
     {
         if (File.Exists(savePath))
         {
            string json = File.ReadAllText(savePath);
            SaveData saveData = JsonUtility.FromJson<SaveData>(json);

            playerWins = saveData.wins;
            Debug.Log("Game Loaded, Wins: "+playerWins);
         }
         else
         {
            Debug.Log("No save file found.");
             saveGame();

         }
    }

    public void AddWin()
    {
        playerWins++;
        saveGame();

        Debug.Log("Total wins: "+playerWins);
    }
}
