using UnityEngine;

public class BasicFunctions : MonoBehaviour
{
    public void QuitGame()
    {
        Debug.Log("Game is exiting...");

        Application.Quit();

        // If you're in the editor this will run
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
