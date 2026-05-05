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
    public AudioClip FlyingSound;
    public AudioClip InvalidMoveSound;

    public void PlayAudio(string AudioClip)
    {
        switch (AudioClip)
        {
            case "Select":
                UIAudioPlayer.PlayOneShot(SelectSound);
                break;
            case "Place":
                UIAudioPlayer.PlayOneShot(SelectSound);
                break;
            case "Move":
                UIAudioPlayer.PlayOneShot(MoveSound);
                break;
            case "FormMill":
                UIAudioPlayer.PlayOneShot(FormMillSound);
                break;
            case "BreakMill":
                UIAudioPlayer.PlayOneShot(BreakMillSound);
                break;
            case "Capture":
                UIAudioPlayer.PlayOneShot(CaptureSound);
                break;
            case "Win":
                UIAudioPlayer.PlayOneShot(WinSound);
                break;
            case "Loss":
                UIAudioPlayer.PlayOneShot(LossSound);
                break;
            case "Fly":
                UIAudioPlayer.PlayOneShot(FlyingSound);
                break;
            case "Invalid":
                UIAudioPlayer.PlayOneShot(InvalidMoveSound);
                break;
            default:
                Console.WriteLine("Unknown Audio request.");
                break;
        }
    }
}
