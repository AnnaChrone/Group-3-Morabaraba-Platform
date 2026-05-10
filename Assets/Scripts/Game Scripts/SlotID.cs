using UnityEngine;
using UnityEngine.UI;

public class SlotID : MonoBehaviour
{
    [Header("Slot Info")]
    public int slotNumber;  //Numbering of slots 1-24 (or 16 for Six Mens Morris)
    public SlotUI slotUI;

    [Header("State")]
    public int occupiedBy = 0;      // 0 = none, 1 = player1, 2 = player2
    public bool isInMill = false;
    public GameController gameController;

    //Checks who is occupying the slot
    public bool IsOccupied
    {
        get { return occupiedBy != 0; }
    }

    void Awake()
    {
        slotUI = GetComponent<SlotUI>();
    }

    //Allows Awake() to be called for testing purposes
    public void InitializeForTesting()
    {
        Awake();
    }

    void OnMouseDown()
    {
        if (gameController != null && gameController.enabled && gameController.IsSpawned)
        {
            gameController.OnSlotClicked(this);
        }
    }

    // Place or move a piece
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

    // Clear the slot when moved or captured
    public void ClearSlot()
    {
        occupiedBy = 0;
        isInMill = false;
        slotUI?.ResetColor();
    }

    // Mill state
    public void SetMillStatus(bool status)
    {
        isInMill = status;
    }
}