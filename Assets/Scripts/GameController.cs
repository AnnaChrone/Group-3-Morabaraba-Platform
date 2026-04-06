using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public int currentPlayer = 1; // 1 = Player 1, 2 = Player 2
    public int placementCounter = 0;

    public SlotID selectedSlot = null;

    Dictionary<int, int[]> adjacency = new Dictionary<int, int[]>()
{
    {1, new int[] {2, 8, 9}},
    {2, new int[] {1,3, 10}},
    {3, new int[] {2,4, 11}},
    {4, new int[] {3,5, 12}},
    {5, new int[] {4,6, 13}},
    {6, new int[] {5,7, 14}},
    {7, new int[] {6,8, 15}},
    {8, new int[] {7,1, 16}},
    {9, new int[] {1,10, 16,17}},
    {10, new int[] {2,9, 11,18}},
    {11, new int[] {3,10,12, 19}},
    {12, new int[] {4,11,13,20}},
    {13, new int[] {5,12,14,21}},
    {14, new int[] {6,13,15,22}},
    {15, new int[] {7,14,16,23}},
    {16, new int[] {8,9,15,24}},
    {17, new int[] {9,18,24}},
    {18, new int[] {10,17,19}},
    {19, new int[] {11,18,20}},
    {20, new int[] {12,19,21}},
    {21, new int[] {13,20,22}},
    {22, new int[] {14,21,23}},
    {23, new int[] {15,22,24}},
    {24, new int[] {16,17,23}}
};

    // This is perfectly fine and actually preferred for Morabaraba!
    int[][] mills = new int[][]
    {
    new int[] {1,2,3}, new int[] {3,4,5}, new int[] {5,6,7}, new int[] {7,8,1},
    new int[] {9,10,11}, new int[] {11,12,13}, new int[] {13,14,15}, new int[] {15,16,9},
    new int[] {17,18,19}, new int[] {19,20,21}, new int[] {21,22,23}, new int[] {23,24,17},
    new int[] {1,9,17}, new int[] {2,10,18}, new int[] {3,11,19}, new int[] {4,12,20},
    new int[] {5,13,21}, new int[] {6,14,22}, new int[] {7,15,23}, new int[] {8,16,24}
    };

    public SlotID[] allSlots;

    SlotID GetSlotByNumber(int number)
    {
        return allSlots.First(s => s.slotNumber == number);
    }

    void SwitchPlayer()
    {
        currentPlayer = (currentPlayer == 1) ? 2 : 1;
    }

    bool IsAdjacent(SlotID from, SlotID to)
    {
        return adjacency[from.slotNumber].Contains(to.slotNumber);
    }

    public void OnSlotClicked(SlotID slot)
    {
        switch (currentPhase)
        {
            case GamePhase.Placing:
                HandlePlacing(slot);
                break;

            case GamePhase.Moving:
                HandleMoving(slot);
                break;

            case GamePhase.Capturing:
                HandleCapturing(slot);
                break;
        }
    }

    public void HandlePlacing(SlotID slot)
    {
        if (slot.IsOccupied) return;

        slot.SetOccupant(currentPlayer);

        placementCounter++; //count every placement until it hits 24

        // Check for mill
        if (CheckMill(slot))
        {
            currentPhase = GamePhase.Capturing;
            return;
        }

        // Switch to moving phase after all cows placed
        if (placementCounter >= 24)
        {
            currentPhase = GamePhase.Moving;
        }

        SwitchPlayer();
    }

    public void HandleMoving(SlotID slot)
    {
        //  STEP 1: No piece selected yet
        if (selectedSlot == null)
        {
            // Can only select your own piece
            if (slot.occupiedBy != currentPlayer)
                return;

            selectedSlot = slot;
            Debug.Log(slot + " Selected");
            selectedSlot.slotUI.Highlight(currentPlayer);

            return;
        }

        //Try move to new slot
        Debug.Log(
    "Trying move from " + selectedSlot.slotNumber +
    " to " + slot.slotNumber +
    " | IsEmpty: " + !slot.IsOccupied +
    " | Adjacent: " + IsAdjacent(selectedSlot, slot)
);
        // Must be empty
        if (!slot.IsOccupied && IsAdjacent(selectedSlot, slot))
        {
            // Move piece
            slot.SetOccupant(currentPlayer);
            selectedSlot.ClearSlot();

            // Clear highlight from previously selected slot
            selectedSlot.slotUI.ResetColor();
            selectedSlot = null;

            // Check for mill
            if (CheckMill(slot))
            {
                currentPhase = GamePhase.Capturing;
                return;
            }

            SwitchPlayer();
        }
        else
        {
            // Allow reselection (nice UX)
            if (slot.occupiedBy == currentPlayer)
            {
                // Clear previous highlight
                if (selectedSlot != null)
                    selectedSlot.slotUI.ResetColor();

                selectedSlot = slot;
                selectedSlot.slotUI.Highlight(currentPlayer);
            }
        }
    }

    public void HandleCapturing(SlotID slot)
    {
        // Can only remove opponent cows
        if (slot.occupiedBy == currentPlayer || !slot.IsOccupied || slot.isInMill)
            return;

        slot.ClearSlot();

        // After capture back to normal gameplay
        if (placementCounter >= 10)
        {
            currentPhase = GamePhase.Moving;
        }
        else
        {
            currentPhase = GamePhase.Placing;
        }

        SwitchPlayer();
    }

    public bool CheckMill(SlotID slot)
    {
        int player = slot.occupiedBy;

        foreach (var mill in mills)
        {
            // Only check mills that include this slot
            if (!mill.Contains(slot.slotNumber))
                continue;

            SlotID s1 = GetSlotByNumber(mill[0]);
            SlotID s2 = GetSlotByNumber(mill[1]);
            SlotID s3 = GetSlotByNumber(mill[2]);

            if (s1.occupiedBy == player &&
                s2.occupiedBy == player &&
                s3.occupiedBy == player)
            {
                Debug.Log("MILL FOR PLAYER " + player);

                // Visual feedback for mill
                s1.SetMillStatus(true);
                s2.SetMillStatus(true);
                s3.SetMillStatus(true);

                // Highlight the mill slots
                s1.slotUI.HighlightMill(player);
                s2.slotUI.HighlightMill(player);
                s3.slotUI.HighlightMill(player);

                return true;
            }
        }

        return false;
    }

    public GamePhase currentPhase = GamePhase.Placing;
}

public enum GamePhase
{
    Placing,
    Moving,
    Capturing
}