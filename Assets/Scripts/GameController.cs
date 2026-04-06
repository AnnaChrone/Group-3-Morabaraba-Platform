using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public int currentPlayer = 1; // 1 = Player 1, 2 = Player 2
    public int placementCounter = 0;
    public int Player1PiecesOnBoard = 12;
    public int Player2PiecesOnBoard = 12;
    public bool END = false;


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
    public bool HasWon()
    {
        int opponent = (currentPlayer == 1) ? 2 : 1;

        int opponentPieces = (opponent == 1) ? Player1PiecesOnBoard : Player2PiecesOnBoard;
        if (placementCounter !=24)
            return false;

        // 1. Opponent has 2 or fewer pieces win
        if (opponentPieces <= 2)
            return true;

        // 2. Check if opponent has any valid moves
        bool canFly = (opponentPieces <= 3);

        foreach (var slot in allSlots)
        {
            if (slot.occupiedBy != opponent)
                continue;

            // Flying
            if (canFly)
            {
                if (allSlots.Any(s => !s.IsOccupied))
                    return false; // has a move NOT a win
            }
            else
            {
                // Normal movement
                foreach (int adj in adjacency[slot.slotNumber])
                {
                    SlotID adjSlot = GetSlotByNumber(adj);

                    if (!adjSlot.IsOccupied)
                        return false; // has a move NOT a win
                }
            }
        }

        // No moves found  win
        return true;
    }

    public void GameOver(int Winner)
    {
        Debug.Log("GAME OVER: WINNER IS PLAYER NO. " + Winner);
        currentPhase = GamePhase.End;
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
        // STEP 1: Select a piece
        if (selectedSlot == null)
        {
            // Can only select your own piece
            if (slot.occupiedBy != currentPlayer)
                return;

            selectedSlot = slot;
            selectedSlot.slotUI.Highlight(currentPlayer);
            return;
        }

        // STEP 2: Determine if player can "fly"
        bool canFly = (currentPlayer == 1 && Player1PiecesOnBoard <= 3) ||
                      (currentPlayer == 2 && Player2PiecesOnBoard <= 3);

        // STEP 3: Try move
        if (!slot.IsOccupied && (IsAdjacent(selectedSlot, slot) || canFly))
        {
            // Move piece
            slot.SetOccupant(currentPlayer);
            selectedSlot.ClearSlot();

            // Clear highlight
            selectedSlot.slotUI.ResetColor();
            selectedSlot = null;

            // Check for mill
            if (CheckMill(slot))
            {
                currentPhase = GamePhase.Capturing;
                return;
            }

            if (HasWon())
            {
                GameOver(currentPlayer);
                return;
            }

            SwitchPlayer();
        }
        else
        {
            // Allow reselection
            if (slot.occupiedBy == currentPlayer)
            {
                selectedSlot.slotUI.ResetColor();
                selectedSlot = slot;
                selectedSlot.slotUI.Highlight(currentPlayer);
            }
        }
    }

    bool OpponentHasFreePiece()
    {
        int opponent = (currentPlayer == 1) ? 2 : 1;

        foreach (var slot in allSlots)
        {
            // Check only opponent pieces
            if (slot.occupiedBy == opponent)
            {
                // If ANY piece is NOT in a mill, return true
                if (!slot.isInMill)
                    return true;
            }
        }

        // All opponent pieces are in mills
        return false;
    }
    public void HandleCapturing(SlotID slot)
    {
        int opponent = (currentPlayer == 1) ? 2 : 1;

        // Must be opponent piece and occupied
        if (slot.occupiedBy != opponent || !slot.IsOccupied)
            return;

        bool opponentHasFreePiece = OpponentHasFreePiece();

        //  If there ARE free pieces,cannot capture from a mill
        if (slot.isInMill && opponentHasFreePiece)
            return;

        //  Valid capture
        slot.ClearSlot();

        switch (currentPlayer)
        {
            case 1:
                Player2PiecesOnBoard--;
                break;
            case 2:
                Player1PiecesOnBoard--;
                break;
        }

        if (HasWon())
        {
            GameOver(currentPlayer);
            return;
        }
        // After capture back to normal gameplay
        if (placementCounter >= 24)
         { 
            currentPhase = GamePhase.Moving; 
         }
         else
         { 
            currentPhase = GamePhase.Placing; 
         }

        SwitchPlayer();
    }
    void UpdateAllMills()
    {
        // 1. Reset all slots
        foreach (var slot in allSlots)
        {
            slot.SetMillStatus(false);
           
        }

        // 2. Recalculate mills
        foreach (var mill in mills)
        {
            SlotID s1 = GetSlotByNumber(mill[0]);
            SlotID s2 = GetSlotByNumber(mill[1]);
            SlotID s3 = GetSlotByNumber(mill[2]);

            if (s1.IsOccupied &&
                s1.occupiedBy == s2.occupiedBy &&
                s2.occupiedBy == s3.occupiedBy)
            {
                s1.SetMillStatus(true);
                s2.SetMillStatus(true);
                s3.SetMillStatus(true);

                s1.slotUI.HighlightMill(s1.occupiedBy);
                s2.slotUI.HighlightMill(s2.occupiedBy);
                s3.slotUI.HighlightMill(s3.occupiedBy);
            }
        }
    }
    public bool CheckMill(SlotID slot)
    {
        UpdateAllMills();
        int player = slot.occupiedBy;

        foreach (var mill in mills)
        {
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
    Capturing,
    End
}