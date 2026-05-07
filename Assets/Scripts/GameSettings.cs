using UnityEngine;

// Create this script if it doesn't exist
public static class GameSettings
{
    private static string _gameType = "Morabaraba";
    private static string _gameTime = "Casual";

    public static string GameType
    {
        get => _gameType;
        set
        {
            _gameType = value;
            Debug.Log($"GameSettings.GameType set to: {_gameType}");
        }
    }

    public static string GameTime
    {
        get => _gameTime;
        set
        {
            _gameTime = value;
            Debug.Log($"GameSettings.GameTime set to: {_gameTime}");
        }
    }

    // Optional: Reset settings when returning to main menu
    public static void ResetToDefaults()
    {
        _gameType = "Morabaraba";
        _gameTime = "Casual";
        Debug.Log("GameSettings reset to defaults");
    }
}