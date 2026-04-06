using UnityEngine;
using UnityEngine.UI;

public class SlotID : MonoBehaviour
{
    [Header("Slot Info")]
    public int slotNumber;          // 1–24 (must match your adjacency)

    [Header("State")]
    public int occupiedBy = 0;      // 0 = none, 1 = player1, 2 = player2
    public bool isInMill = false;

    private Image image;            // cached reference

    // Cleaner check
    public bool IsOccupied
    {
        get { return occupiedBy != 0; }
    }

    void Awake()
    {
        image = GetComponent<Image>();
    }

    // Place or move a cow
    public void SetOccupant(int player)
    {
        occupiedBy = player;

        if (player == 1)
        {
            image.color = Color.green;
        }
        else if (player == 2)
        {
            image.color = Color.red;
        }
    }

    // Clear the slot (VERY IMPORTANT for movement)
    public void ClearSlot()
    {
        occupiedBy = 0;
        isInMill = false;

        image.color = Color.white; // fixes your issue
    }

    // Mill state (for later use)
    public void SetMillStatus(bool status)
    {
        isInMill = status;
    }
}