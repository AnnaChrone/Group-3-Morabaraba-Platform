using UnityEngine;

public class SlotID : MonoBehaviour
{
    [Header("Slot Info")]
    public int slotNumber;          // 0–23 (unique ID for each slot)

    [Header("State")]
    public int occupiedBy = 0;      // 0 = none, 1 = player1, 2 = player2
    public bool isInMill = false;   // true if this slot is part of a mill

    // Optional helper property (cleaner checks)
    public bool IsOccupied
    {
        get { return occupiedBy != 0; }
    }

    // Call this when placing a cow
    public void SetOccupant(int player)
    {
        occupiedBy = player;
    }

    // Call this when removing a cow
    public void ClearSlot()
    {
        occupiedBy = 0;
        isInMill = false;
    }

    // Update mill status
    public void SetMillStatus(bool status)
    {
        isInMill = status;
    }
}