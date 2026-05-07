using UnityEngine;
using UnityEngine.InputSystem;

public class BasicFunctions : MonoBehaviour
{
    public GameObject CloseTarget;
    private bool open = false;
    public void QuitGame()
    {
        Debug.Log("Game is exiting...");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void onClose()
    {
        open = !open;
        CloseTarget.SetActive(open);
    }
}
