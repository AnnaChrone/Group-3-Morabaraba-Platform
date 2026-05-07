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

    public void PlayAudio(string AudioClip)
    {
        switch (AudioClip)
        {
            case "Select":
                UIAudioPlayer.PlayOneShot(SelectSound);
                Debug.Log("Playing SELECT from AudioController");
                break;
            case "Place":
                UIAudioPlayer.PlayOneShot(PlaceSound);
                Debug.Log("Playing Place from AudioController");
                break;
            case "Move":
                UIAudioPlayer.PlayOneShot(MoveSound);
                Debug.Log("Playing MOVE from AudioController");
                break;
            case "FormMill":
                UIAudioPlayer.PlayOneShot(FormMillSound);
                Debug.Log("Playing FORMMILL from AudioController");
                break;
            case "BreakMill":
                UIAudioPlayer.PlayOneShot(BreakMillSound);
                Debug.Log("Playing BREAKMILL from AudioController");
                break;
            case "Capture":
                UIAudioPlayer.PlayOneShot(CaptureSound);
                Debug.Log("Playing CAPTURE from AudioController");
                break;
            case "Win":
                UIAudioPlayer.PlayOneShot(WinSound);
                Debug.Log("Playing WIN from AudioController");
                break;
            case "Loss":
                UIAudioPlayer.PlayOneShot(LossSound);
                Debug.Log("Playing LOSS from AudioController");
                break;
            case "Draw":
                UIAudioPlayer.PlayOneShot(DrawSound);
                Debug.Log("Playing DRAW from AudioController");
                break;
            case "Rewind":
                UIAudioPlayer.PlayOneShot(RewindSound);
                Debug.Log("Playing REWIND from AudioController");
                break;
            case "Fly":
                UIAudioPlayer.PlayOneShot(FlyingSound);
                Debug.Log("Playing FLY from AudioController");
                break;
            case "Invalid":
                UIAudioPlayer.PlayOneShot(InvalidMoveSound);
                Debug.Log("Playing INVALID from AudioController");
                break;
            default:
                Console.WriteLine("Unknown Audio request.");
                break;
        }
    }
}
