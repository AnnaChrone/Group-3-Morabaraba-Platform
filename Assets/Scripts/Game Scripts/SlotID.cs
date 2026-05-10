using UnityEngine;
using UnityEngine.UI;

public class SlotID : MonoBehaviour
{
    [Header("Slot Info")]
    public int slotNumber;          // 1–24 (must match your adjacency)
    public SlotUI slotUI;

    [Header("State")]
    public int occupiedBy = 0;      // 0 = none, 1 = player1, 2 = player2
    public bool isInMill = false;
    //private Image image;            // cached reference
    public GameController gameController;

    // Cleaner check
    public bool IsOccupied
    {
        get { return occupiedBy != 0; }
    }

    void Awake()
    {
        //image = GetComponent<Image>();
        slotUI = GetComponent<SlotUI>();
    }

    void OnMouseDown()
    {
        if (gameController != null && gameController.enabled && gameController.IsSpawned)
        {
            gameController.OnSlotClicked(this);
        }
    }

    // Place or move a cow
    public void SetOccupant(int player)
    {
        occupiedBy = player;

        if (player == 1)
            slotUI.SetPlayerColor(1);
        else if (player == 2)
            slotUI.SetPlayerColor(2);
        else
            slotUI.ResetColor(); // only here for actual empty state
    }

    // Clear the slot (VERY IMPORTANT for movement)
    public void ClearSlot()
    {
        occupiedBy = 0;
        isInMill = false;

        //image.color = Color.white;
        slotUI?.ResetColor();
    }

    // Mill state
    public void SetMillStatus(bool status)
    {
        isInMill = status;
    }
}