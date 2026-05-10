using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SlotUI : MonoBehaviour
{
    [Header("References")]
    public Image slotImage;   // The main image of the slot

    [Header("Colors")]
    public Color emptyColor = Color.white;
    public Color player1Color = Color.green;
    public Color player2Color = Color.red;
    public Color highlightplayer1Color = Color.yellow;
    public Color highlightplayer2Color = Color.yellow;
    public Color millplayer1Color = new Color(1f, 0.84f, 0f); 
    public Color millplayer2Color = new Color(1f, 0.84f, 0f); 

    private Color originalColor;

    void Awake()
    {
        // Auto-assign Image if missing
        if (slotImage == null)
            slotImage = GetComponent<Image>();

        originalColor = slotImage.color;
    }

    //Allows Awake() to be called for testing purposes
    public void InitializeForTesting()
    {
        Awake();
    }

    public void SetPlayerColor(int player)
    {
        switch (player)
        {
            case 1:
                slotImage.color = player1Color;
                break;
            case 2:
                slotImage.color = player2Color;
                break;
            default:
                slotImage.color = emptyColor;
                break;
        }
    }

    public void Highlight(int player)
    {
        switch (player)
        {
            case 1:
                slotImage.color = highlightplayer1Color;
                break;
            case 2:
                slotImage.color = highlightplayer2Color;
                break;
            default:
                slotImage.color = emptyColor;
                break;
        }
    }

    public void HighlightMill(int player)
    {
        switch (player)
        {
            case 1:
                slotImage.color = millplayer1Color;
                break;
            case 2:
                slotImage.color = millplayer2Color;
                break;
            default:
                slotImage.color = emptyColor;
                break;
        }
    }

    public void ResetColor()
    {
        slotImage.color = emptyColor;
    }

}