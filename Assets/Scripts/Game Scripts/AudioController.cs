using System;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public AudioSource UIAudioPlayer;
    public AudioClip SelectSound;
    public AudioClip PlaceSound;
    public AudioClip MoveSound;
    public AudioClip FormMillSound;
    public AudioClip BreakMillSound;
    public AudioClip CaptureSound;
    public AudioClip WinSound;
    public AudioClip LossSound;
    public AudioClip DrawSound;
    public AudioClip FlyingSound;
    public AudioClip RewindSound;
    public AudioClip InvalidMoveSound;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    //Allows Awake() to be called for testing purposes
    public void InitializeForTesting()
    {
        Awake();
    }

    public void PlayAudio(string AudioClip)
    {
        if (string.IsNullOrEmpty(AudioClip))
        {
            Debug.LogWarning("Audio clip name is null or empty");
            return;  // Early return to prevent NullReferenceException
        }

        switch (AudioClip.ToLower()) //ensures it does not need to be case sensitive
        {
            case "select":
                UIAudioPlayer?.PlayOneShot(SelectSound);
                Debug.Log("Playing SELECT from AudioController");
                break;
            case "place":
                UIAudioPlayer?.PlayOneShot(PlaceSound);
                Debug.Log("Playing PLACE from AudioController");
                break;
            case "move":
                UIAudioPlayer?.PlayOneShot(MoveSound);
                Debug.Log("Playing MOVE from AudioController");
                break;
            case "formmill":
                UIAudioPlayer?.PlayOneShot(FormMillSound);
                Debug.Log("Playing FORMMILL from AudioController");
                break;
            case "breakmill":
                UIAudioPlayer?.PlayOneShot(BreakMillSound);
                Debug.Log("Playing BREAKMILL from AudioController");
                break;
            case "capture":
                UIAudioPlayer?.PlayOneShot(CaptureSound);
                Debug.Log("Playing CAPTURE from AudioController");
                break;
            case "win":
                UIAudioPlayer?.PlayOneShot(WinSound);
                Debug.Log("Playing WIN from AudioController");
                break;
            case "loss":
                UIAudioPlayer?.PlayOneShot(LossSound);
                Debug.Log("Playing LOSS from AudioController");
                break;
            case "draw":
                UIAudioPlayer?.PlayOneShot(DrawSound);
                Debug.Log("Playing DRAW from AudioController");
                break;
            case "rewind":
                UIAudioPlayer?.PlayOneShot(RewindSound);
                Debug.Log("Playing REWIND from AudioController");
                break;
            case "fly":
                UIAudioPlayer?.PlayOneShot(FlyingSound);
                Debug.Log("Playing FLY from AudioController");
                break;
            case "invalid":
                UIAudioPlayer?.PlayOneShot(InvalidMoveSound);
                Debug.Log("Playing INVALID from AudioController");
                break;
            default:
                Debug.LogWarning($"Unknown audio request: {AudioClip}");
                break;
        }
    }
}
