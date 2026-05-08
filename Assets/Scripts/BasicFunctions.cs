using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class BasicFunctions : MonoBehaviour
{
    public GameObject CloseTarget;
    private bool open = false;

    // Reference to GameController
    private GameController gameController;

    void Start()
    {
        // Find GameController in the scene
        gameController = FindObjectOfType<GameController>();

        if (gameController == null)
        {
            Debug.LogWarning("GameController not found in scene!");
        }
    }

    public void QuitGame()
    {
        Debug.Log("Game is exiting...");

        // If in a multiplayer game and game hasn't ended, forfeit first
        if (gameController != null && gameController.IsSpawned && !gameController.GameEnded.Value)
        {
            Debug.Log("Player is in active game - sending forfeit before quit");

            // Send forfeit
            gameController.PlayerForfeit();

            // Give a small delay to ensure forfeit is sent
            StartCoroutine(ForfeitThenQuit());
        }
        else
        {
            Debug.Log("Not in active game - quitting directly");
            // Just quit normally if not in game
            PerformQuit();
        }
    }

    private IEnumerator ForfeitThenQuit()
    {
        yield return new WaitForSeconds(0.5f);
        PerformQuit();
    }

    private void PerformQuit()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void onClose()
    {
        open = !open;
        if (CloseTarget != null)
            CloseTarget.SetActive(open);
    }
}